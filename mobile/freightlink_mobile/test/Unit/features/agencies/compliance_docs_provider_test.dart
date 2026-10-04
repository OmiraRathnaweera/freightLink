import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/features/agencies/data/agencies_repository.dart';
import 'package:freightlink_mobile/features/agencies/providers/compliance_docs_provider.dart';
import 'package:mocktail/mocktail.dart';

class _MockRepo extends Mock implements AgenciesRepository {}

void main() {
  late _MockRepo repo;
  late ComplianceDocsProvider provider;

  setUp(() {
    repo = _MockRepo();
    provider = ComplianceDocsProvider(repo);
  });

  void stubDocs(List<Map<String, dynamic>> docs) {
    when(() => repo.getComplianceDocs()).thenAnswer((_) async => docs);
  }

  test('Pending and Verified documents lock their type; Rejected and Expired do not', () async {
    stubDocs([
      {'docType': 'BusinessRegistration', 'status': 'Verified'},
      {'docType': 'VehicleInsurance', 'status': 'Pending'},
      {'docType': 'RevenueLicence', 'status': 'Rejected'},
      {'docType': 'GoodsTransportPermit', 'status': 'Expired'},
    ]);

    await provider.loadDocs();

    expect(provider.lockedDocTypes, {
      'BusinessRegistration': 'Verified',
      'VehicleInsurance': 'Pending',
    });
    expect(provider.isDocTypeLocked('RevenueLicence'), isFalse);
    expect(provider.isDocTypeLocked('Other'), isFalse);
  });

  test('uploadDoc refuses a locked type without calling the API', () async {
    stubDocs([
      {'docType': 'BusinessRegistration', 'status': 'Verified'},
    ]);
    await provider.loadDocs();

    final ok = await provider.uploadDoc('BusinessRegistration', 'BR-1', '2026-01-01', [1, 2], 'a.pdf');

    expect(ok, isFalse);
    expect(provider.errorMessage, contains('verified by an administrator'));
    verifyNever(() => repo.uploadFile(any(), any()));
    verifyNever(() => repo.addComplianceDoc(any()));
  });

  test('uploadDoc allows a type whose previous document was rejected', () async {
    stubDocs([
      {'docType': 'BusinessRegistration', 'status': 'Rejected'},
    ]);
    await provider.loadDocs();
    when(() => repo.uploadFile(any(), any())).thenAnswer((_) async => 'public-id');
    when(() => repo.addComplianceDoc(any())).thenAnswer((_) async {});

    final ok = await provider.uploadDoc('BusinessRegistration', 'BR-2', '2026-02-01', [1], 'b.pdf');

    expect(ok, isTrue);
    verify(() => repo.addComplianceDoc(any())).called(1);
  });
}
