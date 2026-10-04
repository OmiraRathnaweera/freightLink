import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/features/agencies/data/agencies_repository.dart';
import 'package:freightlink_mobile/features/agencies/screens/add_compliance_doc_screen.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';

class _MockRepo extends Mock implements AgenciesRepository {}

void main() {
  testWidgets('long locked labels fit a narrow dropdown without overflowing', (tester) async {
    final repo = _MockRepo();
    when(() => repo.getComplianceDocs()).thenAnswer((_) async => [
          {'docType': 'BusinessRegistration', 'status': 'Verified'},
          {'docType': 'GoodsTransportPermit', 'status': 'Verified'},
        ]);

    // Narrow phone-width viewport — the width at which the dropdown row used to overflow.
    tester.view.physicalSize = const Size(360, 800);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      Provider<AgenciesRepository>.value(
        value: repo,
        child: const MaterialApp(home: AddComplianceDocScreen()),
      ),
    );
    await tester.pumpAndSettle();

    // Open the menu so every (including locked) item is laid out too.
    await tester.tap(find.byType(DropdownButtonFormField<String>));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.textContaining('(verified)'), findsWidgets);
  });
}
