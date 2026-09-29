import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/network/api_exception.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/loads/data/loads_repository.dart';
import 'package:freightlink_mobile/features/loads/providers/load_form_provider.dart';
import 'package:freightlink_mobile/features/loads/screens/load_form_screen.dart';
import 'package:freightlink_mobile/shared/widgets/app_text_field.dart';
import 'package:latlong2/latlong.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';

import '../../../../helpers/fakes.dart';
import '../../../../helpers/finders.dart';
import '../../../../helpers/fixtures.dart';

void main() {
  setUpAll(registerFallbackValues);

  late MockLoadsRepository repository;

  setUp(() {
    repository = MockLoadsRepository();
  });

  /// Fills every field via valid input, reaching the provider directly for
  /// pickup/dropoff/pickup-window — those are set by the map picker and
  /// date/time pickers, which are out of scope for a widget test (see the
  /// plan's "known gaps").
  Future<void> fillValidForm(WidgetTester tester) async {
    await enterAppTextField(tester, 'Cargo Description', 'Palletized goods');
    await enterAppTextField(tester, 'Total Weight (kg)', '1200');
    await enterAppTextField(tester, 'Volume (m³)', '8');

    final form = Provider.of<LoadFormProvider>(
      tester.element(find.widgetWithText(AppTextField, 'Cargo Description')),
      listen: false,
    );
    form
      ..setPickup('Colombo Port, Colombo', const LatLng(6.9344, 79.8428))
      ..setDropoff('Kandy Yard, Kandy', const LatLng(7.2906, 80.6337))
      ..setPickupWindow(DateTime(2026, 6, 1, 9), DateTime(2026, 6, 1, 13));
    await tester.pump();

    // Entering text into the lower fields auto-scrolls them into view,
    // leaving the form scrolled away from the top. Scroll back so the error
    // banner (which appears at the very top of the list on a failed submit)
    // is actually on screen for later assertions.
    await tester.drag(find.byType(ListView), const Offset(0, 600));
    await tester.pumpAndSettle();
  }

  Widget wrap(Widget child) {
    return MultiProvider(
      providers: [Provider<LoadsRepository>.value(value: repository)],
      child: MaterialApp(theme: AppTheme.light, home: child),
    );
  }

  group('Post Load screen', () {
    testWidgets('renders key fields and the submit button', (tester) async {
      await tester.pumpWidget(wrap(const LoadFormScreen()));
      await tester.pumpAndSettle();

      expect(find.text('Post a Load'), findsOneWidget);
      expect(find.text('Pickup Location'), findsOneWidget);
      expect(find.text('Delivery Location'), findsOneWidget);
      expect(find.text('Pickup Window'), findsOneWidget);
      expect(find.text('Cargo Description'), findsOneWidget);
      expect(find.text('Total Weight (kg)'), findsOneWidget);
      expect(find.text('Volume (m³)'), findsOneWidget);
      expect(find.widgetWithText(ElevatedButton, 'Post Load'), findsOneWidget);
    });

    testWidgets('shows validation messages when submitted blank', (
      tester,
    ) async {
      await tester.pumpWidget(wrap(const LoadFormScreen()));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(ElevatedButton, 'Post Load'));
      await tester.pumpAndSettle();

      expect(find.text('Please fill in all required fields.'), findsOneWidget);

      // The Cargo Specifications section sits below the fold once the error
      // banner is added, so it isn't built by the lazy list until scrolled
      // into view.
      await tester.drag(find.byType(ListView), const Offset(0, -600));
      await tester.pumpAndSettle();
      expect(find.text('Cargo description is required.'), findsOneWidget);
      expect(find.text('Weight is required.'), findsOneWidget);
      expect(find.text('Volume is required.'), findsOneWidget);
      verifyNever(() => repository.create(any()));
    });

    testWidgets('valid input submits and pops with the created load', (
      tester,
    ) async {
      when(() => repository.create(any())).thenAnswer((_) async => buildLoad());

      await tester.pumpWidget(
        wrap(
          Builder(
            builder: (context) => Scaffold(
              body: ElevatedButton(
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute(builder: (_) => const LoadFormScreen()),
                ),
                child: const Text('Open form'),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('Open form'));
      await tester.pumpAndSettle();

      await fillValidForm(tester);
      await tester.tap(find.widgetWithText(ElevatedButton, 'Post Load'));
      await tester.pumpAndSettle();

      verify(() => repository.create(any())).called(1);
      // Successful submit pops the form, landing back on the host screen.
      expect(find.text('Open form'), findsOneWidget);
      expect(find.byType(LoadFormScreen), findsNothing);
    });

    testWidgets('shows the server error message on a failed submit', (
      tester,
    ) async {
      when(() => repository.create(any())).thenThrow(
        const ApiException(
          statusCode: 422,
          code: 'VALIDATION_ERROR',
          message: 'That pickup window is no longer available.',
        ),
      );

      await tester.pumpWidget(wrap(const LoadFormScreen()));
      await tester.pumpAndSettle();

      await fillValidForm(tester);
      await tester.tap(find.widgetWithText(ElevatedButton, 'Post Load'));
      await tester.pumpAndSettle();

      expect(
        find.text('That pickup window is no longer available.'),
        findsOneWidget,
      );
      // Loading state resets once the submit fails.
      expect(find.text('Submitting...'), findsNothing);
      expect(find.widgetWithText(ElevatedButton, 'Post Load'), findsOneWidget);
    });
  });
}
