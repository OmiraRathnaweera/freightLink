import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/network/api_exception.dart';
import 'package:freightlink_mobile/features/disputes/models/dispute.dart';
import 'package:freightlink_mobile/features/disputes/providers/dispute_provider.dart';
import 'package:mocktail/mocktail.dart';

import '../../../helpers/fakes.dart';

final _dispute = Dispute(
  disputeId: 'dispute-1',
  tripId: 'trip-1',
  raisedByUserId: 'shipper-1',
  category: DisputeCategory.damage,
  description: 'Cargo arrived with documented water damage.',
  status: DisputeStatus.raised,
  createdAt: DateTime.utc(2026, 9, 27),
);

void main() {
  late MockDisputeRepository repository;

  setUpAll(() => registerFallbackValue(DisputeStatus.raised));

  setUp(() => repository = MockDisputeRepository());

  test('loads status-filtered disputes into the claimant list state', () async {
    when(
      () => repository.getMyDisputes(status: any(named: 'status')),
    ).thenAnswer((_) async => [_dispute]);
    final provider = DisputeListProvider(repository);

    await provider.setStatusFilter(DisputeStatus.raised);

    expect(provider.state, DisputeListState.loaded);
    expect(provider.statusFilter, DisputeStatus.raised);
    expect(provider.items.single.disputeId, 'dispute-1');
    verify(
      () => repository.getMyDisputes(status: DisputeStatus.raised),
    ).called(1);
  });

  test(
    'keeps a typed API error for a retryable claimant-list failure',
    () async {
      const error = ApiException(
        statusCode: 500,
        code: 'INTERNAL_ERROR',
        message: 'Could not load disputes.',
      );
      when(
        () => repository.getMyDisputes(status: any(named: 'status')),
      ).thenThrow(error);
      final provider = DisputeListProvider(repository);

      await provider.load();

      expect(provider.state, DisputeListState.error);
      expect(provider.error, error);
    },
  );
}
