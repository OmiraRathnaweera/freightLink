import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'core/constants/app_constants.dart';
import 'core/theme/app_theme.dart';
import 'features/auth/providers/auth_provider.dart';
import 'features/auth/screens/login_screen.dart';
import 'features/loads/data/loads_repository.dart';
import 'shared/widgets/app_shell.dart';

void main() {
  runApp(const FreightLinkApp());
}

class FreightLinkApp extends StatelessWidget {
  const FreightLinkApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => AuthProvider()..bootstrap()),
        // LoadsRepository wraps AuthProvider's single ApiClient instance, so
        // it always sees the current auth token without being rebuilt on
        // every request.
        ProxyProvider<AuthProvider, LoadsRepository>(
          update: (_, auth, _) => LoadsRepository(auth.apiClient),
        ),
      ],
      child: MaterialApp(
        title: AppConstants.appName,
        debugShowCheckedModeBanner: false,
        theme: AppTheme.light,
        home: const RootScreen(),
      ),
    );
  }
}

/// Switches between the login screen and the app shell based on
/// [AuthProvider.status], set by `bootstrap()` at startup. Public (rather
/// than the usual private-widget convention) so widget tests can pump this
/// exact routing switch instead of re-implementing a copy of it.
class RootScreen extends StatelessWidget {
  const RootScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final status = context.watch<AuthProvider>().status;

    return switch (status) {
      AuthStatus.unknown => const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      ),
      AuthStatus.guest => const LoginScreen(),
      AuthStatus.authenticated => const AppShell(),
    };
  }
}
