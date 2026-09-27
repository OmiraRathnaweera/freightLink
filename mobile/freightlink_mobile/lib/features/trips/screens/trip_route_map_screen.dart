import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:geolocator/geolocator.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/app_top_bar.dart';

/// Full-screen route map for a trip's pickup/dropoff, with the device's own
/// live position shown as a "you are here" navigator marker — an in-app
/// substitute for handing off to Google Maps/Uber-style navigation (which
/// would need a Google Maps API key). Built entirely on the OpenStreetMap
/// stack already used elsewhere in this app (`flutter_map`), not Google Maps.
///
/// Reached by tapping either the pickup or dropoff location on a trip screen
/// (e.g. `DriverAssignedTripScreen`'s route card) — [focus] says which one to
/// center on first; both markers and the current-location puck are always
/// shown regardless.
class TripRouteMapScreen extends StatefulWidget {
  const TripRouteMapScreen({
    super.key,
    required this.pickupLat,
    required this.pickupLng,
    required this.pickupAddress,
    required this.dropoffLat,
    required this.dropoffLng,
    required this.dropoffAddress,
    this.focus,
  });

  final double pickupLat;
  final double pickupLng;
  final String pickupAddress;
  final double dropoffLat;
  final double dropoffLng;
  final String dropoffAddress;

  /// Which point to center on first: `'pickup'`, `'dropoff'`, or `null` for
  /// both (fit-to-bounds).
  final String? focus;

  @override
  State<TripRouteMapScreen> createState() => _TripRouteMapScreenState();
}

class _TripRouteMapScreenState extends State<TripRouteMapScreen> {
  final _mapController = MapController();
  StreamSubscription<Position>? _positionSubscription;
  Position? _currentPosition;
  String? _locationError;

  late final LatLng _pickup = LatLng(widget.pickupLat, widget.pickupLng);
  late final LatLng _dropoff = LatLng(widget.dropoffLat, widget.dropoffLng);

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _fitInitialView());
    _startLocationUpdates();
  }

  @override
  void dispose() {
    _positionSubscription?.cancel();
    super.dispose();
  }

  void _fitInitialView() {
    switch (widget.focus) {
      case 'pickup':
        _mapController.move(_pickup, 15);
      case 'dropoff':
        _mapController.move(_dropoff, 15);
      default:
        _mapController.fitCamera(
          CameraFit.bounds(bounds: LatLngBounds.fromPoints([_pickup, _dropoff]), padding: const EdgeInsets.all(60)),
        );
    }
  }

  Future<void> _startLocationUpdates() async {
    final serviceEnabled = await Geolocator.isLocationServiceEnabled();
    if (!serviceEnabled) {
      if (mounted) setState(() => _locationError = 'Location services are turned off on this device.');
      return;
    }

    var permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) {
      permission = await Geolocator.requestPermission();
    }
    if (permission == LocationPermission.denied || permission == LocationPermission.deniedForever) {
      if (mounted) {
        setState(() => _locationError = 'Location permission denied — showing route only, without your position.');
      }
      return;
    }

    try {
      final position = await Geolocator.getCurrentPosition();
      if (mounted) setState(() => _currentPosition = position);
    } catch (_) {
      // Fall through to the stream below; a transient failure here isn't fatal.
    }

    _positionSubscription = Geolocator.getPositionStream(
      locationSettings: const LocationSettings(accuracy: LocationAccuracy.high, distanceFilter: 10),
    ).listen((position) {
      if (mounted) setState(() => _currentPosition = position);
    });
  }

  void _recenterOnMe() {
    final position = _currentPosition;
    if (position == null) return;
    _mapController.move(LatLng(position.latitude, position.longitude), 16);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: const AppTopBar(title: 'Trip Route', showBackButton: true),
      body: Stack(
        children: [
          FlutterMap(
            mapController: _mapController,
            options: MapOptions(
              initialCenter: _pickup,
              initialZoom: 13,
              interactionOptions: const InteractionOptions(flags: InteractiveFlag.all),
            ),
            children: [
              TileLayer(
                urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                userAgentPackageName: 'com.freightlink.mobile',
              ),
              PolylineLayer(
                polylines: [
                  Polyline(points: [_pickup, _dropoff], color: AppColors.primary, strokeWidth: 3),
                ],
              ),
              MarkerLayer(
                markers: [
                  Marker(
                    point: _pickup,
                    width: 36,
                    height: 36,
                    child: const Icon(Icons.trip_origin_rounded, color: AppColors.statusSuccessFg, size: 30),
                  ),
                  Marker(
                    point: _dropoff,
                    width: 36,
                    height: 36,
                    child: const Icon(Icons.location_on_rounded, color: AppColors.statusErrorFg, size: 36),
                  ),
                  if (_currentPosition != null)
                    Marker(
                      point: LatLng(_currentPosition!.latitude, _currentPosition!.longitude),
                      width: 40,
                      height: 40,
                      child: _NavigatorMarker(headingDegrees: _currentPosition!.heading),
                    ),
                ],
              ),
            ],
          ),
          if (_locationError != null)
            Positioned(
              top: AppConstants.spaceMd,
              left: AppConstants.spaceLg,
              right: AppConstants.spaceLg,
              child: _InfoBanner(message: _locationError!),
            ),
          Positioned(
            right: AppConstants.spaceLg,
            bottom: AppConstants.spaceLg + 140,
            child: FloatingActionButton.small(
              heroTag: 'recenter-on-me',
              backgroundColor: AppColors.surface,
              foregroundColor: AppColors.primary,
              onPressed: _currentPosition == null ? null : _recenterOnMe,
              tooltip: 'Center on my location',
              child: const Icon(Icons.my_location_rounded),
            ),
          ),
          Positioned(
            left: 0,
            right: 0,
            bottom: 0,
            child: _RouteInfoCard(
              pickupAddress: widget.pickupAddress,
              dropoffAddress: widget.dropoffAddress,
              mapController: _mapController,
              pickupPoint: _pickup,
              dropoffPoint: _dropoff,
            ),
          ),
        ],
      ),
    );
  }
}

