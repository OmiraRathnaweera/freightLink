import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/primary_button.dart';
import '../../../shared/widgets/status_pill.dart';
import '../data/trips_repository.dart';
import '../models/trip_models.dart';
import '../services/image_capture_service.dart';

/// Screen for Drivers to capture photographic proof of delivery (signed consignee waybill,
/// unloaded cargo at dock) using device camera/gallery, linking a `DeliveryProof` evidence
/// row and transitioning the trip to `Delivered`, satisfying the Y3S01-74 (ADR-004) hard-block.
class ProofOfDeliveryScreen extends StatefulWidget {
  const ProofOfDeliveryScreen({
    super.key,
    required this.trip,
    this.imageCaptureService,
    this.onDeliveryConfirmed,
  });

  final TripResponse trip;
  final ImageCaptureService? imageCaptureService;
  final ValueChanged<TripResponse>? onDeliveryConfirmed;

  @override
  State<ProofOfDeliveryScreen> createState() => _ProofOfDeliveryScreenState();
}

class _ProofOfDeliveryScreenState extends State<ProofOfDeliveryScreen> {
  late TripResponse _currentTrip;
  late final ImageCaptureService _imageCaptureService;
  CapturedImage? _capturedImage;
  final _notesController = TextEditingController();

  bool _isSubmitting = false;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _currentTrip = widget.trip;
    _imageCaptureService =
        widget.imageCaptureService ?? DefaultImageCaptureService();
  }

  @override
  void dispose() {
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _capturePhoto() async {
    setState(() => _errorMessage = null);
    try {
      final image = await _imageCaptureService.capturePhoto();
      if (image != null && mounted) {
        setState(() => _capturedImage = image);
      }
    } catch (_) {
      if (mounted) {
        setState(() => _errorMessage = 'Failed to capture photo from camera.');
      }
    }
  }

  Future<void> _pickFromGallery() async {
    setState(() => _errorMessage = null);
    try {
      final image = await _imageCaptureService.pickFromGallery();
      if (image != null && mounted) {
        setState(() => _capturedImage = image);
      }
    } catch (_) {
      if (mounted) {
        setState(() => _errorMessage = 'Failed to select photo from gallery.');
      }
    }
  }

  void _retakePhoto() {
    setState(() {
      _capturedImage = null;
      _errorMessage = null;
    });
  }

  Future<void> _submit() async {
    if (_capturedImage == null) {
      setState(() => _errorMessage = 'Please take or select a photo first.');
      return;
    }

    setState(() {
      _isSubmitting = true;
      _errorMessage = null;
    });

    final repo = context.read<TripsRepository>();

    try {
      // Step 1: Upload photo
      final uploadResult = await repo.uploadFile(
        bytes: _capturedImage!.bytes,
        filename: _capturedImage!.name,
      );

      // Step 2: Link evidence as DeliveryProof
      await repo.submitEvidence(
        tripId: _currentTrip.tripId,
        publicId: uploadResult.publicId,
        evidenceType: 'DeliveryProof',
      );

      // Step 3: Advance status to Delivered (satisfies Y3S01-74 hard-block)
      final updated = await repo.changeTripStatus(
        tripId: _currentTrip.tripId,
        targetStatus: 'Delivered',
        notes: _notesController.text.trim().isNotEmpty
            ? _notesController.text.trim()
            : null,
      );

      if (!mounted) return;

      setState(() {
        _currentTrip = updated;
        _isSubmitting = false;
      });

      widget.onDeliveryConfirmed?.call(updated);

      if (mounted) {
        _showSuccessDialog();
      }
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _isSubmitting = false;
        _errorMessage = e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _isSubmitting = false;
        _errorMessage = 'Failed to complete delivery proof. Please try again.';
      });
    }
  }

  void _showSuccessDialog() {
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => AlertDialog(
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        ),
        title: const Row(
          children: [
            Icon(Icons.check_circle_rounded, color: AppColors.statusSuccessFg),
            SizedBox(width: 8),
            Expanded(
              child: Text('Delivery Confirmed!'),
            ),
          ],
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Proof of delivery has been verified. Trip #${_currentTrip.tripId.length > 8 ? _currentTrip.tripId.substring(0, 8).toUpperCase() : _currentTrip.tripId} is officially Delivered.',
              style: const TextStyle(fontSize: 14, color: AppColors.ink),
            ),
            const SizedBox(height: AppConstants.spaceMd),
            Container(
              padding: const EdgeInsets.all(AppConstants.spaceMd),
              decoration: BoxDecoration(
                color: AppColors.statusSuccessBg,
                borderRadius: BorderRadius.circular(AppConstants.radiusSm),
              ),
              child: const Row(
                children: [
                  Icon(Icons.verified_rounded, color: AppColors.statusSuccessFg, size: 18),
                  SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'Hard-block policy (Y3S01-74) satisfied.',
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                        color: AppColors.statusSuccessFg,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () {
              Navigator.of(dialogContext).pop();
              Navigator.of(context).maybePop();
            },
            child: const Text('Done'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final tripCode = _currentTrip.tripId.length > 8
        ? _currentTrip.tripId.substring(0, 8).toUpperCase()
        : _currentTrip.tripId.toUpperCase();

    final statusText = _currentTrip.isDelivered
        ? 'DELIVERED'
        : _currentTrip.status.toUpperCase();

    final (fg, bg) = _currentTrip.isDelivered
        ? (AppColors.statusSuccessFg, AppColors.statusSuccessBg)
        : (AppColors.statusInTransitFg, AppColors.statusInTransitBg);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Proof of Delivery'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back_rounded),
          onPressed: () => Navigator.of(context).maybePop(),
        ),
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 16),
            child: Center(
              child: StatusPill(
                label: statusText,
                foreground: fg,
                background: bg,
              ),
            ),
          ),
        ],
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppConstants.spaceLg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _buildTripSummaryCard(tripCode),
              const SizedBox(height: AppConstants.spaceLg),
              _buildPolicyBanner(),
              const SizedBox(height: AppConstants.spaceLg),
              _buildCaptureSection(),
              const SizedBox(height: AppConstants.spaceLg),
              _buildNotesSection(),
              if (_errorMessage != null) ...[
                const SizedBox(height: AppConstants.spaceMd),
                _buildErrorBanner(),
              ],
              const SizedBox(height: AppConstants.spaceXl),
              if (!_currentTrip.isDelivered)
                PrimaryButton(
                  key: const Key('submit_delivery_proof_button'),
                  label: 'Submit Proof of Delivery',
                  icon: Icons.check_circle_outline_rounded,
                  isLoading: _isSubmitting,
                  onPressed: _capturedImage != null ? _submit : null,
                ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildTripSummaryCard(String tripCode) {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Flexible(
                child: Text(
                  'TRIP #$tripCode',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w700,
                    color: AppColors.primary,
                    letterSpacing: 0.5,
                  ),
                ),
              ),
              if (_currentTrip.referenceCode != null) ...[
                const SizedBox(width: AppConstants.spaceSm),
                Flexible(
                  child: Text(
                    _currentTrip.referenceCode!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    textAlign: TextAlign.end,
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                      color: AppColors.inkMuted,
                    ),
                  ),
                ),
              ],
            ],
          ),
          const Divider(height: 20),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Icon(Icons.location_on_rounded, color: AppColors.primary, size: 20),
              const SizedBox(width: 8),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'DELIVERY SITE',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: AppColors.inkMuted,
                        letterSpacing: 0.5,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      _currentTrip.dropoffAddress ?? 'Destination Address',
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w600,
                        color: AppColors.ink,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: AppConstants.spaceMd),
          if (_currentTrip.cargoDescription != null) ...[
            Row(
              children: [
                const Icon(Icons.inventory_2_outlined, size: 16, color: AppColors.inkMuted),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    _currentTrip.cargoDescription!,
                    style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w500,
                      color: AppColors.ink,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 6),
          ],
          Row(
            children: [
              const Icon(Icons.local_shipping_outlined, size: 16, color: AppColors.inkMuted),
              const SizedBox(width: 8),
              Text(
                _currentTrip.vehicleRegistrationNo ?? 'Vehicle Assigned',
                style: const TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  color: AppColors.ink,
                ),
              ),
              if (_currentTrip.driverName != null) ...[
                const Text(' • ', style: TextStyle(color: AppColors.inkMuted)),
                Text(
                  _currentTrip.driverName!,
                  style: const TextStyle(
                    fontSize: 13,
                    color: AppColors.inkMuted,
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildPolicyBanner() {
    final hasProof = _currentTrip.hasDeliveryProof;

    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceMd),
      decoration: BoxDecoration(
        color: hasProof ? AppColors.statusSuccessBg : AppColors.statusInTransitBg,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        border: Border.all(
          color: hasProof
              ? AppColors.statusSuccessFg.withValues(alpha: 0.2)
              : AppColors.statusInTransitFg.withValues(alpha: 0.3),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            hasProof ? Icons.check_circle_rounded : Icons.gavel_rounded,
            size: 20,
            color: hasProof ? AppColors.statusSuccessFg : AppColors.statusInTransitFg,
          ),
          const SizedBox(width: AppConstants.spaceMd),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  hasProof
                      ? 'Proof of Delivery Verified'
                      : 'Evidence Hard-Block Policy (Y3S01-74)',
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: hasProof ? AppColors.statusSuccessFg : AppColors.statusInTransitFg,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  hasProof
                      ? 'Delivery proof was submitted. The trip status has transitioned to Delivered.'
                      : 'Under FreightLink policy (Y3S01-74), photo evidence of handover must be captured before this trip can advance to Delivered.',
                  style: TextStyle(
                    fontSize: 12,
                    color: hasProof
                        ? AppColors.statusSuccessFg.withValues(alpha: 0.85)
                        : AppColors.statusInTransitFg.withValues(alpha: 0.85),
                    height: 1.3,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildCaptureSection() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'Delivery Photo Proof',
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w700,
              color: AppColors.ink,
            ),
          ),
          const SizedBox(height: 4),
          const Text(
            'Capture signed consignee waybill, receiver signature, or unloaded cargo at dock.',
            style: TextStyle(fontSize: 13, color: AppColors.inkMuted),
          ),
          const SizedBox(height: AppConstants.spaceLg),
          if (_capturedImage != null)
            _buildImagePreview()
          else if (_currentTrip.hasDeliveryProof)
            _buildVerifiedNotice()
          else
            _buildCaptureButtons(),
        ],
      ),
    );
  }

  Widget _buildImagePreview() {
    return Column(
      children: [
        Container(
          height: 220,
          width: double.infinity,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppConstants.radiusMd),
            border: Border.all(color: AppColors.border),
          ),
          clipBehavior: Clip.antiAlias,
          child: Image.memory(
            Uint8List.fromList(_capturedImage!.bytes),
            key: const Key('captured_image_preview'),
            fit: BoxFit.cover,
          ),
        ),
        const SizedBox(height: AppConstants.spaceMd),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Expanded(
              child: Row(
                children: [
                  const Icon(Icons.check_circle_rounded, size: 16, color: AppColors.statusSuccessFg),
                  const SizedBox(width: 4),
                  const Text(
                    'Ready for upload',
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                      color: AppColors.statusSuccessFg,
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      _capturedImage!.name,
                      style: const TextStyle(
                        fontSize: 11,
                        color: AppColors.inkMuted,
                      ),
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: AppConstants.spaceSm),
            TextButton.icon(
              key: const Key('retake_photo_button'),
              icon: const Icon(Icons.refresh_rounded, size: 16),
              label: const Text('Retake Photo'),
              onPressed: _retakePhoto,
            ),
          ],
        ),
      ],
    );
  }

  Widget _buildVerifiedNotice() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceXl),
      decoration: BoxDecoration(
        color: AppColors.statusSuccessBg,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      ),
      child: const Column(
        children: [
          Icon(Icons.verified_rounded, size: 48, color: AppColors.statusSuccessFg),
          SizedBox(height: AppConstants.spaceMd),
          Text(
            'Delivery Evidence Verified',
            style: TextStyle(
              fontSize: 15,
              fontWeight: FontWeight.w700,
              color: AppColors.statusSuccessFg,
            ),
          ),
          SizedBox(height: 4),
          Text(
            'Photo proof has been recorded on the blockchain/immutable trip log.',
            textAlign: TextAlign.center,
            style: TextStyle(fontSize: 12, color: AppColors.inkMuted),
          ),
        ],
      ),
    );
  }

  Widget _buildCaptureButtons() {
    return Column(
      children: [
        Container(
          height: 140,
          decoration: BoxDecoration(
            color: AppColors.background,
            borderRadius: BorderRadius.circular(AppConstants.radiusMd),
            border: Border.all(
              color: AppColors.border,
              style: BorderStyle.solid,
            ),
          ),
          child: const Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(Icons.add_a_photo_outlined, size: 36, color: AppColors.inkMuted),
                SizedBox(height: 8),
                Text(
                  'No photo captured yet',
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w500,
                    color: AppColors.inkMuted,
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: AppConstants.spaceMd),
        Row(
          children: [
            Expanded(
              child: ElevatedButton.icon(
                key: const Key('take_photo_button'),
                icon: const Icon(Icons.camera_alt_rounded),
                label: const Text('Take Photo'),
                style: ElevatedButton.styleFrom(
                  backgroundColor: AppColors.primary,
                  foregroundColor: AppColors.onPrimary,
                  padding: const EdgeInsets.symmetric(vertical: 14),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                  ),
                ),
                onPressed: _capturePhoto,
              ),
            ),
            const SizedBox(width: AppConstants.spaceMd),
            Expanded(
              child: OutlinedButton.icon(
                key: const Key('pick_gallery_button'),
                icon: const Icon(Icons.photo_library_outlined),
                label: const Text('Gallery'),
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppColors.ink,
                  side: const BorderSide(color: AppColors.border),
                  padding: const EdgeInsets.symmetric(vertical: 14),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                  ),
                ),
                onPressed: _pickFromGallery,
              ),
            ),
          ],
        ),
      ],
    );
  }

  Widget _buildNotesSection() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Consignee & Handover Notes (Optional)',
            style: TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w700,
              color: AppColors.ink,
            ),
          ),
          const SizedBox(height: AppConstants.spaceSm),
          AppTextField(
            key: const Key('delivery_notes_field'),
            label: 'Consignee & Handover Notes',
            controller: _notesController,
            hintText: 'e.g. Received by store manager Kasun, goods intact.',
            maxLines: 2,
          ),
        ],
      ),
    );
  }

  Widget _buildErrorBanner() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceMd),
      decoration: BoxDecoration(
        color: AppColors.statusErrorBg,
        borderRadius: BorderRadius.circular(AppConstants.radiusSm),
      ),
      child: Row(
        children: [
          const Icon(Icons.error_outline_rounded, size: 20, color: AppColors.statusErrorFg),
          const SizedBox(width: AppConstants.spaceSm),
          Expanded(
            child: Text(
              _errorMessage!,
              style: const TextStyle(
                color: AppColors.statusErrorFg,
                fontSize: 13,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
