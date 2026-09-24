/// Represents an agency available for selection during driver registration.
class AgencyLookup {
  const AgencyLookup({
    required this.agencyId,
    required this.name,
  });

  factory AgencyLookup.fromJson(Map<String, dynamic> json) {
    return AgencyLookup(
      agencyId: json['agencyId'] as String,
      name: json['name'] as String,
    );
  }

  final String agencyId;
  final String name;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is AgencyLookup &&
          runtimeType == other.runtimeType &&
          agencyId == other.agencyId;

  @override
  int get hashCode => agencyId.hashCode;

  @override
  String toString() => name;
}
