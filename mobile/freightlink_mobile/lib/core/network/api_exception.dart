/// One field-level validation failure, mirroring the backend's
/// `ValidationErrorItemDto` (`{ field, issue }`).
class ApiFieldError {
  const ApiFieldError({required this.field, required this.issue});

  factory ApiFieldError.fromJson(Map<String, dynamic> json) {
    return ApiFieldError(
      field: json['field'] as String? ?? '',
      issue: json['issue'] as String? ?? '',
    );
  }

  final String field;
  final String issue;
}

/// A typed error thrown by [ApiClient] for any non-2xx response, parsed from
/// the backend's standard error envelope: `{ "error": { code, message,
/// details } }` (see `backend/Common/Errors` and
/// `backend/DTOs/Common/ErrorDetailDto.cs`).
class ApiException implements Exception {
  const ApiException({
    required this.statusCode,
    required this.code,
    required this.message,
    this.fieldErrors = const [],
  });

  /// Thrown when the response body isn't the expected JSON error envelope at
  /// all (e.g. the backend is unreachable, or returned an HTML/plaintext
  /// error page).
  factory ApiException.malformed(int statusCode, String body) {
    return ApiException(
      statusCode: statusCode,
      code: 'MALFORMED_RESPONSE',
      message: 'The server returned an unexpected response.',
    );
  }

  /// Thrown when the request never reached the server at all (DNS, refused
  /// connection, timeout).
  factory ApiException.network(Object cause) {
    return ApiException(
      statusCode: 0,
      code: 'NETWORK_ERROR',
      message:
          'Unable to reach the server. Check your connection and try again.',
    );
  }

  final int statusCode;
  final String code;
  final String message;

  /// Field-level validation failures. Only non-empty when [isValidationError].
  final List<ApiFieldError> fieldErrors;

  bool get isUnauthorized => statusCode == 401;
  bool get isNetworkError => code == 'NETWORK_ERROR';
  bool get isValidationError => code == 'VALIDATION_ERROR';

  @override
  String toString() => 'ApiException($statusCode, $code, $message)';
}
