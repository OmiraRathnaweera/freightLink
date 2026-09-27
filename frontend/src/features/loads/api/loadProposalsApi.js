import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../../lib/api/api.js'

/**
 * Manual load-proposal API client: an Agency bids a price directly on a posted load, alongside
 * (not through) the multi-agent matching pipeline, and the Shipper reviews and accepts one.
 */

export const loadProposalKeys = {
  list: (loadId) => ['loads', loadId, 'proposals'],
}

export async function listLoadProposals(loadId) {
  return api.get(`/loads/${loadId}/proposals`)
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
      if (onSuccess) onSuccess(data, variables, context)
    },
    ...options,
  })
}
