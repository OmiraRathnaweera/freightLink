import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '../../../lib/api/api.js'
import { queryClient } from '../../../lib/api/queryClient.js'

export const matchKeys = {
  all: ['match'],
  loadMatch: (loadId) => [...matchKeys.all, loadId],
  loadMatchHistory: (loadId) => [...matchKeys.all, loadId, 'history'],
}

/**
 * GET /api/v1/loads/{loadId}/match
 * Fetches the AI workflow recommendation, alternate candidates, validation checklist, and pipeline
 * steps. Purely read-only — never triggers the AI pipeline as a side effect of viewing data. Use
 * `triggerLoadMatch` to actually start a run.
 * @param {string} loadId
 * @returns {Promise<import('./types').LoadMatchRecommendationDto>}
 */
export async function getLoadMatch(loadId) {
  return api.get(`/loads/${loadId}/match`)
}

/**
 * GET /api/v1/loads/{loadId}/match/history
 * Fetches every agent workflow run attempt ever made for this load (not just the latest),
 * each with its own 4 agent steps and every tool call made during them - the full agent
 * call history, for the AI Workflow Console's history view. Purely read-only.
 * @param {string} loadId
 * @returns {Promise<import('./types').LoadMatchHistoryDto>}
 */
export async function getLoadMatchHistory(loadId) {
  return api.get(`/loads/${loadId}/match/history`)
}

/**
 * POST /api/v1/loads/{loadId}/match/trigger
 * Explicitly triggers the Agentic AI pipeline for a load — a deliberate command, not a side effect
 * of viewing data. Creates the next AgentWorkflowRun attempt and returns the resulting
 * recommendation once the agent service responds.
 * @param {string} loadId
 * @returns {Promise<import('./types').LoadMatchRecommendationDto>}
 */
export async function triggerLoadMatch(loadId) {
  return api.post(`/loads/${loadId}/match/trigger`)
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
 * Query hook for fetching the full agent call history (every attempt, every tool call) for a load.
 * Disabled until explicitly enabled by the caller (the history view is opened on demand, not
 * fetched eagerly alongside the main console).
 * @param {string} loadId
 * @param {object} [options]
 */
export function useLoadMatchHistoryQuery(loadId, options) {
  return useQuery({
    queryKey: matchKeys.loadMatchHistory(loadId),
    queryFn: () => getLoadMatchHistory(loadId),
    enabled: Boolean(loadId) && (options?.enabled ?? true),
    staleTime: 30_000,
    ...options,
  })
}

/**
 * Mutation hook for explicitly triggering the AI matching pipeline.
 * @param {object} [options]
 */
export function useTriggerMatchMutation(options) {
  return useMutation({
    mutationFn: (loadId) => triggerLoadMatch(loadId),
    onSuccess: (data, loadId, context) => {
      queryClient.invalidateQueries({ queryKey: matchKeys.loadMatch(loadId) })
      options?.onSuccess?.(data, loadId, context)
    },
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
