import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Persists the access token in the platform keychain/keystore via
/// `flutter_secure_storage`. Refresh-token rotation is out of scope for this
/// pass (see the Loads implementation plan) — only the access token is kept.
class TokenStorage {
  TokenStorage({FlutterSecureStorage? storage})
    : _storage = storage ?? const FlutterSecureStorage();

  static const _accessTokenKey = 'freightlink.access_token';

  final FlutterSecureStorage _storage;

  Future<String?> readAccessToken() => _storage.read(key: _accessTokenKey);

  Future<void> saveAccessToken(String token) =>
      _storage.write(key: _accessTokenKey, value: token);

  Future<void> clear() => _storage.delete(key: _accessTokenKey);
}
