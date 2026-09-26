using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Common.Domain;

/// <summary>
/// Single source of truth for which <see cref="LoadStatus"/> transitions a <c>Load</c> may make.
/// Per ADR-019, a load's lifecycle is expressed entirely as transitions on this status column —
/// there is no hard-delete path, so "removing" a load is always a transition to
/// <see cref="LoadStatus.Cancelled"/> (or a future <c>Discarded</c> value for abandoned drafts;
/// see the note on <see cref="CancellableStatuses"/>), recorded as a <c>LoadStatusHistory</c> row by
/// the service layer, never a <c>DELETE</c> statement.
/// </summary>
/// <remarks>
/// This class only encodes the transition graph and the two derived questions (editable? /
/// cancellable?) a service layer needs to answer before mutating a <c>Load</c>. It does not touch
/// the database, raise <c>ApiException</c>s, or write <c>LoadStatusHistory</c> rows — that
/// enforcement belongs to the (not-yet-implemented) load service, which should call
/// <see cref="CanTransition"/>/<see cref="CanEdit"/>/<see cref="CanCancel"/> before acting and
/// translate a <c>false</c> result into the contract's <c>422</c> invalid-state-transition response.
/// </remarks>
public static class LoadStatusTransitionRules
{
    /// <summary>
    /// The only statuses a load may be created into directly (<c>CreateLoadDto.PostImmediately</c>
    /// selects between them). Every other status is reachable only via a subsequent transition.
    /// </summary>
    public static readonly IReadOnlySet<LoadStatus> CreatableInitialStatuses =
        new HashSet<LoadStatus> { LoadStatus.Draft, LoadStatus.Posted };

    /// <summary>
    /// Statuses in which a load's content (<c>UpdateLoadDto</c> fields) may still be edited. Once a
    /// load has moved past <see cref="LoadStatus.Posted"/> — matched, in transit, etc. — its content
    /// is locked, since downstream records (matches, assignments, trips) may already depend on it.
    /// </summary>
    public static readonly IReadOnlySet<LoadStatus> EditableStatuses =
        new HashSet<LoadStatus> { LoadStatus.Draft, LoadStatus.Posted };

    /// <summary>
    /// Statuses from which a load may still be cancelled. <see cref="LoadStatus.Matched"/> is
    /// included deliberately: a match is only an AI recommendation pending Shipper approval
    /// (ADR-016) and/or agency accept/decline (ADR-017), so the shipper can still back out before a
    /// <c>Trip</c> exists. Once a load reaches <see cref="LoadStatus.InTransit"/> a physical pickup
    /// has already happened, so cancellation is no longer offered here — that scenario is handled by
    /// the separate dispute/trip-abort flow (Component B/C), not this endpoint.
    /// </summary>
    /// <remarks>
    /// ADR-019 also names a future <c>LoadStatus.Discarded</c> value, distinct from
    /// <see cref="LoadStatus.Cancelled"/>, specifically for abandoned drafts — that member does not
    /// exist on <see cref="LoadStatus"/> yet, so this table treats discarding a <c>Draft</c> as an
    /// ordinary cancel for now. Add it to <see cref="AllowedTransitions"/> and this set once the enum
    /// gains that member.
    /// </remarks>
    public static readonly IReadOnlySet<LoadStatus> CancellableStatuses =
        new HashSet<LoadStatus> { LoadStatus.Draft, LoadStatus.Posted, LoadStatus.Matched };

    /// <summary>
    /// The full transition graph: for each current status, the set of statuses it may move to next.
    /// A status absent as a key (<see cref="LoadStatus.Closed"/>, <see cref="LoadStatus.Cancelled"/>)
    /// is terminal — it has no outgoing transitions. Kept as one dictionary/table so the entire
    /// lifecycle is reviewable and testable in a single place, rather than scattered across
    /// conditionals in a future service.
    /// </summary>
    public static readonly IReadOnlyDictionary<LoadStatus, IReadOnlySet<LoadStatus>> AllowedTransitions =
        new Dictionary<LoadStatus, IReadOnlySet<LoadStatus>>
        {
            [LoadStatus.Draft] = new HashSet<LoadStatus> { LoadStatus.Posted, LoadStatus.Cancelled },
            [LoadStatus.Posted] = new HashSet<LoadStatus> { LoadStatus.Matched, LoadStatus.Cancelled },
            [LoadStatus.Matched] = new HashSet<LoadStatus> { LoadStatus.InTransit, LoadStatus.Cancelled, LoadStatus.Posted },
            [LoadStatus.InTransit] = new HashSet<LoadStatus> { LoadStatus.Delivered },
            [LoadStatus.Delivered] = new HashSet<LoadStatus> { LoadStatus.Closed },
            [LoadStatus.Closed] = new HashSet<LoadStatus>(),
            [LoadStatus.Cancelled] = new HashSet<LoadStatus>()
        };

    /// <summary>Whether a load may be created directly into <paramref name="initialStatus"/>.</summary>
    /// <param name="initialStatus">The status <c>CreateLoadDto.PostImmediately</c> resolves to.</param>
    /// <returns><c>true</c> if creation into this status is allowed.</returns>
    public static bool CanCreateAs(LoadStatus initialStatus) => CreatableInitialStatuses.Contains(initialStatus);

    /// <summary>Whether a load currently in <paramref name="currentStatus"/> may have its content edited.</summary>
    /// <param name="currentStatus">The load's current status.</param>
    /// <returns><c>true</c> if <c>UpdateLoadDto</c> edits are allowed in this status.</returns>
    public static bool CanEdit(LoadStatus currentStatus) => EditableStatuses.Contains(currentStatus);

    /// <summary>Whether a load currently in <paramref name="currentStatus"/> may be cancelled.</summary>
    /// <param name="currentStatus">The load's current status.</param>
    /// <returns><c>true</c> if a transition to <see cref="LoadStatus.Cancelled"/> is allowed from this status.</returns>
    public static bool CanCancel(LoadStatus currentStatus) => CancellableStatuses.Contains(currentStatus);

    /// <summary>
    /// Whether a load currently in <paramref name="currentStatus"/> may be published (transitioned to
    /// <see cref="LoadStatus.Posted"/>). Per <see cref="AllowedTransitions"/>, <see cref="LoadStatus.Posted"/>
    /// is reachable only from <see cref="LoadStatus.Draft"/> — every later status (<c>Matched</c> and
    /// beyond) is reached by internal processes (the AI matching workflow, trip events), never by this
    /// Shipper-facing action.
    /// </summary>
    /// <param name="currentStatus">The load's current status.</param>
    /// <returns><c>true</c> if a transition to <see cref="LoadStatus.Posted"/> is allowed from this status.</returns>
    public static bool CanPublish(LoadStatus currentStatus) => currentStatus == LoadStatus.Draft;

    /// <summary>Whether a direct transition from <paramref name="from"/> to <paramref name="to"/> is allowed.</summary>
    /// <param name="from">The load's current status.</param>
    /// <param name="to">The status being transitioned to.</param>
    /// <returns><c>true</c> if the transition is a legal single step in the lifecycle graph.</returns>
    public static bool CanTransition(LoadStatus from, LoadStatus to) =>
        AllowedTransitions.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>Every status a load in <paramref name="from"/> may legally transition to next.</summary>
    /// <param name="from">The load's current status.</param>
    /// <returns>The set of legal next statuses; empty for a terminal status.</returns>
    public static IReadOnlySet<LoadStatus> GetAllowedNextStatuses(LoadStatus from) =>
        AllowedTransitions.TryGetValue(from, out var next) ? next : new HashSet<LoadStatus>();
}
