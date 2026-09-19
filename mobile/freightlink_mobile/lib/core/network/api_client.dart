import 'dart:convert';

import 'package:http/http.dart' as http;

import '../constants/app_constants.dart';
import 'api_exception.dart';

/// Thin wrapper over [http] for calling the FreightLink backend
/// (`backend/Controllers`). Every feature's repository (e.g.
/// `LoadsRepository`) depends on this rather than calling `http` directly,
/// per the project's networking convention.
///
/// Auth is injected rather than read from a global singleton: [authToken]
/// supplies the current bearer token (or null when signed out), and
/// [onUnauthorized] is invoked once whenever a request comes back 401 so
/// `AuthProvider` can clear the session and route back to the login screen.
class ApiClient {
  ApiClient({
    http.Client? httpClient,
    String? baseUrl,
    required this.authToken,
    this.onUnauthorized,
  }) : _http = httpClient ?? http.Client(),
       _baseUrl = baseUrl ?? AppConstants.apiBaseUrl;

  final http.Client _http;
  final String _baseUrl;

  /// Returns the current access token, or null if signed out. A function
  /// rather than a fixed value so the client always sees the latest token
  /// without needing to be re-created on every login/logout.
  final String? Function() authToken;

  final void Function()? onUnauthorized;

  Uri _uri(String path, [Map<String, dynamic>? query]) {
    final normalized = path.startsWith('/') ? path.substring(1) : path;
    final queryParams = query == null
        ? null
        : {
            for (final entry in query.entries)
              if (entry.value != null) entry.key: entry.value.toString(),
          };
    return Uri.parse(
      '$_baseUrl/$normalized',
    ).replace(queryParameters: queryParams);
  }

  Map<String, String> get _headers {
    final token = authToken();
    return {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      if (token != null) 'Authorization': 'Bearer $token',
    };
  }

  Future<dynamic> get(String path, {Map<String, dynamic>? query}) {
    return _send(() => _http.get(_uri(path, query), headers: _headers));
  }

  Future<dynamic> post(String path, {Object? body}) {
    return _send(
      () => _http.post(
        _uri(path),
        headers: _headers,
        body: body == null ? null : jsonEncode(body),
      ),
    );
  }

  Future<dynamic> put(String path, {Object? body}) {
    return _send(
      () => _http.put(
        _uri(path),
        headers: _headers,
        body: body == null ? null : jsonEncode(body),
      ),
    );
  }

  Future<dynamic> patch(String path, {Object? body}) {
    return _send(
      () => _http.patch(
        _uri(path),
        headers: _headers,
        body: body == null ? null : jsonEncode(body),
      ),
    );
  }

  Future<dynamic> postMultipart(
    String path, {
    required List<int> fileBytes,
    required String filename,
    String fieldName = 'file',
    Map<String, String>? fields,
  }) {
    return _send(() async {
      final request = http.MultipartRequest('POST', _uri(path));
      final token = authToken();
      if (token != null) {
        request.headers['Authorization'] = 'Bearer $token';
      }
      request.headers['Accept'] = 'application/json';
      if (fields != null) {
        request.fields.addAll(fields);
      }
      request.files.add(
        http.MultipartFile.fromBytes(
          fieldName,
          fileBytes,
          filename: filename,
        ),
      );

      final streamedResponse = await _http.send(request);
      return http.Response.fromStream(streamedResponse);
    });
  }

  Future<dynamic> _send(Future<http.Response> Function() request) async {
    final http.Response response;
    try {
      response = await request();
    } on Exception catch (cause) {
      throw ApiException.network(cause);
    }

    if (response.statusCode >= 200 && response.statusCode < 300) {
      if (response.body.isEmpty) return null;
      return jsonDecode(response.body);
    }

    if (response.statusCode == 401) {
      onUnauthorized?.call();
    }

    throw _parseError(response);
  }

  ApiException _parseError(http.Response response) {
    try {
      final decoded = jsonDecode(response.body);
      final error = decoded is Map<String, dynamic>
          ? decoded['error'] as Map<String, dynamic>?
          : null;
      if (error == null) {
        return ApiException.malformed(response.statusCode, response.body);
      }
      final rawDetails = error['details'];
      return ApiException(
        statusCode: response.statusCode,
        code: error['code'] as String? ?? 'UNKNOWN_ERROR',
        message: error['message'] as String? ?? 'Something went wrong.',
        fieldErrors: rawDetails is List
            ? rawDetails
                  .whereType<Map<String, dynamic>>()
                  .map(ApiFieldError.fromJson)
                  .toList()
            : const [],
      );
    } on FormatException {
      return ApiException.malformed(response.statusCode, response.body);
    }
  }
}
