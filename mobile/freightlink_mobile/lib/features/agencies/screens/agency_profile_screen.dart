import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/primary_button.dart';
import '../data/agencies_repository.dart';
import '../providers/agency_profile_provider.dart';

class AgencyProfileScreen extends StatelessWidget {
  const AgencyProfileScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          AgencyProfileProvider(context.read<AgenciesRepository>())..loadProfile(),
      child: const _AgencyProfileBody(),
    );
  }
}

class _AgencyProfileBody extends StatefulWidget {
  const _AgencyProfileBody();

  @override
  State<_AgencyProfileBody> createState() => _AgencyProfileBodyState();
}

class _AgencyProfileBodyState extends State<_AgencyProfileBody> {
  bool _isEditing = false;
  
  final _nameController = TextEditingController();
  final _businessRegNoController = TextEditingController();
  final _yardAddressController = TextEditingController();

  @override
  void dispose() {
    _nameController.dispose();
    _businessRegNoController.dispose();
    _yardAddressController.dispose();
    super.dispose();
  }

  void _populateFields(Map<String, dynamic> data) {
    _nameController.text = data['name'] ?? '';
    _businessRegNoController.text = data['businessRegNo'] ?? '';
    _yardAddressController.text = data['yardAddress'] ?? '';
  }

  Future<void> _saveProfile(AgencyProfileProvider provider) async {
    final success = await provider.updateProfile({
      'name': _nameController.text.trim(),
      'businessRegNo': _businessRegNoController.text.trim(),
      'yardAddress': _yardAddressController.text.trim(),
    });
    
    if (!mounted) return;
    
    if (success) {
      setState(() {
        _isEditing = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Profile updated successfully!')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<AgencyProfileProvider>();

    if (provider.state == ProfileState.loading) {
      return Scaffold(
        appBar: AppTopBar(
          title: 'Agency Profile',
          leading: IconButton(
            icon: const Icon(Icons.arrow_back),
            onPressed: () => context.go('/dashboard'),
          ),
        ),
        body: const Center(child: CircularProgressIndicator()),
      );
    }

    if (provider.state == ProfileState.error) {
      return Scaffold(
        appBar: AppTopBar(
          title: 'Agency Profile',
          leading: IconButton(
            icon: const Icon(Icons.arrow_back),
            onPressed: () => context.go('/dashboard'),
          ),
        ),
        body: Center(child: Text(provider.errorMessage ?? 'Error loading profile')),
      );
    }

    if (!_isEditing && provider.profileData != null) {
      _populateFields(provider.profileData!);
    }

    return Scaffold(
      appBar: AppTopBar(
        title: 'Agency Profile',
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.go('/dashboard'),
        ),
        actions: [
          if (!_isEditing)
            IconButton(
              icon: const Icon(Icons.edit_outlined),
              onPressed: () => setState(() => _isEditing = true),
            ),
        ],
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppConstants.spaceXl),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              AppTextField(
                label: 'Agency Name',
                controller: _nameController,
                readOnly: !_isEditing,
                prefixIcon: Icons.business_outlined,
              ),
              const SizedBox(height: AppConstants.spaceLg),
              AppTextField(
                label: 'Business Registration No.',
                controller: _businessRegNoController,
                readOnly: !_isEditing,
                prefixIcon: Icons.assignment_outlined,
              ),
              const SizedBox(height: AppConstants.spaceLg),
              AppTextField(
                label: 'Yard Address',
                controller: _yardAddressController,
                readOnly: !_isEditing,
                prefixIcon: Icons.location_on_outlined,
                maxLines: 2,
              ),
              const SizedBox(height: AppConstants.spaceXxl),
              if (_isEditing)
                PrimaryButton(
                  label: 'Save Changes',
                  isLoading: provider.state == ProfileState.saving,
                  onPressed: () => _saveProfile(provider),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
