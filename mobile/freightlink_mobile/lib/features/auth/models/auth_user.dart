/// The signed-in user's profile, mirroring the backend's
/// `CurrentUserResponseDto` (`GET /api/v1/auth/me`).
class AuthUser {
  const AuthUser({
    required this.userId,
    required this.email,
    required this.fullName,
    required this.role,
    required this.isActive,
  });

  factory AuthUser.fromJson(Map<String, dynamic> json) {
    return AuthUser(
      userId: json['userId'] as String,
      email: json['email'] as String,
      fullName: json['fullName'] as String,
      role: json['role'] as String,
      isActive: json['isActive'] as bool? ?? true,
    );
  }

  final String userId;
  final String email;
  final String fullName;

  /// One of "Shipper", "AgencyStaff", "Driver", "Admin".
  final String role;
  final bool isActive;

  bool get isAdmin => role == 'Admin';
  bool get isShipper => role == 'Shipper';
}
