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
export async function getLoadMatch(loadId, rerun = false) {
  const url = rerun ? `/loads/${loadId}/match?rerun=true` : `/loads/${loadId}/match`
  return api.get(url)
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
 * POST /api/v1/loads/{loadId}/match/reject
 * Rejects the load's current AI match recommendation, recording the shipper's reason.
 * @param {string} loadId
 * @param {string} reason
 * @returns {Promise<any>}
 */
export async function rejectLoadMatch(loadId, reason) {
  return api.post(`/loads/${loadId}/match/reject`, { reason })
}

/**
 * POST /api/v1/loads/{loadId}/match/revise
 * Requests a revised AI match recommendation for the load, recording the shipper's reason.
 * @param {string} loadId
 * @param {string} reason
 * @returns {Promise<any>}
 */
export async function reviseLoadMatch(loadId, reason) {
  return api.post(`/loads/${loadId}/match/revise`, { reason })
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

/**
 * Mutation hook for rejecting a carrier match recommendation.
 * @param {object} [options]
 */
export function useRejectMatchMutation(options) {
  return useMutation({
    mutationFn: ({ loadId, reason }) => rejectLoadMatch(loadId, reason),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: matchKeys.loadMatch(variables.loadId) })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}

/**
 * Mutation hook for requesting a revised carrier match recommendation.
 * @param {object} [options]
 */
export function useReviseMatchMutation(options) {
  return useMutation({
    mutationFn: ({ loadId, reason }) => reviseLoadMatch(loadId, reason),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: matchKeys.loadMatch(variables.loadId) })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}
