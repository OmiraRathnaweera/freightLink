import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:latlong2/latlong.dart';

/// Reverse-geocodes a map point into a human-readable address via
/// [Nominatim](https://nominatim.org), OpenStreetMap's free geocoding
/// service — the same OSM ecosystem already used for the map tiles
/// (`RouteMapPreview`/`LocationPickerScreen`), so no new API key or account
/// is needed.
///
/// Nominatim's usage policy (https://operations.osmfoundation.org/policies/nominatim/)
/// requires an identifying `User-Agent` and caps public-instance traffic at
/// ~1 request/second — fine for a user manually picking one location at a
/// time, but this should move to a self-hosted/paid instance (with a real
/// contact address in the User-Agent) before any high-volume production use.
class NominatimClient {
  NominatimClient({http.Client? httpClient})
    : _http = httpClient ?? http.Client();

  static const _baseUrl = 'https://nominatim.openstreetmap.org';
  static const _userAgent = 'FreightLinkMobile/1.0';

  final http.Client _http;

  /// Returns the best human-readable address for [point], or null if it
  /// can't be resolved (network failure, no result, etc.) — callers should
  /// fall back to showing coordinates rather than blocking on this.
  Future<String?> reverseGeocode(LatLng point) async {
    final uri = Uri.parse('$_baseUrl/reverse').replace(
      queryParameters: {
        'format': 'jsonv2',
        'lat': point.latitude.toString(),
        'lon': point.longitude.toString(),
        'zoom': '18',
        'addressdetails': '0',
      },
    );

    try {
      final response = await _http
          .get(uri, headers: {'User-Agent': _userAgent})
          .timeout(const Duration(seconds: 8));
      if (response.statusCode != 200) return null;

      final decoded = jsonDecode(response.body);
      if (decoded is! Map<String, dynamic>) return null;
      return decoded['display_name'] as String?;
    } on Exception {
      return null;
    }
  }
}
