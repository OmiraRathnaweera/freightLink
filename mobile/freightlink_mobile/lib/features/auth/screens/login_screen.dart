import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/primary_button.dart';
import '../providers/auth_provider.dart';

/// One common mobile login. The backend resolves whether this account is a
/// Shipper, Agency Staff member, or Driver through /auth/me after sign-in.
class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _obscurePassword = true;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    await context.read<AuthProvider>().login(
          email: _emailController.text.trim(),
          password: _passwordController.text,
        );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(AppConstants.spaceXl),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Consumer<AuthProvider>(
                builder: (context, auth, _) => Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Center(
                      child: Container(
                        width: 64,
                        height: 64,
                        alignment: Alignment.center,
                        decoration: BoxDecoration(
                          color: AppColors.primary,
                          borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                        ),
                        child: const Icon(Icons.local_shipping_rounded, color: AppColors.onPrimary, size: 32),
                      ),
                    ),
                    const SizedBox(height: AppConstants.spaceLg),
                    const Text(
                      AppConstants.appName,
                      textAlign: TextAlign.center,
                      style: TextStyle(fontSize: 24, fontWeight: FontWeight.w700, color: AppColors.ink),
                    ),
                    const SizedBox(height: 4),
                    const Text(
                      'Sign in to your FreightLink account',
                      textAlign: TextAlign.center,
                      style: TextStyle(fontSize: 14, color: AppColors.inkMuted),
                    ),
                    const SizedBox(height: AppConstants.spaceXxl),
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
                      textInputAction: TextInputAction.done,
                      suffixIcon: IconButton(
                        icon: Icon(_obscurePassword ? Icons.visibility_outlined : Icons.visibility_off_outlined, size: 20),
                        onPressed: () => setState(() => _obscurePassword = !_obscurePassword),
                      ),
                    ),
                    if (auth.errorMessage != null) ...[
                      const SizedBox(height: AppConstants.spaceMd),
                      Container(
                        padding: const EdgeInsets.all(AppConstants.spaceMd),
                        decoration: BoxDecoration(
                          color: AppColors.statusErrorBg,
                          borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                        ),
                        child: Text(auth.errorMessage!, style: const TextStyle(color: AppColors.statusErrorFg, fontSize: 13)),
                      ),
                    ],
                    const SizedBox(height: AppConstants.spaceXl),
                    PrimaryButton(label: 'Sign in', isLoading: auth.isSubmitting, onPressed: _submit),
                    const SizedBox(height: AppConstants.spaceLg),
                    Wrap(
                      alignment: WrapAlignment.center,
                      crossAxisAlignment: WrapCrossAlignment.center,
                      spacing: 4,
                      children: [
                        const Text("Registering an agency?", style: TextStyle(color: AppColors.inkMuted, fontSize: 13)),
                        TextButton(
                          onPressed: () => context.go('/register'),
                          child: const Text('Register agency'),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
