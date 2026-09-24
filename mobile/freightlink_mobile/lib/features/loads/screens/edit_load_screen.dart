import 'package:flutter/widgets.dart';

import '../models/load.dart';
import 'load_form_screen.dart';

/// **Edit Load** — edits an existing load. Only reachable for Draft/Posted
/// loads (`LoadStatus.isEditable`, mirroring the backend's edit rule). See
/// [LoadFormScreen] for the shared form implementation.
class EditLoadScreen extends StatelessWidget {
  const EditLoadScreen({super.key, required this.load});

  final Load load;

  @override
  Widget build(BuildContext context) => LoadFormScreen(editing: load);
}
