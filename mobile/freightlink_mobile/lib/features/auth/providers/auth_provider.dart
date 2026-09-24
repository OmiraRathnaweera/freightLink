import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

import '../../../core/network/api_client.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/storage/token_storage.dart';
import '../models/agency_lookup.dart';
import '../models/auth_user.dart';

enum AuthStatus {
  /// Bootstrapping: checking for a stored token before deciding.
  unknown,
  authenticated,
  guest,
}

/// Owns the signed-in session: the access token, the current [AuthUser], and
/// the single [ApiClient] instance the rest of the app's repositories are
/// built on (so every request shares the same token/401 handling).
///
/// Login/registration screens weren't in the supplied mockups, but are
/// required scaffolding: every Loads endpoint needs a JWT, and none of this
/// existed before.
class AuthProvider extends ChangeNotifier {
  /// [httpClient] is a test seam only — production always lets [ApiClient]
  /// build its own `http.Client`. Passing one in (e.g. a `MockClient` from
  /// `package:http/testing.dart`) makes `login()`/`bootstrap()` testable
  /// without a real backend.
  AuthProvider({TokenStorage? tokenStorage, http.Client? httpClient})
    : _tokenStorage = tokenStorage ?? TokenStorage() {
    _apiClient = ApiClient(
      httpClient: httpClient,
      authToken: () => _accessToken,
      onUnauthorized: _handleUnauthorized,
    );
  }

  final TokenStorage _tokenStorage;
  late final ApiClient _apiClient;

  AuthStatus _status = AuthStatus.unknown;
  AuthUser? _user;
  String? _accessToken;
  bool _isSubmitting = false;
  String? _errorMessage;

  AuthStatus get status => _status;
  AuthUser? get user => _user;
  bool get isSubmitting => _isSubmitting;
  String? get errorMessage => _errorMessage;
  ApiClient get apiClient => _apiClient;

  /// Reads any previously stored token and validates it against `/auth/me`.
  /// Called once at app startup.
  Future<void> bootstrap() async {
    final storedToken = await _tokenStorage.readAccessToken();
    if (storedToken == null) {
      _status = AuthStatus.guest;
      notifyListeners();
      return;
    }

    _accessToken = storedToken;
    try {
      final user = await _fetchCurrentUser();
      if (user.isAdmin) {
        // Admin accounts don't get a mobile session — see the note on
        // login() below. A previously-stored admin token (from before this
        // restriction existed) is discarded rather than honored.
        await _tokenStorage.clear();
        _accessToken = null;
        _status = AuthStatus.guest;
      } else {
        _user = user;
        _status = AuthStatus.authenticated;
      }
    } on ApiException {
      await _tokenStorage.clear();
      _accessToken = null;
      _status = AuthStatus.guest;
    }
    notifyListeners();
  }

  Future<bool> login({required String email, required String password}) async {
    _isSubmitting = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final response =
          await _apiClient.post(
                '/auth/login',
                body: {'email': email, 'password': password},
              )
              as Map<String, dynamic>;
      _accessToken = response['accessToken'] as String;
      await _tokenStorage.saveAccessToken(_accessToken!);
      final user = await _fetchCurrentUser();
      if (user.isAdmin) {
        // Admin accounts manage the platform from the web portal, not this
        // app — reject the session rather than letting an Admin land in a
        // Shipper-shaped UI with no Admin flows behind it.
        await _tokenStorage.clear();
        _accessToken = null;
        _errorMessage =
            'Admin accounts can\'t sign in to the mobile app. '
            'Please use the web portal instead.';
        return false;
      }
      _user = user;
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

  void clearErrorMessage() {
    _errorMessage = null;
    notifyListeners();
  }

  /// Fetches the list of active agencies for driver registration selection.
  Future<List<AgencyLookup>> fetchAgencies() async {
    try {
      final response = await _apiClient.get('/auth/agencies') as List<dynamic>;
      return response
          .map((item) => AgencyLookup.fromJson(item as Map<String, dynamic>))
          .toList();
    } on ApiException catch (error) {
      _errorMessage = error.message;
      notifyListeners();
      rethrow;
    }
  }

  /// Self-service driver registration from the mobile app.
  Future<bool> registerDriver({
    required String email,
    required String password,
    required String fullName,
    required String? phoneE164,
    required String licenceNo,
    required String licenceExpiry,
    required String agencyId,
  }) async {
    _isSubmitting = true;
    _errorMessage = null;
    notifyListeners();

    try {
      await _apiClient.post(
        '/auth/register/driver',
        body: {
          'email': email,
          'password': password,
          'fullName': fullName,
          if (phoneE164 != null && phoneE164.isNotEmpty) 'phoneE164': phoneE164,
          'licenceNo': licenceNo,
          'licenceExpiry': licenceExpiry,
          'agencyId': agencyId,
        },
      );
      return true;
    } on ApiException catch (error) {
      _errorMessage = error.message;
      return false;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }

  Future<void> logout() async {
    _accessToken = null;
    _user = null;
    _status = AuthStatus.guest;
    await _tokenStorage.clear();
    notifyListeners();
  }

  Future<AuthUser> _fetchCurrentUser() async {
    final response = await _apiClient.get('/auth/me') as Map<String, dynamic>;
    return AuthUser.fromJson(response);
  }

  void _handleUnauthorized() {
    if (_status != AuthStatus.authenticated) return;
    // Fire-and-forget: clears the stored token and flips to guest so the
    // root widget routes back to the login screen.
    logout();
  }
}
