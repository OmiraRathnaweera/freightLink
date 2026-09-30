import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/network/api_exception.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/billing/screens/raise_dispute_screen.dart';
import 'package:freightlink_mobile/features/disputes/data/dispute_repository.dart';
import 'package:freightlink_mobile/features/disputes/models/dispute.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';

import '../../../../helpers/fakes.dart';

void main() {
  late MockDisputeRepository repository;

  setUpAll(() => registerFallbackValue(DisputeCategory.damage));

  setUp(() => repository = MockDisputeRepository());

  testWidgets(
    'shows the duplicate-active-dispute message instead of a generic failure',
    (tester) async {
      when(
        () => repository.raiseDispute(
          tripId: any(named: 'tripId'),
          category: any(named: 'category'),
          description: any(named: 'description'),
        ),
      ).thenThrow(
        const ApiException(
          statusCode: 409,
          code: 'DISPUTE_ALREADY_EXISTS_FOR_TRIP_AND_CATEGORY',
          message: 'Backend duplicate message',
        ),
      );

      await tester.pumpWidget(
        Provider<DisputeRepository>.value(
          value: repository,
          child: MaterialApp(
            theme: AppTheme.light,
            home: const RaiseDisputeScreen(tripId: 'trip-1'),
          ),
        ),
      );

      await tester.enterText(
        find.byType(TextFormField),
        'The carrier arrived too late for the scheduled collection.',
      );
      await tester.tap(find.text('Submit dispute'));
      await tester.pump();
      await tester.pump(const Duration(seconds: 1));

      expect(
        find.text(
          'You already have an active dispute in this category for this trip.',
        ),
        findsOneWidget,
      );
      verify(
        () => repository.raiseDispute(
          tripId: 'trip-1',
          category: DisputeCategory.damage,
          description:
              'The carrier arrived too late for the scheduled collection.',
        ),
      ).called(1);
    },
  );
}
