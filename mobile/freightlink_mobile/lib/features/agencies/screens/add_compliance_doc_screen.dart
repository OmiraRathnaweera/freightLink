import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:file_picker/file_picker.dart';
import 'package:intl/intl.dart';

import '../providers/compliance_docs_provider.dart';
import '../data/agencies_repository.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../constants/app_constants.dart';

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
  PlatformFile? _selectedFile;

  @override
  void dispose() {
    _docNumberController.dispose();
    _issuedOnController.dispose();
    super.dispose();
  }

  Future<void> _pickFile() async {
    final result = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowedExtensions: ['pdf', 'png', 'jpg', 'jpeg'],
      withData: true,
    );

    if (result != null) {
      setState(() {
        _selectedFile = result.files.single;
      });
    }
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
    if (!_formKey.currentState!.validate()) return;
    if (_selectedFile == null || _selectedFile!.bytes == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please select a file to upload.')),
      );
      return;
    }

    final success = await provider.uploadDoc(
      _selectedDocType,
      _docNumberController.text.trim(),
      _issuedOnController.text.trim(),
      _selectedFile!.bytes!,
      _selectedFile!.name,
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
                  validator: (val) => val == null || val.isEmpty ? 'Required' : null,
                ),
                const SizedBox(height: AppConstants.spaceLg),
                InkWell(
                  onTap: _pickDate,
                  child: IgnorePointer(
                    child: AppTextField(
                      label: 'Issued / Effective Date',
                      controller: _issuedOnController,
                      prefixIcon: Icons.calendar_today_outlined,
                      validator: (val) => val == null || val.isEmpty ? 'Required' : null,
                    ),
                  ),
                ),
                const SizedBox(height: AppConstants.spaceLg),
                OutlinedButton.icon(
                  onPressed: _pickFile,
                  icon: const Icon(Icons.upload_file),
                  label: Text(_selectedFile == null ? 'Select File' : _selectedFile!.name),
                  style: OutlinedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 16),
                  ),
                ),
                const SizedBox(height: AppConstants.spaceXxl),
                if (provider.isUploading)
                  const Center(child: CircularProgressIndicator())
                else
                  AppButton(
                    onPressed: () => _submit(provider),
                    text: 'Upload Document',
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
