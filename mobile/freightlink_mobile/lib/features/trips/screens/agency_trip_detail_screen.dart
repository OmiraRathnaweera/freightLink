import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/error_state.dart';
import '../../../shared/widgets/section_card.dart';
import '../../auth/providers/auth_provider.dart';
import '../../billing/screens/create_invoice_screen.dart';
import '../data/trips_repository.dart';
import '../models/trip_event.dart';
import '../models/trip_models.dart';
import '../widgets/trip_status_pill.dart';

/// Full trip detail — the mobile counterpart to the web app's `TripDetailPage`:
/// overview (agency/driver/vehicle/route), a route map, the full status
/// timeline, and captured pickup/delivery evidence photos. Read-only, so it's
/// reused as-is both from Agency Staff's Trips list and from a Shipper's own
/// Load Detail screen once their load has a dispatched trip.
class AgencyTripDetailScreen extends StatefulWidget {
  const AgencyTripDetailScreen({super.key, required this.tripId});

  final String tripId;

  @override
  State<AgencyTripDetailScreen> createState() => _AgencyTripDetailScreenState();
}

class _AgencyTripDetailScreenState extends State<AgencyTripDetailScreen> {
  late Future<TripResponse> _trip;

  @override
  void initState() {
    super.initState();
    _trip = _loadTrip();
  }

  Future<TripResponse> _loadTrip() {
    return context.read<TripsRepository>().getTripById(widget.tripId);
  }

