import 'package:flutter/foundation.dart';
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

  Future<void> loadDocs() async {
    _state = ComplianceDocsState.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      _docs = await _repository.getComplianceDocs();
      _state = ComplianceDocsState.loaded;
    } catch (e) {
      _state = ComplianceDocsState.error;
      _errorMessage = e.toString();
    }
    notifyListeners();
  }

  Future<bool> uploadDoc(String docType, String docNumber, String issuedOn, List<int> fileBytes, String fileName) async {
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
    } catch (e) {
      _errorMessage = e.toString();
      _isUploading = false;
      notifyListeners();
      return false;
    }
  }
}
