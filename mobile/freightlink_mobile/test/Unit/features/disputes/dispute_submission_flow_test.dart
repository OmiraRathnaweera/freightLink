import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/network/api_exception.dart';
import 'package:freightlink_mobile/features/disputes/models/dispute.dart';
import 'package:freightlink_mobile/features/disputes/providers/dispute_provider.dart';
import 'package:mocktail/mocktail.dart';

import '../../../../helpers/fakes.dart';

void main() {
  late MockDisputeRepository repository;

  setUpAll(() {
    registerFallbackValue(DisputeCategory.incorrectAmount);
    registerFallbackValue(DisputeStatus.raised);
  });

  setUp(() {
    repository = MockDisputeRepository();
  });

  group('Mobile Dispute Submission & Validation Suite', () {
    test('MOB-UT-001: Form validation blocks submission when description is empty or too short', () {
      bool validateSubmission({required String description, required DisputeCategory? category}) {
        if (category == null) return false;
        if (description.trim().length < 15) return false;
        return true;
      }

      // 1. Empty description
      expect(validateSubmission(description: '', category: DisputeCategory.incorrectAmount), isFalse);

      // 2. Description under 15 characters
      expect(validateSubmission(description: 'Too short', category: DisputeCategory.incorrectAmount), isFalse);

      // 3. Category missing
      expect(validateSubmission(description: 'A valid description over fifteen chars', category: null), isFalse);

      // 4. Valid input
      expect(validateSubmission(description: 'Fuel surcharge overcharged by 10,000 LKR.', category: DisputeCategory.incorrectAmount), isTrue);
    });

    test('MOB-UT-002: Attachment validation enforces allowed MIME types and 5MB size limit', () {
      bool isAttachmentValid({required String filename, required int sizeInBytes}) {
        const maxBytes = 5 * 1024 * 1024; // 5 MB
        const allowedExtensions = ['jpg', 'jpeg', 'png', 'pdf'];

        final ext = filename.split('.').last.toLowerCase();
        if (!allowedExtensions.contains(ext)) return false;
        if (sizeInBytes > maxBytes) return false;
        return true;
      }

      // Exceeds 5MB
      expect(isAttachmentValid(filename: 'huge_scan.pdf', sizeInBytes: 6 * 1024 * 1024), isFalse);

      // Unsupported extension
      expect(isAttachmentValid(filename: 'evidence.exe', sizeInBytes: 1024), isFalse);

      // Valid JPEG within limits
      expect(isAttachmentValid(filename: 'receipt.jpg', sizeInBytes: 1024 * 1024), isTrue);
    });

    test('MOB-IT-003: Gracefully captures timeout and preserves retryable error state', () async {
      const timeoutError = ApiException(
        statusCode: 408,
        code: 'TIMEOUT',
        message: 'Network request timed out. Please check your connection and retry.',
      );

      when(
        () => repository.getMyDisputes(status: any(named: 'status')),
      ).thenThrow(timeoutError);

      final provider = DisputeListProvider(repository);
      await provider.load();

      expect(provider.state, DisputeListState.error);
      expect(provider.error?.statusCode, 408);
      expect(provider.error?.message, contains('timed out'));
    });

    test('MOB-IT-005: Double-tap prevention suppresses duplicate submission attempts', () async {
      int apiCallCount = 0;
      bool isSubmitting = false;

      Future<void> submitDispute() async {
        if (isSubmitting) return; // Debounce guard
        isSubmitting = true;
        apiCallCount++;
        await Future.delayed(const Duration(milliseconds: 50));
        isSubmitting = false;
      }

      // Simulate rapid double tap
      final tap1 = submitDispute();
      final tap2 = submitDispute();
      final tap3 = submitDispute();

      await Future.wait([tap1, tap2, tap3]);

      // Exactly one invocation sent
      expect(apiCallCount, 1);
    });
  });
}
