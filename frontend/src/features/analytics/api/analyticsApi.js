import { useQuery } from '@tanstack/react-query'
import { api } from '../../../lib/api/api.js'

export const analyticsKeys = {
  summary: ['analytics', 'summary'],
}

/**
 * GET /admin/analytics/summary â€” a read-only, on-demand system-wide snapshot (counts by
 * status/role across loads, agencies, trips, assignments, disputes, invoices, users, plus
 * invoice revenue totals). Admin-only.
 * @returns {Promise<import('./types').AdminAnalyticsSummary>}
 */
export async function getAnalyticsSummary() {
  return api.get('/admin/analytics/summary')
}

/** Query hook for the Admin analytics dashboard. */
export function useAnalyticsSummaryQuery(options) {
  return useQuery({
    queryKey: analyticsKeys.summary,
    queryFn: getAnalyticsSummary,
    staleTime: 30_000,
    ...options,
  })
}
