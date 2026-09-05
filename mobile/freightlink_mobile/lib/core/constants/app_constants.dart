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
  /// `--dart-define-from-file=.env` (see `.env.example`). Defaults to the
  /// backend's local dev URL (`backend/Properties/launchSettings.json`'s
  /// `http` profile) so `flutter run` works with no extra flags against a
  /// locally running backend.
  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://localhost:5159/api/v1',
  );

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
