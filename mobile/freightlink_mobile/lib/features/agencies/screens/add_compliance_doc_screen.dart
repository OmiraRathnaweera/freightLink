import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:file_picker/file_picker.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';

import '../providers/compliance_docs_provider.dart';
import '../data/agencies_repository.dart';
import '../../../core/constants/app_constants.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/primary_button.dart';

class AddComplianceDocScreen extends StatelessWidget {
  const AddComplianceDocScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => ComplianceDocsProvider(context.read<AgenciesRepository>()),
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
  final _formKey = GlobalKey<FormState>();
  final _docNumberController = TextEditingController();
  final _issuedOnController = TextEditingController();

  String _selectedDocType = 'BusinessRegistration';
  String? _selectedFileName;
  List<int>? _selectedFileBytes;

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
    }
  }

  Future<void> _submit(ComplianceDocsProvider provider) async {
    if (_docNumberController.text.trim().isEmpty || _issuedOnController.text.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Document number and issue date are required.')),
      );
      return;
    }
    if (_selectedFileName == null || _selectedFileBytes == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please select a file to upload.')),
      );
      return;
    }

    final success = await provider.uploadDoc(
      _selectedDocType,
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
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                DropdownButtonFormField<String>(
                  value: _selectedDocType,
                  decoration: InputDecoration(
                    labelText: 'Document Type',
                    prefixIcon: const Icon(Icons.description_outlined),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                    ),
                  ),
                  items: const [
                    DropdownMenuItem(value: 'BusinessRegistration', child: Text('Business Registration')),
                    DropdownMenuItem(value: 'VehicleInsurance', child: Text('Vehicle Insurance')),
                    DropdownMenuItem(value: 'RevenueLicence', child: Text('Revenue Licence')),
                    DropdownMenuItem(value: 'GoodsTransportPermit', child: Text('Goods Transport Permit')),
                    DropdownMenuItem(value: 'Other', child: Text('Other')),
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
                ),
                const SizedBox(height: AppConstants.spaceLg),
                InkWell(
                  onTap: _pickDate,
                  child: IgnorePointer(
                    child: AppTextField(
                      label: 'Issued / Effective Date',
                      controller: _issuedOnController,
                      prefixIcon: Icons.calendar_today_outlined,
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
                const SizedBox(height: AppConstants.spaceXxl),
                if (provider.isUploading)
                  const Center(child: CircularProgressIndicator())
                else
                  PrimaryButton(
                    onPressed: () => _submit(provider),
                    label: 'Upload Document',
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
