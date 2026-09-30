import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/empty_state.dart';
import '../../../shared/widgets/error_state.dart';
import '../../notifications/providers/notification_provider.dart';
import '../data/loads_repository.dart';
import '../providers/loads_list_provider.dart';
import '../widgets/load_card.dart';
import '../widgets/load_list_skeleton.dart';
import '../widgets/load_search_bar.dart';
import '../widgets/load_status_filter_chips.dart';
import 'load_detail_screen.dart';
import 'post_load_screen.dart';

/// **My Loads** — the Shipper's own loads. Covers the mockup's 4 states
/// (dashboard/empty/loading/error) via [LoadsListProvider].
class MyLoadsScreen extends StatelessWidget {
  const MyLoadsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    NotificationProvider? notifProvider;
    try {
      notifProvider = Provider.of<NotificationProvider>(context, listen: false);
    } catch (_) {
      notifProvider = null;
    }

    return ChangeNotifierProvider(
      create: (context) =>
          LoadsListProvider(context.read<LoadsRepository>(), notifProvider)..load(),
      child: const _MyLoadsBody(),
    );
  }
}

class _MyLoadsBody extends StatelessWidget {
  const _MyLoadsBody();

  Future<void> _openPostLoad(BuildContext context) async {
    final created = await Navigator.of(
      context,
    ).push(MaterialPageRoute(builder: (_) => const PostLoadScreen()));
    if (created != null && context.mounted) {
      context.read<LoadsListProvider>().load();
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<LoadsListProvider>();

    return Scaffold(
      appBar: AppTopBar(
        title: 'My Loads',
        leading: const AppAvatar(),
        actions: const [NotificationBellButton()],
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => _openPostLoad(context),
        child: const Icon(Icons.add_rounded),
      ),
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(
                AppConstants.spaceLg,
                AppConstants.spaceLg,
                AppConstants.spaceLg,
                0,
              ),
              child: LoadSearchBar(onChanged: provider.onSearchChanged),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(
                AppConstants.spaceLg,
                AppConstants.spaceMd,
                AppConstants.spaceLg,
                0,
              ),
              child: LoadStatusFilterChips(
                selected: provider.statusFilter,
                onChanged: provider.setStatusFilter,
              ),
            ),
            Expanded(child: _buildBody(context, provider)),
          ],
        ),
      ),
    );
  }

  Widget _buildBody(BuildContext context, LoadsListProvider provider) {
    switch (provider.state) {
      case LoadsListState.loading:
        return const LoadListSkeleton();
      case LoadsListState.error:
        return ErrorState(
          title: 'Unable to load data',
          message:
              provider.error?.message ??
              'We encountered a connection issue while fetching your load '
                  'assignments. Please check your network and try again.',
          errorCode: provider.error?.code,
          onRetry: provider.load,
        );
      case LoadsListState.empty:
        return EmptyState(
          icon: Icons.local_shipping_outlined,
          title: 'No loads found',
          message: 'Post your first load to get started',
          actionLabel: 'Post a Load',
          onAction: () => _openPostLoad(context),
        );
      case LoadsListState.loaded:
        return RefreshIndicator(
          onRefresh: provider.load,
          child: ListView.separated(
            padding: const EdgeInsets.all(AppConstants.spaceLg),
            itemCount: provider.items.length,
            separatorBuilder: (_, _) =>
                const SizedBox(height: AppConstants.spaceLg),
            itemBuilder: (context, index) {
              final load = provider.items[index];
              return LoadCard(
                load: load,
                onTap: () async {
                  await Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => LoadDetailScreen(loadId: load.loadId),
                    ),
                  );
                  if (context.mounted) provider.load();
                },
              );
            },
          ),
        );
    }
  }
}
