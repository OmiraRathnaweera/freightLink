import 'package:flutter/material.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';

/// The shimmering placeholder-card list from the "My Loads — Loading State"
/// mockup. No shimmer package added for one subtle animation — a simple
/// opacity pulse gets the same effect.
class LoadListSkeleton extends StatefulWidget {
  const LoadListSkeleton({super.key, this.itemCount = 3});

  final int itemCount;

  @override
  State<LoadListSkeleton> createState() => _LoadListSkeletonState();
}

class _LoadListSkeletonState extends State<LoadListSkeleton>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 900),
  )..repeat(reverse: true);

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return FadeTransition(
      opacity: _controller.drive(
        Tween<double>(
          begin: 0.5,
          end: 1,
        ).chain(CurveTween(curve: Curves.easeInOut)),
      ),
      child: ListView.separated(
        padding: const EdgeInsets.all(AppConstants.spaceLg),
        itemCount: widget.itemCount,
        separatorBuilder: (_, _) =>
            const SizedBox(height: AppConstants.spaceLg),
        itemBuilder: (context, _) => const _SkeletonCard(),
      ),
    );
  }
}

class _SkeletonCard extends StatelessWidget {
  const _SkeletonCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              _bar(width: 70, height: 14),
              _bar(width: 90, height: 20),
            ],
          ),
          const SizedBox(height: AppConstants.spaceLg),
          _bar(width: double.infinity, height: 14),
          const SizedBox(height: AppConstants.spaceSm),
          _bar(width: 160, height: 14),
          const SizedBox(height: AppConstants.spaceLg),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              _bar(width: 60, height: 12),
              _bar(width: 100, height: 12),
            ],
          ),
        ],
      ),
    );
  }

  Widget _bar({required double width, required double height}) {
    return Container(
      width: width,
      height: height,
      decoration: BoxDecoration(
        color: AppColors.statusNeutralBg,
        borderRadius: BorderRadius.circular(4),
      ),
    );
  }
}
