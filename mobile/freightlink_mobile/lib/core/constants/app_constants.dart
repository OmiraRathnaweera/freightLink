import 'dart:io' show Platform;
import 'package:flutter/foundation.dart' show kIsWeb;

/// App-wide constants: display strings, the API base URL, and the spacing /
/// radius scale used throughout the design system.
class AppConstants {
  AppConstants._();

  static const String appName = String.fromEnvironment(
    'APP_NAME',
    defaultValue: 'FreightLink',
  );

  /// Backend API base URL, including the `/api/v1` prefix.
  ///
  /// Overridable at build/run time with `--dart-define=API_BASE_URL=...` or
  /// `--dart-define-from-file=.env` (see `.env.example`).
  ///
  /// When not explicitly specified:
  /// - On Android (emulator): defaults to `http://10.0.2.2:5159/api/v1`
  ///   (inside Android emulator, `10.0.2.2` maps to the host machine's `127.0.0.1`).
  /// - On Web / Desktop / iOS Simulator: defaults to `http://localhost:5159/api/v1`.
  static String get apiBaseUrl {
    const fromEnv = String.fromEnvironment('API_BASE_URL');
    if (kIsWeb) {
      if (fromEnv.isNotEmpty) {
        // In a browser, 10.0.2.2 is an emulator alias and unreachable; translate to localhost
        return fromEnv.replaceAll('10.0.2.2', 'localhost');
      }
      return 'http://localhost:5159/api/v1';
    }
    if (Platform.isAndroid) {
      if (fromEnv.isNotEmpty) {
        return fromEnv;
      }
      return 'http://10.0.2.2:5159/api/v1';
    }
    if (fromEnv.isNotEmpty) {
      return fromEnv.replaceAll('10.0.2.2', 'localhost');
    }
    return 'http://localhost:5159/api/v1';
  }

  // Spacing scale (logical pixels).
  static const double spaceXs = 4;
  static const double spaceSm = 8;
  static const double spaceMd = 12;
  static const double spaceLg = 16;
  static const double spaceXl = 24;
  static const double spaceXxl = 32;

  // Corner radii.
  static const double radiusSm = 8;
  static const double radiusMd = 12;
  static const double radiusLg = 16;
}
