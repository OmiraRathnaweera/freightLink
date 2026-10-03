import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:file_picker/file_picker.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';

import '../providers/compliance_docs_provider.dart';
import '../data/agencies_repository.dart';
import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/app_validators.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/primary_button.dart';

class AddComplianceDocScreen extends StatelessWidget {
  const AddComplianceDocScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => ComplianceDocsProvider(context.read<AgenciesRepository>())..loadDocs(),
      child: const _AddComplianceDocForm(),
    );
  }
}

class _AddComplianceDocForm extends StatefulWidget {
  const _AddComplianceDocForm();

  @override
  State<_AddComplianceDocForm> createState() => _AddComplianceDocFormState();
}

class _AddComplianceDocFormState extends State<_AddComplianceDocForm> {
  final _docNumberController = TextEditingController();
  final _issuedOnController = TextEditingController();

  static const _docTypes = <String, String>{
    'BusinessRegistration': 'Business Registration',
    'VehicleInsurance': 'Vehicle Insurance',
    'RevenueLicence': 'Revenue Licence',
    'GoodsTransportPermit': 'Goods Transport Permit',
    'Other': 'Other',
  };

  String _selectedDocType = 'BusinessRegistration';

  /// The chosen type, or the first still-uploadable one if the chosen type is locked (a Verified or
  /// Pending document already exists for it), or null when every type is locked.
  String? _effectiveDocType(ComplianceDocsProvider provider) {
    if (!provider.isDocTypeLocked(_selectedDocType)) return _selectedDocType;
    for (final type in _docTypes.keys) {
      if (!provider.isDocTypeLocked(type)) return type;
    }
    return null;
  }

  String _lockedSuffix(String status) => status == 'Verified' ? ' (verified)' : ' (pending)';
  String? _selectedFileName;
  List<int>? _selectedFileBytes;

  bool _hasAttemptedSubmit = false;
  Map<String, String> _fieldErrors = {};

  String? _errorFor(String field) => _hasAttemptedSubmit ? _fieldErrors[field] : null;

  /// Mirrors `ComplianceDocCreateDto`'s DataAnnotations — see
  /// `backend/DTOs/Agency/ComplianceDocCreateDto.cs`.
  Map<String, String> _validate() {
    final errors = <String, String>{};

    final docNumberError = AppValidators.length(_docNumberController.text, 'Document number', max: 100);
    if (docNumberError != null) errors['docNumber'] = docNumberError;

    if (_issuedOnController.text.trim().isEmpty) {
      errors['issuedOn'] = 'Issued/effective date is required.';
    }

    if (_selectedFileName == null || _selectedFileBytes == null) {
      errors['file'] = 'Select a file to upload.';
    }

    return errors;
  }

  void _revalidateIfDirty() {
    if (!_hasAttemptedSubmit) return;
    setState(() => _fieldErrors = _validate());
  }

  @override
  void dispose() {
    _docNumberController.dispose();
    _issuedOnController.dispose();
    super.dispose();
  }

  Future<void> _pickFile() async {
    final result = await FilePicker.pickFiles(
      type: FileType.custom,
      allowedExtensions: ['pdf', 'png', 'jpg', 'jpeg'],
    );

    if (result.isNotEmpty) {
      final file = result.first;
      setState(() {
        _selectedFileName = file.name;
      });
      _selectedFileBytes = await file.readAsBytes();
      _revalidateIfDirty();
    }
  }

  Future<void> _capturePhoto() async {
    final photo = await ImagePicker().pickImage(
      source: ImageSource.camera,
      imageQuality: 85,
    );
    if (photo == null) return;

    final bytes = await photo.readAsBytes();
    if (!mounted) return;
    setState(() {
      _selectedFileName = photo.name;
      _selectedFileBytes = bytes;
    });
    _revalidateIfDirty();
  }

