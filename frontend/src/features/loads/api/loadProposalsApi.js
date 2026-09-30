import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../../lib/api/api.js'

/**
 * Manual load-proposal API client: an Agency bids a price directly on a posted load, alongside
 * (not through) the multi-agent matching pipeline, and the Shipper reviews and accepts one.
 */

export const loadProposalKeys = {
  list: (loadId) => ['loads', loadId, 'proposals'],
  all: () => ['loads', 'proposals', 'all'],
}

export async function listLoadProposals(loadId) {
  return api.get(`/loads/${loadId}/proposals`)
}

/**
 * GET /loads/proposals
 * Every proposal across all of the caller's own loads (Shipper only) — backs the aggregate
 * "Load Proposals" page, as opposed to listLoadProposals' single-load scope.
 */
export async function listAllLoadProposals() {
  return api.get('/loads/proposals')
}

export async function createLoadProposal(loadId, { proposedPrice, message }) {
  return api.post(`/loads/${loadId}/proposals`, { proposedPrice, message: message || undefined })
}

export async function acceptLoadProposal(loadId, proposalId) {
  return api.post(`/loads/${loadId}/proposals/${proposalId}/accept`)
}

export async function rejectLoadProposal(loadId, proposalId, reason) {
  return api.post(`/loads/${loadId}/proposals/${proposalId}/reject`, { reason: reason || undefined })
}

export async function withdrawLoadProposal(loadId, proposalId) {
  return api.post(`/loads/${loadId}/proposals/${proposalId}/withdraw`)
}

export function useLoadProposalsQuery(loadId, options) {
  return useQuery({
    queryKey: loadProposalKeys.list(loadId),
    queryFn: () => listLoadProposals(loadId),
    ...options,
  })
}

export function useAllLoadProposalsQuery(options) {
  return useQuery({
    queryKey: loadProposalKeys.all(),
    queryFn: listAllLoadProposals,
    ...options,
  })
}

export function useCreateLoadProposalMutation(loadId, { onSuccess, ...options } = {}) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload) => createLoadProposal(loadId, payload),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: loadProposalKeys.list(loadId) })
      if (onSuccess) onSuccess(data, variables, context)
    },
    ...options,
  })
}

export function useAcceptLoadProposalMutation(loadId, { onSuccess, ...options } = {}) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (proposalId) => acceptLoadProposal(loadId, proposalId),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: loadProposalKeys.list(loadId) })
      queryClient.invalidateQueries({ queryKey: loadProposalKeys.all() })
      if (onSuccess) onSuccess(data, variables, context)
    },
    ...options,
  })
}

export function useRejectLoadProposalMutation(loadId, { onSuccess, ...options } = {}) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ proposalId, reason }) => rejectLoadProposal(loadId, proposalId, reason),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: loadProposalKeys.list(loadId) })
      queryClient.invalidateQueries({ queryKey: loadProposalKeys.all() })
      if (onSuccess) onSuccess(data, variables, context)
    },
    ...options,
  })
}

export function useWithdrawLoadProposalMutation(loadId, { onSuccess, ...options } = {}) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (proposalId) => withdrawLoadProposal(loadId, proposalId),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: loadProposalKeys.list(loadId) })
      queryClient.invalidateQueries({ queryKey: loadProposalKeys.all() })
      if (onSuccess) onSuccess(data, variables, context)
    },
    ...options,
  })
}

/**
 * Accept/reject variants for the aggregate "Load Proposals" page, whose rows span many different
 * loads at once — unlike the per-load hooks above, the loadId is part of each call's variables
 * rather than fixed once via the hook factory.
 */
export function useAcceptAnyLoadProposalMutation({ onSuccess, ...options } = {}) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ loadId, proposalId }) => acceptLoadProposal(loadId, proposalId),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: loadProposalKeys.list(variables.loadId) })
      queryClient.invalidateQueries({ queryKey: loadProposalKeys.all() })
      if (onSuccess) onSuccess(data, variables, context)
    },
    ...options,
  })
}

export function useRejectAnyLoadProposalMutation({ onSuccess, ...options } = {}) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ loadId, proposalId, reason }) => rejectLoadProposal(loadId, proposalId, reason),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: loadProposalKeys.list(variables.loadId) })
      queryClient.invalidateQueries({ queryKey: loadProposalKeys.all() })
      if (onSuccess) onSuccess(data, variables, context)
    },
    ...options,
  })
}
