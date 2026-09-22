import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/primary_button.dart';
import '../../../shared/widgets/status_pill.dart';
import '../../auth/providers/auth_provider.dart';
import '../../notifications/models/in_app_notification.dart';
import '../../notifications/providers/notification_provider.dart';
import '../data/trips_repository.dart';
import '../models/trip_models.dart';
import 'proof_of_delivery_screen.dart';

/// Screen displayed to authenticated users with the Driver role.
/// Shows the driver's currently assigned active trip (Assigned, PickedUp, InTransit),
/// route details, vehicle information, cargo specifications, and transit lifecycle controls.
class DriverAssignedTripScreen extends StatefulWidget {
  const DriverAssignedTripScreen({
    super.key,
    this.tripsRepository,
    this.initialTrip,
    this.enableAutoPolling = true,
  });

  /// Optional repository override for testing.
  final TripsRepository? tripsRepository;

  /// Optional initial trip for widget tests or instant display.
  final TripResponse? initialTrip;

  /// Whether to automatically poll for new assignments when empty.
  final bool enableAutoPolling;

  @override
  State<DriverAssignedTripScreen> createState() => _DriverAssignedTripScreenState();
}

class _DriverAssignedTripScreenState extends State<DriverAssignedTripScreen>
    with WidgetsBindingObserver {
  TripResponse? _trip;
  bool _isLoading = false;
  bool _isAdvancing = false;
  String? _errorMessage;
  Timer? _pollTimer;
  bool _isInitialFetch = true;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    if (widget.initialTrip != null) {
      _trip = widget.initialTrip;
      _isInitialFetch = false;
    } else {
      _loadTrip();
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _stopPolling();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed && _trip == null) {
      _loadTrip(isSilent: true);
    }
  }

  void _startPollingIfEmpty() {
    if (!widget.enableAutoPolling) return;
    _stopPolling();
    if (_trip == null && mounted) {
      _pollTimer = Timer.periodic(const Duration(seconds: 5), (_) {
        if (!mounted || _trip != null || _isLoading) return;
        _loadTrip(isSilent: true);
      });
    }
  }

  void _stopPolling() {
    _pollTimer?.cancel();
    _pollTimer = null;
  }

  TripsRepository get _repository =>
      widget.tripsRepository ?? context.read<TripsRepository>();

  Future<void> _loadTrip({bool isSilent = false}) async {
    if (!isSilent) {
      setState(() {
        _isLoading = true;
        _errorMessage = null;
      });
    }

    try {
      final activeTrip = await _repository.getDriverActiveTrip();
      if (!mounted) return;

      final wasInitial = _isInitialFetch;
      _isInitialFetch = false;

      if (!wasInitial && _trip?.tripId != activeTrip?.tripId && activeTrip != null && activeTrip.isAssigned) {
        NotificationProvider? notifProvider;
        try {
          notifProvider = Provider.of<NotificationProvider>(context, listen: false);
        } catch (_) {
          notifProvider = null;
        }

        final tripCode = activeTrip.tripId.length > 8
            ? activeTrip.tripId.substring(0, 8).toUpperCase()
            : activeTrip.tripId.toUpperCase();

        notifProvider?.pushNotification(
          title: 'New Trip Assigned!',
          message: 'Trip #$tripCode has been assigned to your vehicle.',
          category: NotificationCategory.tripAssigned,
          referenceId: activeTrip.tripId,
        );
      }

      setState(() {
        _trip = activeTrip;
        _isLoading = false;
      });
      if (activeTrip != null) {
        _stopPolling();
      } else {
        _startPollingIfEmpty();
      }
    } on ApiException catch (e) {
      if (!mounted) return;
      if (!isSilent) {
        setState(() {
          _errorMessage = e.message;
          _isLoading = false;
        });
      }
    } catch (_) {
      if (!mounted) return;
      if (!isSilent) {
        setState(() {
          _errorMessage = 'Failed to load assigned trip. Please try again.';
          _isLoading = false;
        });
      }
    }
  }

  Future<void> _startTransit() async {
    if (_trip == null) return;

    setState(() => _isAdvancing = true);
    try {
      final updated = await _repository.changeTripStatus(
        tripId: _trip!.tripId,
        targetStatus: 'InTransit',
        notes: 'Driver departed pickup location and started transit.',
      );
      if (!mounted) return;
      setState(() {
        _trip = updated;
        _isAdvancing = false;
      });

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Trip status updated: In Transit'),
          backgroundColor: AppColors.primary,
        ),
      );
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => _isAdvancing = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(e.message),
          backgroundColor: AppColors.statusErrorFg,
        ),
      );
    } catch (_) {
      if (!mounted) return;
      setState(() => _isAdvancing = false);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Failed to update trip status.'),
          backgroundColor: AppColors.statusErrorFg,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final user = context.watch<AuthProvider>().user;
    final driverName = user?.fullName ?? 'Driver';

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: _loadTrip,
          color: AppColors.primary,
          child: CustomScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            slivers: [
              SliverToBoxAdapter(
                child: AppTopBar(
                  title: 'Driver Portal',
                  subtitle: driverName,
                  leading: const AppAvatar(),
                  actions: [
                    const NotificationBellButton(),
                    IconButton(
                      icon: const Icon(Icons.refresh_rounded, color: AppColors.ink),
                      tooltip: 'Refresh',
                      onPressed: _loadTrip,
                    ),
                  ],
                ),
              ),
              SliverPadding(
                padding: const EdgeInsets.all(AppConstants.spaceLg),
                sliver: SliverToBoxAdapter(
                  child: _buildContent(),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildContent() {
    if (_isLoading) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: 80),
        child: Center(
          child: CircularProgressIndicator(color: AppColors.primary),
        ),
      );
    }

    if (_errorMessage != null) {
      return _buildErrorCard();
    }

    if (_trip == null) {
      return _buildEmptyCard();
    }

    return _buildTripView(_trip!);
  }

  Widget _buildErrorCard() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceXl),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        children: [
          const Icon(
            Icons.error_outline_rounded,
            size: 48,
            color: AppColors.statusErrorFg,
          ),
          const SizedBox(height: AppConstants.spaceMd),
          Text(
            _errorMessage ?? 'An error occurred',
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: 15,
              fontWeight: FontWeight.w600,
              color: AppColors.ink,
            ),
          ),
          const SizedBox(height: AppConstants.spaceLg),
          PrimaryButton(
            label: 'Try Again',
            onPressed: _loadTrip,
          ),
        ],
      ),
    );
  }

  Widget _buildEmptyCard() {
    return Container(
      key: const Key('empty_trip_card'),
      padding: const EdgeInsets.symmetric(
        horizontal: AppConstants.spaceXl,
        vertical: AppConstants.spaceXxl,
      ),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
        boxShadow: const [
          BoxShadow(
            color: Color(0x08000000),
            blurRadius: 10,
            offset: Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 72,
            height: 72,
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.1),
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.local_shipping_outlined,
              size: 38,
              color: AppColors.primary,
            ),
          ),
          const SizedBox(height: AppConstants.spaceLg),
          const Text(
            'No Active Trip Assigned',
            style: TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.w700,
              color: AppColors.ink,
            ),
          ),
          const SizedBox(height: AppConstants.spaceSm),
          const Text(
            'You currently have no active trips assigned by your agency dispatcher. When a trip is assigned to your vehicle, it will appear here automatically.',
            textAlign: TextAlign.center,
            style: TextStyle(
              fontSize: 14,
              color: AppColors.inkMuted,
              height: 1.4,
            ),
          ),
          const SizedBox(height: AppConstants.spaceXl),
          SizedBox(
            width: double.infinity,
            child: OutlinedButton.icon(
              key: const Key('check_assignments_button'),
              icon: const Icon(Icons.refresh_rounded, size: 18),
              label: const Text('Check for Assignments'),
              style: OutlinedButton.styleFrom(
                foregroundColor: AppColors.primary,
                side: const BorderSide(color: AppColors.primary),
                padding: const EdgeInsets.symmetric(vertical: 14),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                ),
              ),
              onPressed: _loadTrip,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTripView(TripResponse trip) {
    return Column(
      key: const Key('active_trip_view'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (trip.isAssigned) ...[
          _buildNewAssignmentBanner(trip),
          const SizedBox(height: AppConstants.spaceLg),
        ],
        _buildHeaderCard(trip),
        const SizedBox(height: AppConstants.spaceLg),
        _buildRouteCard(trip),
        const SizedBox(height: AppConstants.spaceLg),
        _buildVehicleAndCargoCard(trip),
        const SizedBox(height: AppConstants.spaceLg),
        _buildPickupProofPolicyCard(trip),
        const SizedBox(height: AppConstants.spaceXl),
        _buildActionCard(trip),
      ],
    );
  }

  Widget _buildNewAssignmentBanner(TripResponse trip) {
    return Container(
      key: const Key('new_assignment_banner'),
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      decoration: BoxDecoration(
        color: AppColors.primary.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(
          color: AppColors.primary.withValues(alpha: 0.3),
          width: 1.5,
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.all(8),
            decoration: const BoxDecoration(
              color: AppColors.primary,
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.assignment_turned_in_rounded,
              color: AppColors.onPrimary,
              size: 20,
            ),
          ),
          const SizedBox(width: AppConstants.spaceMd),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'New Assigned Trip (Post-Approval)',
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w700,
                    color: AppColors.primary,
                    letterSpacing: -0.2,
                  ),
                ),
                const SizedBox(height: 3),
                const Text(
                  'Admin approved AI match candidate. You have been assigned to transport this load. Review pickup window and cargo requirements below.',
                  style: TextStyle(
                    fontSize: 12,
                    color: AppColors.ink,
                    height: 1.35,
                  ),
                ),
                if (trip.referenceCode != null && trip.referenceCode!.isNotEmpty) ...[
                  const SizedBox(height: 6),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                    decoration: BoxDecoration(
                      color: AppColors.surface,
                      borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                      border: Border.all(color: AppColors.border),
                    ),
                    child: Text(
                      'Assigned Load: ${trip.referenceCode}',
                      style: const TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                        color: AppColors.inkMuted,
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildHeaderCard(TripResponse trip) {
    final tripCode = trip.tripId.length > 8
        ? trip.tripId.substring(0, 8).toUpperCase()
        : trip.tripId.toUpperCase();

    final statusText = trip.isPickedUp
        ? 'PICKED UP'
        : trip.isInTransit
            ? 'IN TRANSIT'
            : trip.status.toUpperCase();

    final (fg, bg) = trip.isPickedUp
        ? (AppColors.statusSuccessFg, AppColors.statusSuccessBg)
        : trip.isInTransit
            ? (AppColors.statusInTransitFg, AppColors.statusInTransitBg)
            : (AppColors.statusMatchedFg, AppColors.statusMatchedBg);

    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
        boxShadow: const [
          BoxShadow(
            color: Color(0x06000000),
            blurRadius: 8,
            offset: Offset(0, 2),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                ),
                child: Text(
                  'TRIP #$tripCode',
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: AppColors.primary,
                    letterSpacing: 0.5,
                  ),
                ),
              ),
              StatusPill(
                label: statusText,
                foreground: fg,
                background: bg,
              ),
            ],
          ),
          if (trip.referenceCode != null && trip.referenceCode!.isNotEmpty) ...[
            const SizedBox(height: AppConstants.spaceSm),
            Text(
              'Load Ref: ${trip.referenceCode}',
              style: const TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w500,
                color: AppColors.inkMuted,
              ),
            ),
          ],
          const SizedBox(height: AppConstants.spaceMd),
          Row(
            children: [
              _buildMetricChip(
                icon: Icons.straighten_rounded,
                label: trip.formattedDistance,
              ),
              const SizedBox(width: AppConstants.spaceSm),
              _buildMetricChip(
                icon: Icons.access_time_rounded,
                label: trip.formattedDuration,
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildMetricChip({required IconData icon, required String label}) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: AppColors.background,
        borderRadius: BorderRadius.circular(AppConstants.radiusSm),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 14, color: AppColors.inkMuted),
          const SizedBox(width: 6),
          Text(
            label,
            style: const TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: AppColors.ink,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildRouteCard(TripResponse trip) {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Route Details',
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w700,
              color: AppColors.ink,
            ),
          ),
          const SizedBox(height: AppConstants.spaceLg),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Column(
                children: [
                  Container(
                    width: 14,
                    height: 14,
                    decoration: const BoxDecoration(
                      color: AppColors.statusSuccessFg,
                      shape: BoxShape.circle,
                    ),
                  ),
                  Container(
                    width: 2,
                    height: 52,
                    color: AppColors.border,
                  ),
                  Container(
                    width: 14,
                    height: 14,
                    decoration: const BoxDecoration(
                      color: AppColors.primary,
                      shape: BoxShape.circle,
                    ),
                  ),
                ],
              ),
              const SizedBox(width: AppConstants.spaceMd),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'PICKUP LOCATION',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: AppColors.inkMuted,
                        letterSpacing: 0.5,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      trip.pickupAddress ?? 'Origin Address',
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w600,
                        color: AppColors.ink,
                      ),
                    ),
                    if (trip.pickupWindowStart != null) ...[
                      const SizedBox(height: 2),
                      Text(
                        'Window: ${DateFormat('MMM d, h:mm a').format(trip.pickupWindowStart!)}',
                        style: const TextStyle(
                          fontSize: 12,
                          color: AppColors.inkMuted,
                        ),
                      ),
                    ],
                    const SizedBox(height: 20),
                    const Text(
                      'DROPOFF LOCATION',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: AppColors.inkMuted,
                        letterSpacing: 0.5,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      trip.dropoffAddress ?? 'Destination Address',
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w600,
                        color: AppColors.ink,
                      ),
                    ),
                    if (trip.pickupWindowEnd != null) ...[
                      const SizedBox(height: 2),
                      Text(
                        'Target: ${DateFormat('MMM d, h:mm a').format(trip.pickupWindowEnd!)}',
                        style: const TextStyle(
                          fontSize: 12,
                          color: AppColors.inkMuted,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildVehicleAndCargoCard(TripResponse trip) {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Vehicle & Cargo',
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w700,
              color: AppColors.ink,
            ),
          ),
          const SizedBox(height: AppConstants.spaceMd),
          Row(
            children: [
              const Icon(Icons.local_shipping_outlined, size: 20, color: AppColors.primary),
              const SizedBox(width: AppConstants.spaceSm),
              Text(
                trip.vehicleRegistrationNo ?? 'Vehicle Assigned',
                style: const TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.w700,
                  color: AppColors.ink,
                ),
              ),
              const Spacer(),
              if (trip.agencyName != null && trip.agencyName!.isNotEmpty)
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: AppColors.background,
                    borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                    border: Border.all(color: AppColors.border),
                  ),
                  child: Text(
                    trip.agencyName!,
                    style: const TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w500,
                      color: AppColors.inkMuted,
                    ),
                  ),
                ),
            ],
          ),
          const Divider(height: 24),
          if (trip.cargoDescription != null && trip.cargoDescription!.isNotEmpty) ...[
            Text(
              trip.cargoDescription!,
              style: const TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w600,
                color: AppColors.ink,
              ),
            ),
            const SizedBox(height: AppConstants.spaceSm),
          ],
          if (trip.formattedCargoSummary.isNotEmpty)
            Row(
              children: [
                const Icon(Icons.inventory_2_outlined, size: 14, color: AppColors.inkMuted),
                const SizedBox(width: 6),
                Text(
                  trip.formattedCargoSummary,
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w500,
                    color: AppColors.inkMuted,
                  ),
                ),
              ],
            ),
        ],
      ),
    );
  }

  Widget _buildPickupProofPolicyCard(TripResponse trip) {
    final hasProof = trip.hasPickupProof;

    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceMd),
      decoration: BoxDecoration(
        color: hasProof ? AppColors.statusSuccessBg : AppColors.statusInTransitBg,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        border: Border.all(
          color: hasProof
              ? AppColors.statusSuccessFg.withValues(alpha: 0.2)
              : AppColors.statusInTransitFg.withValues(alpha: 0.3),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            hasProof ? Icons.check_circle_rounded : Icons.info_outline_rounded,
            size: 20,
            color: hasProof ? AppColors.statusSuccessFg : AppColors.statusInTransitFg,
          ),
          const SizedBox(width: AppConstants.spaceMd),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  hasProof ? 'Proof of Pickup Verified' : 'Pickup Verification Pending',
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: hasProof ? AppColors.statusSuccessFg : AppColors.statusInTransitFg,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  hasProof
                      ? 'Photo evidence was captured and validated by the agency dispatcher. This trip is cleared for departure.'
                      : 'Per FreightLink policy (Y3S01-74), photo evidence must be captured by agency staff before this trip can advance.',
                  style: TextStyle(
                    fontSize: 12,
                    color: hasProof
                        ? AppColors.statusSuccessFg.withValues(alpha: 0.85)
                        : AppColors.statusInTransitFg.withValues(alpha: 0.85),
                    height: 1.3,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildActionCard(TripResponse trip) {
    if (trip.isAssigned) {
      return Container(
        key: const Key('assigned_status_card'),
        padding: const EdgeInsets.all(AppConstants.spaceLg),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(AppConstants.radiusMd),
          border: Border.all(color: AppColors.border),
        ),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: AppColors.statusMatchedBg,
                shape: BoxShape.circle,
              ),
              child: const Icon(
                Icons.hourglass_top_rounded,
                color: AppColors.statusMatchedFg,
                size: 20,
              ),
            ),
            const SizedBox(width: AppConstants.spaceMd),
            const Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Awaiting Pickup Verification',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w700,
                      color: AppColors.ink,
                    ),
                  ),
                  SizedBox(height: 2),
                  Text(
                    'Trip finalized post-approval. Waiting for agency dispatcher to confirm cargo loading and record pickup photo proof.',
                    style: TextStyle(
                      fontSize: 12,
                      color: AppColors.inkMuted,
                      height: 1.3,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      );
    }

    if (trip.isPickedUp) {
      return PrimaryButton(
        key: const Key('start_transit_button'),
        label: 'Start Transit (Depart)',
        icon: Icons.navigation_rounded,
        isLoading: _isAdvancing,
        onPressed: _startTransit,
      );
    }

    if (trip.isInTransit) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            padding: const EdgeInsets.all(AppConstants.spaceLg),
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.08),
              borderRadius: BorderRadius.circular(AppConstants.radiusMd),
              border: Border.all(color: AppColors.primary.withValues(alpha: 0.3)),
            ),
            child: const Row(
              children: [
                Icon(Icons.directions_bus_rounded, color: AppColors.primary, size: 24),
                SizedBox(width: AppConstants.spaceMd),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Trip In Transit',
                        style: TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.w700,
                          color: AppColors.primary,
                        ),
                      ),
                      SizedBox(height: 2),
                      Text(
                        'Drive safely. Prepare for delivery proof capture upon arrival at destination.',
                        style: TextStyle(
                          fontSize: 12,
                          color: AppColors.inkMuted,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: AppConstants.spaceLg),
          PrimaryButton(
            key: const Key('capture_delivery_proof_button'),
            label: 'Complete Delivery & Capture Proof',
            icon: Icons.camera_alt_rounded,
            onPressed: () async {
              await Navigator.of(context).push(
                MaterialPageRoute<void>(
                  builder: (_) => ProofOfDeliveryScreen(
                    trip: trip,
                    onDeliveryConfirmed: (updated) {
                      setState(() => _trip = updated);
                    },
                  ),
                ),
              );
              _loadTrip();
            },
          ),
        ],
      );
    }

    if (trip.isDelivered) {
      return Container(
        padding: const EdgeInsets.all(AppConstants.spaceLg),
        decoration: BoxDecoration(
          color: AppColors.statusSuccessBg,
          borderRadius: BorderRadius.circular(AppConstants.radiusMd),
          border: Border.all(color: AppColors.statusSuccessFg.withValues(alpha: 0.3)),
        ),
        child: const Row(
          children: [
            Icon(Icons.verified_rounded, color: AppColors.statusSuccessFg, size: 28),
            SizedBox(width: AppConstants.spaceMd),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Trip Delivered & Complete',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w700,
                      color: AppColors.statusSuccessFg,
                    ),
                  ),
                  SizedBox(height: 2),
                  Text(
                    'Proof of delivery has been submitted and verified.',
                    style: TextStyle(
                      fontSize: 12,
                      color: AppColors.inkMuted,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      );
    }

    return const SizedBox.shrink();
  }
}
