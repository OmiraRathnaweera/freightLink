import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/features/auth/models/agency_lookup.dart';

void main() {
  group('AgencyLookup', () {
    test('fromJson parses agencyId and name correctly', () {
      final json = {
        'agencyId': 'b7d8d21b-4f9e-4c7b-b5d1-6789abcdef01',
        'name': 'Speedy Logistics',
      };

      final agency = AgencyLookup.fromJson(json);

      expect(agency.agencyId, 'b7d8d21b-4f9e-4c7b-b5d1-6789abcdef01');
      expect(agency.name, 'Speedy Logistics');
      expect(agency.toString(), 'Speedy Logistics');
    });

    test('equality and hashCode are based on agencyId', () {
      const a1 = AgencyLookup(
        agencyId: 'agency-1',
        name: 'Agency One',
      );
      const a2 = AgencyLookup(
        agencyId: 'agency-1',
        name: 'Agency One (Different Name)',
      );
      const a3 = AgencyLookup(
        agencyId: 'agency-2',
        name: 'Agency One',
      );

      expect(a1, equals(a2));
      expect(a1, isNot(equals(a3)));
      expect(a1.hashCode, equals(a2.hashCode));
    });
  });
}
