/// Mirrors the backend's generic paging envelope, `PagedResponseDto<T>`
/// (`{ items, page, pageSize, totalItems, totalPages }`) — reusable by any
/// feature that calls a paginated list endpoint.
class PagedResult<T> {
  const PagedResult({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalItems,
    required this.totalPages,
  });

  factory PagedResult.fromJson(
    Map<String, dynamic> json,
    T Function(Map<String, dynamic>) fromJsonT,
  ) {
    return PagedResult(
      items: (json['items'] as List<dynamic>? ?? [])
          .cast<Map<String, dynamic>>()
          .map(fromJsonT)
          .toList(),
      page: json['page'] as int? ?? 1,
      pageSize: json['pageSize'] as int? ?? 0,
      totalItems: json['totalItems'] as int? ?? 0,
      totalPages: json['totalPages'] as int? ?? 0,
    );
  }

  final List<T> items;
  final int page;
  final int pageSize;
  final int totalItems;
  final int totalPages;

  bool get hasNextPage => page < totalPages;
}
