import 'package:image_picker/image_picker.dart';

/// Represents a captured or picked image ready for upload.
class CapturedImage {
  const CapturedImage({
    required this.bytes,
    required this.name,
    this.mimeType = 'image/jpeg',
  });

  final List<int> bytes;
  final String name;
  final String? mimeType;

  int get sizeBytes => bytes.length;

  String get formattedSize {
    if (sizeBytes < 1024) return '$sizeBytes B';
    if (sizeBytes < 1024 * 1024) {
      return '${(sizeBytes / 1024).toStringAsFixed(1)} KB';
    }
    return '${(sizeBytes / (1024 * 1024)).toStringAsFixed(2)} MB';
  }
}

/// Abstract service contract for camera capture and gallery selection.
/// Enables mock injection in tests without invoking native platform camera channels.
abstract class ImageCaptureService {
  Future<CapturedImage?> capturePhoto();
  Future<CapturedImage?> pickFromGallery();
}

/// Default implementation backed by the official `image_picker` package.
class DefaultImageCaptureService implements ImageCaptureService {
  DefaultImageCaptureService({ImagePicker? picker})
      : _picker = picker ?? ImagePicker();

  final ImagePicker _picker;

  @override
  Future<CapturedImage?> capturePhoto() async {
    final xFile = await _picker.pickImage(
      source: ImageSource.camera,
      imageQuality: 85,
      maxWidth: 1920,
      maxHeight: 1920,
    );

    if (xFile == null) return null;

    final bytes = await xFile.readAsBytes();
    return CapturedImage(
      bytes: bytes,
      name: xFile.name.isNotEmpty
          ? xFile.name
          : 'pickup_proof_${DateTime.now().millisecondsSinceEpoch}.jpg',
      mimeType: xFile.mimeType ?? 'image/jpeg',
    );
  }

  @override
  Future<CapturedImage?> pickFromGallery() async {
    final xFile = await _picker.pickImage(
      source: ImageSource.gallery,
      imageQuality: 85,
      maxWidth: 1920,
      maxHeight: 1920,
    );

    if (xFile == null) return null;

    final bytes = await xFile.readAsBytes();
    return CapturedImage(
      bytes: bytes,
      name: xFile.name.isNotEmpty
          ? xFile.name
          : 'pickup_proof_${DateTime.now().millisecondsSinceEpoch}.jpg',
      mimeType: xFile.mimeType ?? 'image/jpeg',
    );
  }
}
