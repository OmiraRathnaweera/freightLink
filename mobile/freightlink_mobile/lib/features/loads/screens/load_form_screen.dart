import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/cancel_reason_dialog.dart';
import '../../../shared/widgets/primary_button.dart';
import '../../../shared/widgets/section_card.dart';
import '../data/loads_repository.dart';
import '../models/load.dart';
import '../models/load_validation_rules.dart';
import '../providers/load_form_provider.dart';
import 'location_picker_screen.dart';

/// Blocks anything but digits and a single decimal point from ever being
/// typed into a numeric field — a real-time complement to
/// `LoadFormProvider`'s "Enter a valid number" message, not a replacement
/// for it.
final _decimalInputFormatters = <TextInputFormatter>[
  FilteringTextInputFormatter.allow(RegExp(r'[0-9.]')),
];

/// The shared **Post a Load** / **Edit Load** form. The mockups show 3
/// differing field sets across their states — this reconciles them into the
/// one shape `CreateLoadDto`/`UpdateLoadDto` actually accepts (see the
/// implementation plan). [editing] is null when posting a new load.
class LoadFormScreen extends StatelessWidget {
  const LoadFormScreen({super.key, this.editing});

  final Load? editing;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          LoadFormProvider(context.read<LoadsRepository>(), initial: editing),
      child: const _LoadFormBody(),
    );
  }
}

class _LoadFormBody extends StatefulWidget {
  const _LoadFormBody();

  @override
  State<_LoadFormBody> createState() => _LoadFormBodyState();
}

class _LoadFormBodyState extends State<_LoadFormBody> {
  final _cargoController = TextEditingController();
  final _weightController = TextEditingController();
  final _volumeController = TextEditingController();
  final _pickupController = TextEditingController();
  final _dropoffController = TextEditingController();
  final _windowController = TextEditingController();

  @override
  void initState() {
    super.initState();
    final form = context.read<LoadFormProvider>();
    _cargoController.text = form.cargoDescription;
    _weightController.text = form.weightKgText;
    _volumeController.text = form.volumeM3Text;
  }

  @override
  void dispose() {
    _cargoController.dispose();
    _weightController.dispose();
    _volumeController.dispose();
    _pickupController.dispose();
    _dropoffController.dispose();
    _windowController.dispose();
    super.dispose();
  }

  Future<void> _pickLocation({
    required bool isPickup,
    required LoadFormProvider form,
  }) async {
    final point = isPickup ? form.pickupPoint : form.dropoffPoint;
    final result = await Navigator.of(context).push<PickedLocation>(
      MaterialPageRoute(
        builder: (_) => LocationPickerScreen(
          title: isPickup ? 'Pickup location' : 'Delivery location',
          initialPoint: point,
        ),
      ),
    );
    if (result == null || !mounted) return;

    if (isPickup) {
      form.setPickup(result.address, result.point);
    } else {
      form.setDropoff(result.address, result.point);
    }
  }

  Future<void> _pickWindow(LoadFormProvider form) async {
    final now = DateTime.now();
    final date = await showDatePicker(
      context: context,
      initialDate: form.pickupWindowStart ?? now,
      firstDate: now.subtract(const Duration(days: 1)),
      lastDate: now.add(const Duration(days: 365)),
    );
    if (date == null || !mounted) return;

    final startTime = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(form.pickupWindowStart ?? now),
    );
    if (startTime == null || !mounted) return;

    final start = DateTime(
      date.year,
      date.month,
      date.day,
      startTime.hour,
      startTime.minute,
    );

    final endTime = await showTimePicker(
      context: context,
      helpText: 'END OF PICKUP WINDOW',
      initialTime: TimeOfDay.fromDateTime(
        form.pickupWindowEnd ?? start.add(const Duration(hours: 2)),
      ),
    );
    if (endTime == null || !mounted) return;

