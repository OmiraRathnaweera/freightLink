import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/primary_button.dart';
import '../data/agencies_repository.dart';

class AddDriverScreen extends StatefulWidget {
  const AddDriverScreen({super.key});

  @override
  State<AddDriverScreen> createState() => _AddDriverScreenState();
}

class _AddDriverScreenState extends State<AddDriverScreen> {
  final _nameController = TextEditingController();
  final _licenseController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _expiryController = TextEditingController(); // YYYY-MM-DD
  
  bool _isSubmitting = false;

  @override
  void dispose() {
    _nameController.dispose();
    _licenseController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    _expiryController.dispose();
    super.dispose();
  }

  Future<void> _submit(BuildContext context) async {
    final repository = context.read<AgenciesRepository>();
    
    setState(() => _isSubmitting = true);

    try {
      await repository.addDriver({
        'fullName': _nameController.text.trim(),
        'licenceNo': _licenseController.text.trim(),
        'email': _emailController.text.trim(),
        'password': _passwordController.text,
        'licenceExpiry': _expiryController.text.trim(),
      });
      
      if (!context.mounted) return;
      setState(() => _isSubmitting = false);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Driver registered successfully!')),
      );
      context.go('/dashboard/driver-onboarding');
    } catch (e) {
      if (!context.mounted) return;
      setState(() => _isSubmitting = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString())),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppTopBar(
        title: 'Add Driver',
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.go('/dashboard/driver-onboarding'),
        ),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppConstants.spaceXl),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              AppTextField(
                label: 'Driver Full Name',
                controller: _nameController,
                prefixIcon: Icons.person_outline,
              ),
              const SizedBox(height: AppConstants.spaceLg),
              AppTextField(
                label: 'Email Address (for login)',
                controller: _emailController,
                prefixIcon: Icons.email_outlined,
                keyboardType: TextInputType.emailAddress,
              ),
              const SizedBox(height: AppConstants.spaceLg),
              AppTextField(
                label: 'Password (min 6 chars)',
                controller: _passwordController,
                prefixIcon: Icons.lock_outline,
                obscureText: true,
              ),
              const SizedBox(height: AppConstants.spaceLg),
              AppTextField(
                label: 'License Number',
                controller: _licenseController,
                prefixIcon: Icons.badge_outlined,
              ),
              const SizedBox(height: AppConstants.spaceLg),
              AppTextField(
                label: 'License Expiry (YYYY-MM-DD)',
                controller: _expiryController,
                prefixIcon: Icons.calendar_today_outlined,
              ),
              const SizedBox(height: AppConstants.spaceXxl),
              PrimaryButton(
                label: 'Register Driver',
                isLoading: _isSubmitting,
                onPressed: () => _submit(context),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