/// A directional "you are here" puck — a filled blue circle with a
/// navigation arrow rotated to the device's current heading, when available.
class _NavigatorMarker extends StatelessWidget {
  const _NavigatorMarker({required this.headingDegrees});

  final double headingDegrees;

  @override
  Widget build(BuildContext context) {
    return Container(
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: AppColors.statusMatchedFg.withValues(alpha: 0.18),
        shape: BoxShape.circle,
      ),
      child: Container(
        width: 24,
        height: 24,
        decoration: BoxDecoration(
          color: AppColors.statusMatchedFg,
          shape: BoxShape.circle,
          border: Border.all(color: Colors.white, width: 2),
        ),
        child: Transform.rotate(
          angle: headingDegrees.isNaN ? 0 : headingDegrees * 3.1415926535 / 180,
          child: const Icon(Icons.navigation_rounded, color: Colors.white, size: 14),
        ),
      ),
    );
  }
}

class _InfoBanner extends StatelessWidget {
  const _InfoBanner({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: AppConstants.spaceMd, vertical: AppConstants.spaceSm),
      decoration: BoxDecoration(
        color: AppColors.statusInTransitBg,
        borderRadius: BorderRadius.circular(AppConstants.radiusSm),
        border: Border.all(color: AppColors.statusInTransitFg.withValues(alpha: 0.3)),
      ),
      child: Row(
        children: [
          const Icon(Icons.info_outline_rounded, size: 16, color: AppColors.statusInTransitFg),
          const SizedBox(width: AppConstants.spaceSm),
          Expanded(
            child: Text(message, style: const TextStyle(fontSize: 12, color: AppColors.statusInTransitFg)),
          ),
        ],
      ),
    );
  }
}

/// Bottom sheet-style card listing pickup/dropoff addresses; tapping either
/// recenters the map on that point (a lightweight PickMe/Uber-style "trip
/// stops" card).
class _RouteInfoCard extends StatelessWidget {
  const _RouteInfoCard({
    required this.pickupAddress,
    required this.dropoffAddress,
    required this.mapController,
    required this.pickupPoint,
    required this.dropoffPoint,
  });

  final String pickupAddress;
  final String dropoffAddress;
  final MapController mapController;
  final LatLng pickupPoint;
  final LatLng dropoffPoint;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(
        AppConstants.spaceLg,
        AppConstants.spaceLg,
        AppConstants.spaceLg,
        AppConstants.spaceXl,
      ),
      decoration: const BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.vertical(top: Radius.circular(AppConstants.radiusLg)),
        boxShadow: [BoxShadow(color: Color(0x1A000000), blurRadius: 16, offset: Offset(0, -4))],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          _RouteStopRow(
            icon: Icons.trip_origin_rounded,
            iconColor: AppColors.statusSuccessFg,
            label: 'PICKUP',
            address: pickupAddress,
            onTap: () => mapController.move(pickupPoint, 15),
          ),
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 6, horizontal: 18),
            child: SizedBox(
              height: 16,
              child: VerticalDivider(color: AppColors.border, thickness: 2, width: 2),
            ),
          ),
          _RouteStopRow(
            icon: Icons.location_on_rounded,
            iconColor: AppColors.statusErrorFg,
            label: 'DROPOFF',
            address: dropoffAddress,
            onTap: () => mapController.move(dropoffPoint, 15),
          ),
        ],
      ),
    );
  }
}

class _RouteStopRow extends StatelessWidget {
  const _RouteStopRow({
    required this.icon,
    required this.iconColor,
    required this.label,
    required this.address,
    required this.onTap,
  });

  final IconData icon;
  final Color iconColor;
  final String label;
  final String address;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(AppConstants.radiusSm),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: Row(
          children: [
            Icon(icon, size: 22, color: iconColor),
            const SizedBox(width: AppConstants.spaceMd),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    label,
                    style: const TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w700,
                      letterSpacing: 0.4,
                      color: AppColors.inkMuted,
                    ),
                  ),
                  Text(
                    address,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.ink),
                  ),
                ],
              ),
            ),
            const Icon(Icons.center_focus_strong_rounded, size: 18, color: AppColors.inkFaint),
          ],
        ),
      ),
    );
  }
}
