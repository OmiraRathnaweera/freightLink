import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';

/// The live map card from the "Load Detail — In Transit" mockup — a real
/// interactive map (flutter_map + OpenStreetMap tiles, no API key needed),
/// showing the pickup/dropoff pins and a straight route line between them.
///
/// This is a route *preview*, not live vehicle tracking: the API has no
/// endpoint exposing a driver's current position, so there's no moving
/// marker here — just the two known endpoints.
class RouteMapPreview extends StatelessWidget {
  const RouteMapPreview({
    super.key,
    required this.pickup,
    required this.dropoff,
    this.height = 180,
  });

  final LatLng pickup;
  final LatLng dropoff;
  final double height;

  @override
  Widget build(BuildContext context) {
    final bounds = LatLngBounds.fromPoints([pickup, dropoff]);

    return ClipRRect(
      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      child: SizedBox(
        height: height,
        child: IgnorePointer(
          // A static, non-interactive preview inside a scrolling detail
          // page — avoids the map capturing vertical drag gestures.
          child: FlutterMap(
            options: MapOptions(
              initialCameraFit: CameraFit.bounds(
                bounds: bounds,
                padding: const EdgeInsets.all(36),
              ),
            ),
            children: [
              TileLayer(
                urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                userAgentPackageName: 'com.freightlink.mobile',
              ),
              PolylineLayer(
                polylines: [
                  Polyline(
                    points: [pickup, dropoff],
                    color: AppColors.primary,
                    strokeWidth: 3,
                  ),
                ],
              ),
              MarkerLayer(
                markers: [
                  Marker(
                    point: pickup,
                    child: const Icon(
                      Icons.trip_origin_rounded,
                      color: AppColors.primary,
                      size: 20,
                    ),
                  ),
                  Marker(
                    point: dropoff,
                    child: const Icon(
                      Icons.location_on_rounded,
                      color: AppColors.statusErrorFg,
                      size: 28,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
