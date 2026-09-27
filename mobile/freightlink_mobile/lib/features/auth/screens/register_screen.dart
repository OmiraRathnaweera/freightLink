import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/primary_button.dart';
import '../providers/auth_provider.dart';
import '../../loads/screens/location_picker_screen.dart';

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
  PickedLocation? _yardLocation;

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
    final yard = _yardLocation;
    if (yard == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Select your agency yard location.')),
      );
      return;
    }

    final registered = await auth.registerAgency(
      email: _emailController.text.trim(),
      password: _passwordController.text,
      fullName: _fullNameController.text.trim(),
      phoneE164: _phoneController.text.trim(),
      jobTitle: _jobTitleController.text.trim(),
      agencyName: _agencyNameController.text.trim(),
      businessRegNo: _businessRegNoController.text.trim(),
      yardAddress: yard.address,
      yardLat: yard.point.latitude,
      yardLng: yard.point.longitude,
    );
    if (registered && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Agency registered. Verify your email, then sign in.')),
      );
      context.go('/login');
    }
  }

  Future<void> _pickYard() async {
    final picked = await Navigator.of(context).push<PickedLocation>(
      MaterialPageRoute(
        builder: (_) => LocationPickerScreen(
          title: 'Select agency yard',
          initialPoint: _yardLocation?.point,
        ),
      ),
    );
    if (picked == null || !mounted) return;
    setState(() {
      _yardLocation = picked;
      _yardAddressController.text = picked.address;
    });
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
                        hintText: 'Tap to select on map',
                        prefixIcon: Icons.location_on_outlined,
                        readOnly: true,
                        onTap: _pickYard,
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
