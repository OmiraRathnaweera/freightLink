import 'package:intl/intl.dart';

/// Currency/date/number formatting shared across the app, matching the
/// mockups' `LKR 42,500.00` / `12 Aug 2026` style strings.
class AppFormatters {
  AppFormatters._();

  static final NumberFormat _currency = NumberFormat.currency(
    locale: 'en_US',
    symbol: 'LKR ',
    decimalDigits: 2,
  );

  static final NumberFormat _weight = NumberFormat.decimalPattern('en_US');

  static final DateFormat _date = DateFormat('d MMM y');
  static final DateFormat _dateTime = DateFormat('d MMM y, h:mm a');
  static final DateFormat _time = DateFormat('h:mm a');

  static String currency(num? amount) => _currency.format(amount ?? 0);

  static String weightKg(num kg) => '${_weight.format(kg)} kg';

  static String volumeM3(num m3) => '${_weight.format(m3)} cbm';

  static String date(DateTime value) => _date.format(value.toLocal());

  static String dateTime(DateTime value) => _dateTime.format(value.toLocal());

  static String time(DateTime value) => _time.format(value.toLocal());
}
