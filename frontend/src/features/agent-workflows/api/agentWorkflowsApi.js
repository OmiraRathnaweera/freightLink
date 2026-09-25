import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '../../../lib/api/api.js'
import { queryClient } from '../../../lib/api/queryClient.js'

export const matchKeys = {
  all: ['match'],
  loadMatch: (loadId) => [...matchKeys.all, loadId],
}

/**
 * GET /api/v1/loads/{loadId}/match
 * Fetches the AI workflow recommendation, alternate candidates, validation checklist, and pipeline steps.
 * @param {string} loadId
 * @returns {Promise<import('./types').LoadMatchRecommendationDto>}
 */
export async function getLoadMatch(loadId) {
  return api.get(`/loads/${loadId}/match`)
}

/**
 * POST /api/v1/loads/{loadId}/match/confirm
 * Approves a matched carrier for the load (concurrency-safe, ADR-013 / ADR-016).
 * Creates an Assignment in Proposed status and transitions load to Matched.
 * @param {string} loadId
 * @param {string} agencyId
 * @returns {Promise<any>}
 */
export async function confirmLoadMatch(loadId, agencyId) {
  return api.post(`/loads/${loadId}/match/confirm`, { agencyId })
}

/**
 * Query hook for fetching AI match recommendation and pipeline telemetry.
 * @param {string} loadId
 * @param {object} [options]
 */
export function useLoadMatchQuery(loadId, options) {
  return useQuery({
    queryKey: matchKeys.loadMatch(loadId),
    queryFn: () => getLoadMatch(loadId),
    enabled: Boolean(loadId),
    staleTime: 30_000,
    ...options,
  })
}

/**
 * Mutation hook for approving a carrier match.
 * @param {object} [options]
 */
export function useConfirmMatchMutation(options) {
  return useMutation({
    mutationFn: ({ loadId, agencyId }) => confirmLoadMatch(loadId, agencyId),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: matchKeys.loadMatch(variables.loadId) })
      queryClient.invalidateQueries({ queryKey: ['loads'] })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}
