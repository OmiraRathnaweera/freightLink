import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/network/nominatim_client.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/primary_button.dart';

/// What [LocationPickerScreen] hands back: the picked point plus its
/// resolved human-readable address (falls back to a "lat, lng" string if
/// reverse geocoding failed).
typedef PickedLocation = ({LatLng point, String address});

/// A center-pin map picker ("drag the map, the pin stays centered") used by
/// Post a Load / Edit Load to turn a free-text address into the
/// `pickupLat/Lng`/`dropoffLat/Lng` the API requires. Reverse-geocodes the
/// picked point via [NominatimClient] so the form shows a real address, not
/// raw coordinates.
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
  final _geocoder = NominatimClient();

  late LatLng _center =
      widget.initialPoint ?? LocationPickerScreen._defaultCenter;

  /// The resolved address for [_center] — cleared whenever the map moves so
  /// a stale address is never attributed to a new point.
  String? _resolvedAddress;
  bool _isResolving = false;
  Timer? _debounce;

  @override
  void initState() {
    super.initState();
    _resolveAddress();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  void _onMapMoved(LatLng newCenter) {
    setState(() {
      _center = newCenter;
      _resolvedAddress = null;
    });
    // Debounced: reverse-geocoding on every drag frame would blow through
    // Nominatim's ~1 req/sec public-instance rate limit.
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 600), _resolveAddress);
  }

  Future<void> _resolveAddress() async {
    final target = _center;
    setState(() => _isResolving = true);
    final address = await _geocoder.reverseGeocode(target);
    // The map may have moved again while the request was in flight — only
    // apply the result if it still matches the point it was resolved for.
    if (!mounted || target != _center) return;
    setState(() {
      _resolvedAddress = address;
      _isResolving = false;
    });
  }

  Future<void> _confirm() async {
    var address = _resolvedAddress;
    if (address == null) {
      // Resolution hasn't landed yet (still debouncing, or the last attempt
      // failed) — try once more synchronously rather than falling back to
      // coordinates the instant a request happens to still be in flight.
      setState(() => _isResolving = true);
      address = await _geocoder.reverseGeocode(_center);
    }
    if (!mounted) return;
    Navigator.of(
      context,
    ).pop<PickedLocation>((point: _center, address: address ?? _fallbackLabel));
  }

  String get _fallbackLabel =>
      '${_center.latitude.toStringAsFixed(5)}, '
      '${_center.longitude.toStringAsFixed(5)}';

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
              onPositionChanged: (position, hasGesture) {
                if (hasGesture) _onMapMoved(position.center);
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
                    Row(
                      children: [
                        Expanded(
                          child: Text(
                            _isResolving
                                ? 'Resolving address…'
                                : (_resolvedAddress ?? _fallbackLabel),
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(
                              color: _isResolving
                                  ? AppColors.inkFaint
                                  : AppColors.ink,
                              fontSize: 13,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                        if (_isResolving) ...[
                          const SizedBox(width: AppConstants.spaceSm),
                          const SizedBox(
                            width: 14,
                            height: 14,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          ),
                        ],
                      ],
                    ),
                    const SizedBox(height: AppConstants.spaceSm),
                    PrimaryButton(
                      label: 'Use this location',
                      icon: Icons.check_rounded,
                      isLoading: _isResolving && _resolvedAddress == null,
                      onPressed: _confirm,
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
