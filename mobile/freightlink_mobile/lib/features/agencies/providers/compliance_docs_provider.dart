import 'package:flutter/foundation.dart';
import '../../../core/network/api_exception.dart';
import '../data/agencies_repository.dart';

enum ComplianceDocsState { initial, loading, loaded, error }

class ComplianceDocsProvider extends ChangeNotifier {
  ComplianceDocsProvider(this._repository);

  final AgenciesRepository _repository;

  ComplianceDocsState _state = ComplianceDocsState.initial;
  ComplianceDocsState get state => _state;

  List<Map<String, dynamic>> _docs = [];
  List<Map<String, dynamic>> get docs => _docs;

  String? _errorMessage;
  String? get errorMessage => _errorMessage;

  bool _isUploading = false;
  bool get isUploading => _isUploading;

  /// Document types that already have a live (Pending or Verified) document, mapped to that status.
  /// The server allows one live document per type and freezes a Verified one, and this app has no
  /// replace flow, so such types can't be uploaded again.
  Map<String, String> get lockedDocTypes => {
        for (final doc in _docs)
          if (doc['status'] == 'Pending' || doc['status'] == 'Verified')
            doc['docType'] as String: doc['status'] as String,
      };

  bool isDocTypeLocked(String docType) => lockedDocTypes.containsKey(docType);

  Future<void> loadDocs() async {
    _state = ComplianceDocsState.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      _docs = await _repository.getComplianceDocs();
      _state = ComplianceDocsState.loaded;
    } on ApiException catch (e) {
      _state = ComplianceDocsState.error;
      _errorMessage = e.message;
    } catch (_) {
      _state = ComplianceDocsState.error;
      _errorMessage = 'Failed to load compliance documents. Please try again.';
    }
    notifyListeners();
  }

  Future<bool> uploadDoc(String docType, String docNumber, String issuedOn, List<int> fileBytes, String fileName) async {
    if (isDocTypeLocked(docType)) {
      _errorMessage = lockedDocTypes[docType] == 'Verified'
          ? 'This document has been verified by an administrator and can no longer be changed or replaced.'
          : 'A document of this type is already awaiting review.';
      notifyListeners();
      return false;
    }

    _isUploading = true;
    notifyListeners();

    try {
      // 1. Upload the file to Cloudinary / storage endpoint
      final publicId = await _repository.uploadFile(fileBytes, fileName);
      
      // 2. Submit the doc info referencing the uploaded publicId
      await _repository.addComplianceDoc({
        'publicId': publicId,
        'docType': docType,
        'docNumber': docNumber,
        'issuedOn': issuedOn,
      });
      
      _isUploading = false;
      notifyListeners();
      await loadDocs(); // Reload the list to include the new/updated one
      return true;
    } on ApiException catch (e) {
      _errorMessage = e.message;
      _isUploading = false;
      notifyListeners();
      return false;
    } catch (_) {
      _errorMessage = 'Failed to upload compliance document. Please try again.';
      _isUploading = false;
      notifyListeners();
      return false;
    }
  }
}
