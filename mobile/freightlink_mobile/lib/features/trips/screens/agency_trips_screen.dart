import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
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

/// Agency Staff view of the trips already dispatched by their agency — the
/// mobile counterpart to the web app's Trips list (`TripsPage`/`TripsTable`),
/// showing the same per-trip fields (reference, status, agency, driver,
/// route, created date) so the two surfaces read consistently.
class AgencyTripsScreen extends StatefulWidget {
  const AgencyTripsScreen({super.key});

  @override
  State<AgencyTripsScreen> createState() => _AgencyTripsScreenState();
}

// Mirrors the backend's TripStatus enum values exactly (wire casing).
const _statusFilters = <String?>[null, 'Assigned', 'PickedUp', 'InTransit', 'Delivered', 'Cancelled'];

String _statusFilterLabel(String? status) => status ?? 'All';

class _AgencyTripsScreenState extends State<AgencyTripsScreen> {
  String? _selectedStatus;
  late Future<List<TripResponse>> _trips;

  @override
  void initState() {
    super.initState();
    _trips = _loadTrips();
  }

  Future<List<TripResponse>> _loadTrips() async {
    final page = await context.read<TripsRepository>().getTrips(
      status: _selectedStatus,
      pageSize: 100,
      sortBy: 'createdAt',
      sortDir: 'desc',
    );
    return page.items;
  }

  void _refresh() => setState(() => _trips = _loadTrips());

  void _onStatusSelected(String? status) {
    setState(() {
      _selectedStatus = status;
      _trips = _loadTrips();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppTopBar(
        title: 'Agency Trips',
        subtitle: 'Track dispatched trips across your fleet',
        leading: const AppAvatar(),
        actions: [
          const NotificationBellButton(),
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
                        : 'Could not load trips. Please try again.';
                    return ErrorState(
                      title: 'Failed to Load Trips',
                      message: message,
                      onRetry: _refresh,
                    );
                  }
                  final trips = snapshot.data ?? const [];
                  if (trips.isEmpty) {
                    return EmptyState(
                      icon: Icons.local_shipping_outlined,
                      title: 'No Trips Found',
                      message: _selectedStatus == null
                          ? 'Dispatched trips will appear here once your agency accepts a proposal and assigns a vehicle/driver.'
                          : 'No trips currently in "$_selectedStatus" status.',
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
          for (final status in _statusFilters) ...[
            _buildFilterChip(status),
            const SizedBox(width: AppConstants.spaceSm),
          ],
        ],
      ),
    );
  }

  Widget _buildFilterChip(String? status) {
    final isSelected = _selectedStatus == status;
    return FilterChip(
      label: Text(_statusFilterLabel(status)),
      selected: isSelected,
      onSelected: (_) => _onStatusSelected(status),
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

    return InkWell(
      key: Key('trip_card_${trip.tripId}'),
      onTap: () => context.push('/trips/${trip.tripId}'),
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
                    style: const TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w700,
                      color: AppColors.ink,
                    ),
                  ),
                ),
                const SizedBox(width: AppConstants.spaceSm),
                TripStatusPill(status: trip.status),
              ],
            ),
            const SizedBox(height: AppConstants.spaceSm),
            if (trip.agencyName != null && trip.agencyName!.isNotEmpty)
              _buildInfoRow(Icons.domain_outlined, trip.agencyName!),
            _buildInfoRow(
              Icons.person_outline_rounded,
              trip.driverName ?? 'Driver ${trip.driverId.substring(0, 8)}',
            ),
            _buildInfoRow(
              Icons.route_outlined,
              '${trip.pickupAddress ?? 'Pickup'} → ${trip.dropoffAddress ?? 'Dropoff'}',
              maxLines: 2,
            ),
            const SizedBox(height: AppConstants.spaceXs),
            Row(
              children: [
                const Icon(Icons.schedule_rounded, size: 13, color: AppColors.inkFaint),
                const SizedBox(width: 6),
                Text(
                  AppFormatters.dateTime(trip.createdAt),
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
