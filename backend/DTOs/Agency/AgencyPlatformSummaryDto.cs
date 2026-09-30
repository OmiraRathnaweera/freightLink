namespace FreightLink.Api.DTOs.Agency;

/// <summary>
/// System-wide agency/driver/vehicle counts for the Admin "Registered Agencies &amp; Fleet"
/// dashboard's summary cards. Computed via direct COUNT queries against the whole table, never by
/// paging through and summing rows client-side — the page listing (<see cref="PagedAgencyResponseDto"/>)
/// is scoped to the admin's current search/status filter and page, so it must never be the source
/// for numbers presented as platform-wide totals (issue #45).
/// </summary>
public class AgencyPlatformSummaryDto
{
    public int TotalAgencies { get; set; }
    public int ActiveAgencies { get; set; }
    public int TotalDrivers { get; set; }
    public int ActiveDrivers { get; set; }
    public int TotalVehicles { get; set; }
}
