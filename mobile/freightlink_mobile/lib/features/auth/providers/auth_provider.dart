import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

import '../../../core/network/api_client.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/storage/token_storage.dart';
import '../models/auth_user.dart';

enum AuthStatus { unknown, authenticated, guest }

/// Owns the common mobile login/session flow. The backend determines the
/// signed-in role through /auth/me; UI login role tabs are presentation only.
class AuthProvider extends ChangeNotifier {
  AuthProvider({TokenStorage? tokenStorage, http.Client? httpClient})
      : _tokenStorage = tokenStorage ?? TokenStorage() {
    _apiClient = ApiClient(
      httpClient: httpClient,
      authToken: () => _accessToken,
      onUnauthorized: _refreshSession,
    );
  }

  final TokenStorage _tokenStorage;
  late final ApiClient _apiClient;

  AuthStatus _status = AuthStatus.unknown;
  AuthUser? _user;
  String? _accessToken;
  String? _refreshToken;
  Future<bool>? _refreshInFlight;
  bool _isSubmitting = false;
  String? _errorMessage;

  AuthStatus get status => _status;
  AuthUser? get user => _user;
  bool get isSubmitting => _isSubmitting;
  String? get errorMessage => _errorMessage;
  ApiClient get apiClient => _apiClient;

  Future<void> bootstrap() async {
    _refreshToken = await _tokenStorage.readRefreshToken();
    if (_refreshToken == null) {
      _status = AuthStatus.guest;
      notifyListeners();
      return;
    }

    if (!await _refreshSession()) return;
    try {
      final resolvedUser = await _fetchCurrentUser();
      if (resolvedUser.isAdmin) {
        await _endSession();
      } else {
        _user = resolvedUser;
        _status = AuthStatus.authenticated;
      }
    } on ApiException {
      await _endSession();
    }
    notifyListeners();
  }

  /// Shared login for Shipper, Agency Staff, and Driver. The returned backend
  /// role, never the visual role tab, controls session routing and access.
  Future<bool> login({required String email, required String password}) async {
    _isSubmitting = true;
    _errorMessage = null;
    notifyListeners();
    try {
      final response = await _apiClient.post(
        '/auth/login',
        body: {'email': email, 'password': password},
        retryOnUnauthorized: false,
      ) as Map<String, dynamic>;
      await _applyTokenPair(response);
      final resolvedUser = await _fetchCurrentUser();
      if (resolvedUser.isAdmin) {
        await _endSession();
        _errorMessage = 'Admin accounts can\'t sign in to the mobile app. Please use the web portal instead.';
        return false;
      }
      _user = resolvedUser;
      _status = AuthStatus.authenticated;
      return true;
    } on ApiException catch (error) {
      _errorMessage = error.message;
      return false;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }

  /// Agency-only mobile registration. A successful registration does not
  /// force login because the backend may require email verification first.
  Future<bool> registerAgency({
    required String email,
    required String password,
    required String fullName,
    String? phoneE164,
    String? jobTitle,
    required String agencyName,
    required String businessRegNo,
    required String yardAddress,
    required double yardLat,
    required double yardLng,
  }) async {
    _isSubmitting = true;
    _errorMessage = null;
    notifyListeners();
    try {
      await _apiClient.post('/auth/register/agency', body: {
        'email': email,
        'password': password,
        'fullName': fullName,
        if (phoneE164 != null && phoneE164.isNotEmpty) 'phoneE164': phoneE164,
        if (jobTitle != null && jobTitle.isNotEmpty) 'jobTitle': jobTitle,
        'agencyName': agencyName,
        'businessRegNo': businessRegNo,
        'yardAddress': yardAddress,
        'yardLat': yardLat,
        'yardLng': yardLng,
      }, retryOnUnauthorized: false);
      return true;
    } on ApiException catch (error) {
      _errorMessage = error.message;
      return false;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }

  void clearErrorMessage() {
    _errorMessage = null;
    notifyListeners();
  }

  Future<void> logout() => _endSession(revokeRemote: true);

  Future<AuthUser> _fetchCurrentUser() async =>
      AuthUser.fromJson(await _apiClient.get('/auth/me') as Map<String, dynamic>);

  Future<bool> _refreshSession() {
    final inFlight = _refreshInFlight;
    if (inFlight != null) return inFlight;
    final token = _refreshToken;
    if (token == null) return Future.value(false);
    final future = _requestRefresh(token);
    _refreshInFlight = future;
    future.whenComplete(() => _refreshInFlight = null);
    return future;
  }

  Future<bool> _requestRefresh(String token) async {
    try {
      final response = await _apiClient.post(
        '/auth/refresh',
        body: {'refreshToken': token},
        retryOnUnauthorized: false,
      ) as Map<String, dynamic>;
      await _applyTokenPair(response);
      return true;
    } catch (_) {
      await _endSession();
      return false;
    }
  }

  Future<void> _applyTokenPair(Map<String, dynamic> response) async {
    final access = response['accessToken'] as String?;
    final refresh = response['refreshToken'] as String?;
    if (access == null || refresh == null) {
      throw const ApiException(
        statusCode: 0,
        code: 'MALFORMED_TOKEN_RESPONSE',
        message: 'The server did not return a valid token pair.',
      );
    }
    _accessToken = access;
    _refreshToken = refresh;
    await _tokenStorage.saveTokenPair(accessToken: access, refreshToken: refresh);
  }

  Future<void> _endSession({bool revokeRemote = false}) async {
    final refresh = _refreshToken;
    if (revokeRemote && refresh != null) {
      try {
        await _apiClient.post('/auth/logout', body: {'refreshToken': refresh}, retryOnUnauthorized: false);
      } catch (_) {
        // Local logout must succeed even when the network does not.
      }
    }
    _accessToken = null;
    _refreshToken = null;
    _user = null;
    _status = AuthStatus.guest;
    await _tokenStorage.clear();
    notifyListeners();
  }
}
