import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/primary_button.dart';

/// A center-pin map picker ("drag the map, the pin stays centered") used by
/// Post a Load / Edit Load to turn a free-text address into the
/// `pickupLat/Lng`/`dropoffLat/Lng` the API requires — see the
/// implementation plan's note on reconciling the mockups' text-only route
/// fields with the backend's coordinate-based DTOs.
class LocationPickerScreen extends StatefulWidget {
  const LocationPickerScreen({
    super.key,
    required this.title,
    this.initialPoint,
  });

  final String title;
  final LatLng? initialPoint;

  static const _defaultCenter = LatLng(6.9271, 79.8612); // Colombo, LK

  @override
  State<LocationPickerScreen> createState() => _LocationPickerScreenState();
}

class _LocationPickerScreenState extends State<LocationPickerScreen> {
  late LatLng _center =
      widget.initialPoint ?? LocationPickerScreen._defaultCenter;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppTopBar(title: widget.title, showBackButton: true),
      body: Stack(
        alignment: Alignment.center,
        children: [
          FlutterMap(
            options: MapOptions(
              initialCenter: _center,
              initialZoom: 12,
              onPositionChanged: (position, _) {
                setState(() => _center = position.center);
              },
            ),
            children: [
              TileLayer(
                urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                userAgentPackageName: 'com.freightlink.mobile',
              ),
            ],
          ),
          const Icon(
            Icons.location_on_rounded,
            size: 44,
            color: AppColors.primary,
          ),
          Positioned(
            left: AppConstants.spaceLg,
            right: AppConstants.spaceLg,
            bottom: AppConstants.spaceLg,
            child: SafeArea(
              child: Container(
                padding: const EdgeInsets.all(AppConstants.spaceLg),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                  border: Border.all(color: AppColors.border),
                ),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '${_center.latitude.toStringAsFixed(5)}, ${_center.longitude.toStringAsFixed(5)}',
                      style: const TextStyle(
                        color: AppColors.inkMuted,
                        fontSize: 12,
                      ),
                    ),
                    const SizedBox(height: AppConstants.spaceSm),
                    PrimaryButton(
                      label: 'Use this location',
                      icon: Icons.check_rounded,
                      onPressed: () => Navigator.of(context).pop(_center),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