    var end = DateTime(
      date.year,
      date.month,
      date.day,
      endTime.hour,
      endTime.minute,
    );
    if (!end.isAfter(start)) {
      end = start.add(const Duration(hours: 1));
    }
    form.setPickupWindow(start, end);
  }

  Future<void> _submit(LoadFormProvider form) async {
    // Sync controller text into the provider before validating/submitting.
    form
      ..setCargoDescription(_cargoController.text)
      ..setWeightKgText(_weightController.text)
      ..setVolumeM3Text(_volumeController.text);

    final success = await form.submit();
    if (success && mounted) {
      Navigator.of(context).pop(form.result);
    }
  }

  /// Edit Load's header trash icon — there's no delete endpoint, so this is
  /// the closest real capability: cancel the load (`PATCH .../status` →
  /// Cancelled), which requires a reason.
  Future<void> _cancelLoad(Load editing) async {
    final reason = await showCancelReasonDialog(context);
    if (reason == null || !mounted) return;

    try {
      final updated = await context.read<LoadsRepository>().cancel(
        editing.loadId,
        reason: reason,
      );
      if (mounted) Navigator.of(context).pop(updated);
    } on Exception catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text('$error')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final form = context.watch<LoadFormProvider>();
    final isEditing = form.isEditing;

    // These three are read-only, tap-to-open fields — kept in sync with
    // provider state here rather than rebuilt as new controllers, which
    // would drop focus/selection on every rebuild.
    _pickupController.text = form.pickupAddress;
    _dropoffController.text = form.dropoffAddress;
    _windowController.text = form.pickupWindowStart == null
        ? ''
        : '${AppFormatters.dateTime(form.pickupWindowStart!)} – ${AppFormatters.time(form.pickupWindowEnd!)}';

    return Scaffold(
      appBar: AppTopBar(
        title: isEditing ? 'Edit Load' : 'Post a Load',
        subtitle: isEditing ? form.editing!.referenceCode : null,
        showBackButton: true,
        actions: [
          if (isEditing && form.editing!.status.isCancellable)
            IconButton(
              tooltip: 'Cancel load',
              icon: const Icon(Icons.delete_outline_rounded),
              onPressed: () => _cancelLoad(form.editing!),
            ),
        ],
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(AppConstants.spaceLg),
          children: [
            if (form.formError != null) ...[
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(AppConstants.spaceMd),
                decoration: BoxDecoration(
                  color: AppColors.statusErrorBg,
                  borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                  border: Border.all(color: AppColors.statusErrorFg),
                ),
                child: Row(
                  children: [
                    const Icon(
                      Icons.error_outline_rounded,
                      color: AppColors.statusErrorFg,
                      size: 18,
                    ),
                    const SizedBox(width: AppConstants.spaceSm),
                    Expanded(
                      child: Text(
                        form.formError!,
                        style: const TextStyle(
                          color: AppColors.statusErrorFg,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: AppConstants.spaceLg),
            ],
            SectionCard(
              title: 'Route Details',
              icon: Icons.route_outlined,
              child: Column(
                children: [
                  AppTextField(
                    label: 'Pickup Location',
                    hintText: 'Tap to choose on map',
                    prefixIcon: Icons.location_on_outlined,
                    readOnly: true,
                    controller: _pickupController,
                    errorText: form.errorFor('pickupAddress'),
                    onTap: () => _pickLocation(isPickup: true, form: form),
                  ),
                  const SizedBox(height: AppConstants.spaceLg),
                  AppTextField(
                    label: 'Delivery Location',
                    hintText: 'Tap to choose on map',
                    prefixIcon: Icons.flag_outlined,
                    readOnly: true,
                    controller: _dropoffController,
                    errorText: form.errorFor('dropoffAddress'),
                    onTap: () => _pickLocation(isPickup: false, form: form),
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppConstants.spaceLg),
            SectionCard(
              title: 'Scheduling',
              icon: Icons.event_outlined,
              child: AppTextField(
                label: 'Pickup Window',
                hintText: 'mm/dd/yyyy, start – end',
                prefixIcon: Icons.calendar_today_outlined,
                readOnly: true,
                controller: _windowController,
                errorText:
                    form.errorFor('pickupWindowStart') ??
                    form.errorFor('pickupWindowEnd'),
                onTap: () => _pickWindow(form),
              ),
            ),
            const SizedBox(height: AppConstants.spaceLg),
            SectionCard(
              title: 'Cargo Specifications',
              icon: Icons.inventory_2_outlined,
              child: Column(
                children: [
                  AppTextField(
                    label: 'Cargo Description',
                    hintText: 'e.g. Industrial machinery parts',
                    controller: _cargoController,
                    maxLines: 2,
                    maxLength: LoadValidationRules.cargoDescriptionMaxLength,
                    errorText: form.errorFor('cargoDescription'),
                    onChanged: form.setCargoDescription,
                  ),
                  const SizedBox(height: AppConstants.spaceLg),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: AppTextField(
                          label: 'Total Weight (kg)',
                          hintText: '0.00',
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                          ),
                          inputFormatters: _decimalInputFormatters,
                          controller: _weightController,
                          errorText: form.errorFor('weightKg'),
                          onChanged: form.setWeightKgText,
                        ),
                      ),
                      const SizedBox(width: AppConstants.spaceLg),
                      Expanded(
                        child: AppTextField(
                          label: 'Volume (m³)',
                          hintText: '0.00',
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                          ),
                          inputFormatters: _decimalInputFormatters,
                          controller: _volumeController,
                          errorText: form.errorFor('volumeM3'),
                          onChanged: form.setVolumeM3Text,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: SafeArea(
        child: Container(
          padding: const EdgeInsets.all(AppConstants.spaceLg),
          decoration: const BoxDecoration(
            color: AppColors.surface,
            border: Border(top: BorderSide(color: AppColors.border)),
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Expanded(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'ESTIMATED RATE',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: AppColors.inkMuted,
                      ),
                    ),
                    Text(
                      isEditing && form.editing!.estimatedPrice != null
                          ? AppFormatters.currency(form.editing!.estimatedPrice)
                          : 'Calculated after posting',
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w700,
                        color: AppColors.ink,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: AppConstants.spaceLg),
              SizedBox(
                width: 180,
                child: PrimaryButton(
                  label: isEditing ? 'Update Load' : 'Post Load',
                  loadingLabel: 'Submitting...',
                  isLoading: form.isSubmitting,
                  onPressed: () => _submit(form),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