  void _refresh() => setState(() => _trip = _loadTrip());

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppTopBar(
        title: 'Trip Detail',
        showBackButton: true,
        actions: [
          IconButton(icon: const Icon(Icons.refresh_rounded), tooltip: 'Refresh', onPressed: _refresh),
        ],
      ),
      body: SafeArea(
        child: FutureBuilder<TripResponse>(
          future: _trip,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator(color: AppColors.primary));
            }
            if (snapshot.hasError) {
              final message = snapshot.error is ApiException
                  ? (snapshot.error as ApiException).message
                  : 'Could not load this trip. Please try again.';
              return ErrorState(title: 'Failed to Load Trip', message: message, onRetry: _refresh);
            }
            final trip = snapshot.data!;
            return RefreshIndicator(
              onRefresh: () async => _refresh(),
              color: AppColors.primary,
              child: ListView(
                padding: const EdgeInsets.all(AppConstants.spaceLg),
                children: [
                  _buildOverviewCard(trip),
                  if (trip.isDelivered && (context.watch<AuthProvider>().user?.isAgencyStaff ?? false)) ...[
                    const SizedBox(height: AppConstants.spaceLg),
                    _buildCreateInvoiceAction(context, trip),
                  ],
                  if (trip.hasRouteCoordinates) ...[
                    const SizedBox(height: AppConstants.spaceLg),
                    _buildRouteMapCard(trip),
                  ],
                  const SizedBox(height: AppConstants.spaceLg),
                  _buildTimelineCard(trip),
                  const SizedBox(height: AppConstants.spaceLg),
                  _buildEvidenceCard(trip),
                ],
              ),
            );
          },
        ),
      ),
    );
  }

  Widget _buildCreateInvoiceAction(BuildContext context, TripResponse trip) {
    return SectionCard(
      child: Row(
        children: [
          const Icon(Icons.receipt_long_outlined, color: AppColors.primary),
          const SizedBox(width: AppConstants.spaceMd),
          const Expanded(
            child: Text(
              'This trip has been delivered. Create an invoice to bill the shipper.',
              style: TextStyle(fontSize: 13, color: AppColors.ink),
            ),
          ),
          const SizedBox(width: AppConstants.spaceSm),
          FilledButton(
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => CreateInvoiceScreen(tripId: trip.tripId)),
            ),
            child: const Text('Create Invoice'),
          ),
        ],
      ),
    );
  }

  Widget _buildOverviewCard(TripResponse trip) {
    final tripCode = trip.referenceCode ?? 'Trip ${trip.tripId.substring(0, 8).toUpperCase()}';

    return SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  tripCode,
                  style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: AppColors.ink),
                ),
              ),
              const SizedBox(width: AppConstants.spaceSm),
              TripStatusPill(status: trip.status),
            ],
          ),
          const SizedBox(height: AppConstants.spaceLg),
          _buildOverviewRow(Icons.domain_outlined, 'Executing Agency', trip.agencyName ?? 'Unassigned'),
          _buildOverviewRow(
            Icons.person_outline_rounded,
            'Assigned Driver',
            trip.driverName ?? 'Driver ${trip.driverId.substring(0, 8)}',
          ),
          _buildOverviewRow(
            Icons.local_shipping_outlined,
            'Assigned Vehicle',
            trip.vehicleRegistrationNo ?? 'Vehicle ${trip.vehicleId.substring(0, 8)}',
            mono: true,
          ),
          _buildOverviewRow(Icons.schedule_rounded, 'Created', AppFormatters.dateTime(trip.createdAt), mono: true),
          if (trip.pickupAddress != null && trip.dropoffAddress != null)
            _buildOverviewRow(
              Icons.route_outlined,
              'Route',
              '${trip.pickupAddress} → ${trip.dropoffAddress}',
              isLast: true,
            ),
        ],
      ),
    );
  }

  Widget _buildOverviewRow(IconData icon, String label, String value, {bool mono = false, bool isLast = false}) {
    return Padding(
      padding: EdgeInsets.only(bottom: isLast ? 0 : AppConstants.spaceMd),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 16, color: AppColors.inkMuted),
          const SizedBox(width: AppConstants.spaceSm),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label.toUpperCase(),
                  style: const TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    letterSpacing: 0.3,
                    color: AppColors.inkMuted,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  value,
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                    color: AppColors.ink,
                    fontFamily: mono ? 'monospace' : null,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildRouteMapCard(TripResponse trip) {
    final pickup = LatLng(trip.pickupLat!, trip.pickupLng!);
    final dropoff = LatLng(trip.dropoffLat!, trip.dropoffLng!);
    final bounds = LatLngBounds.fromPoints([pickup, dropoff]);

    return SectionCard(
      title: 'Route Map',
      icon: Icons.map_outlined,
      padding: EdgeInsets.zero,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        child: SizedBox(
          height: 220,
          child: FlutterMap(
            options: MapOptions(
              initialCameraFit: CameraFit.bounds(
                bounds: bounds,
                padding: const EdgeInsets.all(36),
              ),
              interactionOptions: const InteractionOptions(flags: InteractiveFlag.pinchZoom | InteractiveFlag.drag),
            ),
            children: [
              TileLayer(
                urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                userAgentPackageName: 'com.freightlink.mobile',
              ),
              PolylineLayer(
                polylines: [
                  Polyline(points: [pickup, dropoff], color: AppColors.primary, strokeWidth: 3),
                ],
              ),
              MarkerLayer(
                markers: [
                  Marker(
                    point: pickup,
                    width: 32,
                    height: 32,
                    child: const Icon(Icons.trip_origin_rounded, color: AppColors.statusSuccessFg, size: 28),
                  ),
                  Marker(
                    point: dropoff,
                    width: 32,
                    height: 32,
                    child: const Icon(Icons.location_on_rounded, color: AppColors.statusErrorFg, size: 32),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildTimelineCard(TripResponse trip) {
    final events = [...trip.events]..sort((a, b) => b.occurredAt.compareTo(a.occurredAt));

    return SectionCard(
      title: 'Status & Timeline',
      icon: Icons.history_rounded,
      child: events.isEmpty
          ? const Text(
              'No status transitions recorded yet.',
              style: TextStyle(fontSize: 13, color: AppColors.inkMuted),
            )
          : Column(
              children: [
                for (var i = 0; i < events.length; i++) _buildTimelineEntry(events[i], isLast: i == events.length - 1),
              ],
            ),
    );
  }

  Widget _buildTimelineEntry(TripEvent event, {required bool isLast}) {
    return Padding(
      padding: EdgeInsets.only(bottom: isLast ? 0 : AppConstants.spaceLg),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Column(
            children: [
              Container(
                width: 12,
                height: 12,
                decoration: const BoxDecoration(color: AppColors.primary, shape: BoxShape.circle),
              ),
              if (!isLast) Container(width: 2, height: 48, color: AppColors.border),
            ],
          ),
          const SizedBox(width: AppConstants.spaceMd),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Wrap(
                  crossAxisAlignment: WrapCrossAlignment.center,
                  spacing: 6,
                  children: [
                    if (event.fromStatus != null) ...[
                      TripStatusPill(status: event.fromStatus!),
                      const Icon(Icons.arrow_forward_rounded, size: 12, color: AppColors.inkFaint),
                    ],
                    TripStatusPill(status: event.toStatus),
                  ],
                ),
                const SizedBox(height: 6),
                Text(
                  AppFormatters.dateTime(event.occurredAt),
                  style: const TextStyle(fontSize: 12, color: AppColors.inkFaint, fontFamily: 'monospace'),
                ),
                if (event.notes != null && event.notes!.isNotEmpty) ...[
                  const SizedBox(height: 6),
                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(AppConstants.spaceSm),
                    decoration: BoxDecoration(
                      color: AppColors.background,
                      borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                      border: Border.all(color: AppColors.border),
                    ),
                    child: Text(event.notes!, style: const TextStyle(fontSize: 13, color: AppColors.ink)),
                  ),
                ],
                if (event.hasSnapshotLocation) ...[
                  const SizedBox(height: 6),
                  _buildGpsChip(event.snapshotLat!, event.snapshotLng!),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildGpsChip(double lat, double lng) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(color: AppColors.background, borderRadius: BorderRadius.circular(AppConstants.radiusSm)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.my_location_rounded, size: 12, color: AppColors.inkMuted),
          const SizedBox(width: 4),
          Text(
            'GPS: ${lat.toStringAsFixed(4)}, ${lng.toStringAsFixed(4)}',
            style: const TextStyle(fontSize: 11, color: AppColors.inkMuted),
          ),
        ],
      ),
    );
  }

  Widget _buildEvidenceCard(TripResponse trip) {
    return SectionCard(
      title: 'Trip Evidence',
      icon: Icons.camera_alt_outlined,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(AppConstants.spaceLg),
        decoration: BoxDecoration(
          color: AppColors.background,
          borderRadius: BorderRadius.circular(AppConstants.radiusMd),
          border: Border.all(color: AppColors.border),
        ),
        child: Column(
          children: [
            const Icon(Icons.desktop_windows_outlined, size: 28, color: AppColors.inkFaint),
            const SizedBox(height: AppConstants.spaceSm),
            const Text(
              'Please use the web application to view proof-of-pickup and proof-of-delivery photos for this trip.',
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 13, color: AppColors.inkMuted),
            ),
          ],
        ),
      ),
    );
  }
}
