import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/empty_state.dart';
import '../../../shared/widgets/error_state.dart';
import '../data/trips_repository.dart';
import '../models/trip_models.dart';
import '../widgets/trip_status_pill.dart';
import 'agency_trip_detail_screen.dart';

/// Which slice of a driver's trips the history screen shows. The Driver Dashboard's KPI cards deep-link
/// here with the matching filter (`/dashboard/trip-history?filter=completed`).
enum DriverTripFilter {
  all('All', null),
  active('Active', null),
  completed('Completed', 'Delivered'),
  cancelled('Cancelled', 'Cancelled');

  const DriverTripFilter(this.label, this.status);

  final String label;

  /// Backend `status` query value for a single-status filter; null when the filter spans several
  /// statuses (or all of them) and is resolved by [DriverTripHistoryScreen] itself.
  final String? status;

  /// Parses the `filter` query parameter, defaulting to [all] for anything unknown.
  static DriverTripFilter fromQuery(String? value) => DriverTripFilter.values.firstWhere(
        (f) => f.name == value,
        orElse: () => DriverTripFilter.all,
      );
}

// "Active" spans every in-progress TripStatus (wire casing mirrors the backend enum).
const _activeStatuses = ['Assigned', 'PickedUp', 'InTransit'];

/// A Driver's full trip history — every trip assigned to them, past and present, filterable by outcome.
/// [DriverAssignedTripScreen] (the "My Trip" tab) only shows the one trip currently in progress, so this
/// is where completed and cancelled rides live. Tapping a trip opens the same read-only trip detail
/// Shipper and Agency Staff use.
class DriverTripHistoryScreen extends StatefulWidget {
  const DriverTripHistoryScreen({super.key, this.initialFilter = DriverTripFilter.all});

  final DriverTripFilter initialFilter;

  @override
  State<DriverTripHistoryScreen> createState() => _DriverTripHistoryScreenState();
}

class _DriverTripHistoryScreenState extends State<DriverTripHistoryScreen> {
  late DriverTripFilter _filter;
  late Future<List<TripResponse>> _trips;

  @override
  void initState() {
    super.initState();
    _filter = widget.initialFilter;
    _trips = _loadTrips();
  }

  Future<List<TripResponse>> _loadTrips() async {
    final repository = context.read<TripsRepository>();

    Future<List<TripResponse>> fetch({String? status}) async {
      // GET /trips is auto-scoped by the backend to the calling Driver's own trips.
      final page = await repository.getTrips(
        status: status,
        pageSize: 100,
        sortBy: 'createdAt',
        sortDir: 'desc',
      );
      return page.items;
    }

    if (_filter != DriverTripFilter.active) {
      return fetch(status: _filter.status);
    }

    final byStatus = await Future.wait(_activeStatuses.map((s) => fetch(status: s)));
    return byStatus.expand((trips) => trips).toList()..sort((a, b) => b.createdAt.compareTo(a.createdAt));
  }

  void _refresh() {
    // Block body: an arrow closure would return the Future, which setState asserts against.
    setState(() {
      _trips = _loadTrips();
    });
  }

  void _onFilterSelected(DriverTripFilter filter) {
    if (filter == _filter) return;
    setState(() {
      _filter = filter;
      _trips = _loadTrips();
    });
  }