  Future<void> _chooseDocumentSource() async {
    await showModalBottomSheet<void>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.camera_alt_outlined),
              title: const Text('Take photo'),
              onTap: () {
                Navigator.pop(sheetContext);
                _capturePhoto();
              },
            ),
            ListTile(
              leading: const Icon(Icons.folder_open_outlined),
              title: const Text('Choose file'),
              onTap: () {
                Navigator.pop(sheetContext);
                _pickFile();
              },
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime.now(),
      firstDate: DateTime(2000),
      lastDate: DateTime.now().add(const Duration(days: 3650)),
    );
    if (picked != null) {
      setState(() {
        _issuedOnController.text = DateFormat('yyyy-MM-dd').format(picked);
      });
      _revalidateIfDirty();
    }
  }

  Future<void> _submit(ComplianceDocsProvider provider) async {
    final docType = _effectiveDocType(provider);
    if (docType == null) return;
    setState(() {
      _hasAttemptedSubmit = true;
      _fieldErrors = _validate();
    });
    if (_fieldErrors.isNotEmpty) return;

    final success = await provider.uploadDoc(
      docType,
      _docNumberController.text.trim(),
      _issuedOnController.text.trim(),
      _selectedFileBytes!,
      _selectedFileName!,
    );

    if (!mounted) return;

    if (success) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Document uploaded successfully!')),
      );
      context.pop();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(provider.errorMessage ?? 'Upload failed.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ComplianceDocsProvider>();
    final effectiveDocType = _effectiveDocType(provider);
    final allLocked = provider.state == ComplianceDocsState.loaded && effectiveDocType == null;

    return Scaffold(
      appBar: AppTopBar(
        title: 'Add Document',
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.pop(),
        ),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppConstants.spaceXl),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
                if (allLocked) ...[
                  const Text(
                    'Every document type already has a verified or pending document, so there is nothing left to upload.',
                    style: TextStyle(color: AppColors.statusErrorFg),
                  ),
                  const SizedBox(height: AppConstants.spaceLg),
                ],
                DropdownButtonFormField<String>(
                  // Re-created when the effective type changes (e.g. docs finish loading and the
                  // default type turned out to be locked), since initialValue is only read once.
                  key: ValueKey(effectiveDocType),
                  initialValue: effectiveDocType,
                  // Fill the field's width so long labels ellipsize instead of overflowing the row.
                  isExpanded: true,
                  decoration: InputDecoration(
                    labelText: 'Document Type',
                    prefixIcon: const Icon(Icons.description_outlined),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                    ),
                  ),
                  items: [
                    for (final entry in _docTypes.entries)
                      DropdownMenuItem(
                        value: entry.key,
                        enabled: !provider.isDocTypeLocked(entry.key),
                        child: Text(
                          entry.value + (provider.isDocTypeLocked(entry.key) ? _lockedSuffix(provider.lockedDocTypes[entry.key]!) : ''),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                  ],
                  onChanged: (val) {
                    if (val != null) {
                      setState(() {
                        _selectedDocType = val;
                      });
                    }
                  },
                ),
                const SizedBox(height: AppConstants.spaceLg),
                AppTextField(
                  label: 'Document Number',
                  controller: _docNumberController,
                  prefixIcon: Icons.numbers_outlined,
                  maxLength: 100,
                  errorText: _errorFor('docNumber'),
                  onChanged: (_) => _revalidateIfDirty(),
                ),
                const SizedBox(height: AppConstants.spaceLg),
                InkWell(
                  onTap: _pickDate,
                  child: IgnorePointer(
                    child: AppTextField(
                      label: 'Issued / Effective Date',
                      controller: _issuedOnController,
                      prefixIcon: Icons.calendar_today_outlined,
                      hintText: 'Tap to select a date',
                      errorText: _errorFor('issuedOn'),
                    ),
                  ),
                ),
                const SizedBox(height: AppConstants.spaceLg),
                OutlinedButton.icon(
                  onPressed: _chooseDocumentSource,
                  icon: const Icon(Icons.upload_file),
                  label: Text(_selectedFileName == null ? 'Add document' : _selectedFileName!),
                  style: OutlinedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 16),
                  ),
                ),
                if (_errorFor('file') != null) ...[
                  const SizedBox(height: 4),
                  Text(
                    _errorFor('file')!,
                    style: const TextStyle(fontSize: 12, color: AppColors.statusErrorFg),
                  ),
                ],
                const SizedBox(height: AppConstants.spaceXxl),
                if (provider.isUploading)
                  const Center(child: CircularProgressIndicator())
                else
                  PrimaryButton(
                    onPressed: effectiveDocType == null ? null : () => _submit(provider),
                    label: 'Upload Document',
                  ),
              ],
            ),
        ),
      ),
    );
  }
}
