import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/primary_button.dart';
import '../providers/auth_provider.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  // Common fields
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _fullNameController = TextEditingController();
  final _phoneController = TextEditingController();
  bool _obscurePassword = true;
  
  // Agency fields
  final _agencyNameController = TextEditingController();
  final _businessRegNoController = TextEditingController();
  final _yardAddressController = TextEditingController();
  final _jobTitleController = TextEditingController();

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _fullNameController.dispose();
    _phoneController.dispose();
    _agencyNameController.dispose();
    _businessRegNoController.dispose();
    _yardAddressController.dispose();
    _jobTitleController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final auth = context.read<AuthProvider>();
    
    await auth.registerAgency(
      email: _emailController.text.trim(),
      password: _passwordController.text,
      fullName: _fullNameController.text.trim(),
      phoneE164: _phoneController.text.trim(),
      jobTitle: _jobTitleController.text.trim(),
      agencyName: _agencyNameController.text.trim(),
      businessRegNo: _businessRegNoController.text.trim(),
      yardAddress: _yardAddressController.text.trim(),
      yardLat: 34.05, // Defaulting coordinates for quick registration
      yardLng: -118.25,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Register an Agency Account'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.go('/login'),
        ),
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(AppConstants.spaceXl),
            child: Consumer<AuthProvider>(
              builder: (context, auth, _) {
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Text(
                      'Shipper registration is available through the FreightLink web portal.',
                      style: TextStyle(color: AppColors.inkMuted),
                    ),
                    const SizedBox(height: AppConstants.spaceXxl),
                    
                    // Common Fields
                    AppTextField(
                      label: 'Full Name',
                      controller: _fullNameController,
                      hintText: 'John Doe',
                      prefixIcon: Icons.person_outline,
                      textInputAction: TextInputAction.next,
                    ),
                    const SizedBox(height: AppConstants.spaceLg),
                    AppTextField(
                      label: 'Email',
                      controller: _emailController,
                      hintText: 'you@company.com',
                      prefixIcon: Icons.mail_outline_rounded,
                      keyboardType: TextInputType.emailAddress,
                      textInputAction: TextInputAction.next,
                    ),
                    const SizedBox(height: AppConstants.spaceLg),
                    AppTextField(
                      label: 'Password',
                      controller: _passwordController,
                      hintText: '••••••••',
                      prefixIcon: Icons.lock_outline_rounded,
                      obscureText: _obscurePassword,
                      textInputAction: TextInputAction.next,
                      suffixIcon: IconButton(
                        icon: Icon(
                          _obscurePassword
                              ? Icons.visibility_outlined
                              : Icons.visibility_off_outlined,
                          size: 20,
                        ),
                        onPressed: () => setState(
                          () => _obscurePassword = !_obscurePassword,
                        ),
                      ),
                    ),
                    const SizedBox(height: AppConstants.spaceLg),
                    AppTextField(
                      label: 'Phone (E.164)',
                      controller: _phoneController,
                      hintText: '+14155552671',
                      prefixIcon: Icons.phone_outlined,
                      keyboardType: TextInputType.phone,
                      textInputAction: TextInputAction.next,
                    ),
                    const SizedBox(height: AppConstants.spaceLg),

                    // Agency-specific fields. Shipper signup is intentionally web-only.
                    ...[
                      AppTextField(
                        label: 'Agency Name',
                        controller: _agencyNameController,
                        hintText: 'Fast Freight Co.',
                        prefixIcon: Icons.business_outlined,
                        textInputAction: TextInputAction.next,
                      ),
                      const SizedBox(height: AppConstants.spaceLg),
                      AppTextField(
                        label: 'Business Registration No.',
                        controller: _businessRegNoController,
                        hintText: 'REG-12345',
                        prefixIcon: Icons.numbers_outlined,
                        textInputAction: TextInputAction.next,
                      ),
                      const SizedBox(height: AppConstants.spaceLg),
                      AppTextField(
                        label: 'Job Title',
                        controller: _jobTitleController,
                        hintText: 'Fleet Manager',
                        prefixIcon: Icons.work_outline,
                        textInputAction: TextInputAction.next,
                      ),
                      const SizedBox(height: AppConstants.spaceLg),
                      AppTextField(
                        label: 'Yard Address',
                        controller: _yardAddressController,
                        hintText: '123 Main St, City, ST 12345',
                        prefixIcon: Icons.location_on_outlined,
                        textInputAction: TextInputAction.done,
                      ),
                    ],

                    if (auth.errorMessage != null) ...[
                      const SizedBox(height: AppConstants.spaceMd),
                      Container(
                        padding: const EdgeInsets.all(AppConstants.spaceMd),
                        decoration: BoxDecoration(
                          color: AppColors.statusErrorBg,
                          borderRadius: BorderRadius.circular(
                            AppConstants.radiusSm,
                          ),
                        ),
                        child: Text(
                          auth.errorMessage!,
                          style: const TextStyle(
                            color: AppColors.statusErrorFg,
                            fontSize: 13,
                          ),
                        ),
                      ),
                    ],
                    const SizedBox(height: AppConstants.spaceXl),
                    PrimaryButton(
                      label: 'Create Account',
                      isLoading: auth.isSubmitting,
                      onPressed: _submit,
                    ),
                  ],
                );
              },
            ),
          ),
        ),
      ),
    );
  }
}