  String _emptyMessage() => switch (_filter) {
        DriverTripFilter.all => 'Trips assigned to you will appear here.',
        DriverTripFilter.active => 'You have no trips in progress right now.',
        DriverTripFilter.completed => "You haven't completed any trips yet.",
        DriverTripFilter.cancelled => 'You have no cancelled trips.',
      };

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppTopBar(
        title: 'Trip History',
        subtitle: 'All your trips',
        showBackButton: true,
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            tooltip: 'Refresh',
            onPressed: _refresh,
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            _buildFilterChips(),
            Expanded(
              child: FutureBuilder<List<TripResponse>>(
                future: _trips,
                builder: (context, snapshot) {
                  if (snapshot.connectionState != ConnectionState.done) {
                    return const Center(child: CircularProgressIndicator(color: AppColors.primary));
                  }
                  if (snapshot.hasError) {
                    final message = snapshot.error is ApiException
                        ? (snapshot.error as ApiException).message
                        : 'Could not load your trips. Please try again.';
                    return ErrorState(title: 'Failed to Load Trips', message: message, onRetry: _refresh);
                  }
                  final trips = snapshot.data ?? const [];
                  if (trips.isEmpty) {
                    return EmptyState(
                      icon: Icons.history_rounded,
                      title: 'No Trips Found',
                      message: _emptyMessage(),
                    );
                  }
                  return RefreshIndicator(
                    onRefresh: () async => _refresh(),
                    color: AppColors.primary,
                    child: ListView.separated(
                      padding: const EdgeInsets.all(AppConstants.spaceLg),
                      itemCount: trips.length,
                      separatorBuilder: (_, _) => const SizedBox(height: AppConstants.spaceMd),
                      itemBuilder: (context, index) => _buildTripCard(trips[index]),
                    ),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildFilterChips() {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.symmetric(
        horizontal: AppConstants.spaceLg,
        vertical: AppConstants.spaceSm,
      ),
      child: Row(
        children: [
          for (final filter in DriverTripFilter.values) ...[
            _buildFilterChip(filter),
            const SizedBox(width: AppConstants.spaceSm),
          ],
        ],
      ),
    );
  }

  Widget _buildFilterChip(DriverTripFilter filter) {
    final isSelected = _filter == filter;
    return FilterChip(
      key: Key('trip_filter_${filter.name}'),
      label: Text(filter.label),
      selected: isSelected,
      onSelected: (_) => _onFilterSelected(filter),
      backgroundColor: AppColors.surface,
      selectedColor: AppColors.statusMatchedBg,
      labelStyle: TextStyle(
        fontSize: 13,
        fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
        color: isSelected ? AppColors.statusMatchedFg : AppColors.inkMuted,
      ),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppConstants.radiusSm),
        side: BorderSide(color: isSelected ? AppColors.statusMatchedFg : AppColors.border),
      ),
      showCheckmark: false,
    );
  }

  Widget _buildTripCard(TripResponse trip) {
    final tripCode = trip.referenceCode ?? 'Trip ${trip.tripId.substring(0, 8).toUpperCase()}';
    // A finished trip's last update is when it was delivered/cancelled; otherwise show when it was assigned.
    final isFinished = trip.status == 'Delivered' || trip.status == 'Cancelled';
    final date = isFinished ? (trip.updatedAt ?? trip.createdAt) : trip.createdAt;

    return InkWell(
      key: Key('driver_trip_card_${trip.tripId}'),
      onTap: () => Navigator.of(context).push(
        MaterialPageRoute(builder: (_) => AgencyTripDetailScreen(tripId: trip.tripId)),
      ),
      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      child: Container(
        padding: const EdgeInsets.all(AppConstants.spaceLg),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(AppConstants.radiusMd),
          border: Border.all(color: AppColors.border),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    tripCode,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: AppColors.ink),
                  ),
                ),
                const SizedBox(width: AppConstants.spaceSm),
                TripStatusPill(status: trip.status),
              ],
            ),
            const SizedBox(height: AppConstants.spaceSm),
            _buildInfoRow(
              Icons.route_outlined,
              '${trip.pickupAddress ?? 'Pickup'} → ${trip.dropoffAddress ?? 'Dropoff'}',
              maxLines: 2,
            ),
            if (trip.cargoDescription != null && trip.cargoDescription!.isNotEmpty)
              _buildInfoRow(Icons.inventory_2_outlined, trip.cargoDescription!),
            if (trip.vehicleRegistrationNo != null && trip.vehicleRegistrationNo!.isNotEmpty)
              _buildInfoRow(Icons.local_shipping_outlined, trip.vehicleRegistrationNo!),
            const SizedBox(height: AppConstants.spaceXs),
            Row(
              children: [
                const Icon(Icons.schedule_rounded, size: 13, color: AppColors.inkFaint),
                const SizedBox(width: 6),
                Text(
                  AppFormatters.dateTime(date),
                  style: const TextStyle(fontSize: 12, color: AppColors.inkFaint),
                ),
                const Spacer(),
                const Icon(Icons.chevron_right_rounded, size: 18, color: AppColors.inkFaint),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildInfoRow(IconData icon, String text, {int maxLines = 1}) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppConstants.spaceXs),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 14, color: AppColors.inkMuted),
          const SizedBox(width: 6),
          Expanded(
            child: Text(
              text,
              maxLines: maxLines,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontSize: 13, color: AppColors.inkMuted),
            ),
          ),
        ],
      ),
    );
  }
}
