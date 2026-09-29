import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/features/disputes/models/dispute.dart';

void main() {
  test(
    'parses the backend detail payload including its read-only resolution',
    () {
      final dispute = Dispute.fromJson({
        'disputeId': 'dispute-1',
        'tripId': 'trip-1',
        'raisedByUserId': 'shipper-1',
        'raisedByName': 'Nadeesha Fernando',
        'raisedByRole': 'Shipper',
        'tripRouteSummary': 'Colombo → Kandy',
        'carrierAgencyName': 'Central Express',
        'vehicleRegistrationNo': 'WP CAB-1234',
        'category': 'Delay',
        'description':
            'The carrier arrived many hours after the booked pickup window.',
        'status': 'Resolved',
        'createdAt': '2026-09-27T10:00:00Z',
        'updatedAt': '2026-09-27T12:00:00Z',
        'resolution': {
          'disputeId': 'dispute-1',
          'resolvedByUserId': 'admin-1',
          'outcome': 'PartiallyUpheld',
          'notes':
              'The documented delay is confirmed; a partial credit was approved.',
          'resolvedAt': '2026-09-27T12:00:00Z',
        },
      });

      expect(dispute.category, DisputeCategory.delay);
      expect(dispute.status, DisputeStatus.resolved);
      expect(dispute.tripRouteSummary, 'Colombo → Kandy');
      expect(dispute.resolution?.outcome, DisputeOutcome.partiallyUpheld);
      expect(dispute.resolution?.notes, contains('partial credit'));
    },
  );
}
