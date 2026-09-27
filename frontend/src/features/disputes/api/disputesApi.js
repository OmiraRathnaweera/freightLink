import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../../lib/api/api.js'
import { validateResolutionPayload } from '../lib/disputeRules.js'

export const disputeKeys = {
  all: ['disputes'],
  list: (filters) => [...disputeKeys.all, 'list', filters],
  detail: (id) => [...disputeKeys.all, 'detail', id],
}

function toDisplayDispute(dispute) {
  return {
    ...dispute,
    displayId: dispute.disputeId?.slice(0, 8).toUpperCase(),
    raisedDate: dispute.createdAt,
    trip: {
      tripId: dispute.tripId,
      routeSummary: dispute.tripRouteSummary ?? 'Route details unavailable',
      carrierAgency: dispute.carrierAgencyName ?? '—',
      truckRegNo: dispute.vehicleRegistrationNo ?? '—',
    },
    raisedByUser: {
      name: dispute.raisedByName ?? dispute.raisedByUserId?.slice(0, 8),
      role: dispute.raisedByRole ?? 'Account holder',
      company: dispute.carrierAgencyName ?? '—',
      email: dispute.raisedByEmail ?? '—',
      phone: '—',
    },
    resolution: dispute.resolution ?? null,
  }
}

export async function fetchDisputes({ status = 'All', search = '', category = 'All' } = {}) {
  const params = { page: 1, pageSize: 100 }
  if (status !== 'All') params.status = status
  if (category !== 'All') params.category = category === 'Payment Issue' ? 'Billing' : category

  const response = await api.get('/disputes', { params })
  const items = (response.items ?? []).map(toDisplayDispute)
  const query = search.trim().toLowerCase()
  if (!query) return items

  return items.filter((item) =>
    [item.disputeId, item.displayId, item.tripId, item.description, item.category, item.status]
      .filter(Boolean)
      .some((value) => String(value).toLowerCase().includes(query)),
  )
}

export async function fetchDispute(disputeId) {
  return toDisplayDispute(await api.get(`/disputes/${disputeId}`))
}

export async function raiseDispute({ tripId, category, description }) {
  return toDisplayDispute(
    await api.post('/disputes', {
      tripId,
      category,
      description: description.trim(),
    }),
  )
}

export async function startReviewDispute(disputeId) {
  return toDisplayDispute(await api.patch(`/disputes/${disputeId}/review`))
}

export async function resolveDispute({ disputeId, outcome = 'Upheld', resolutionNote }) {
  const validationError = validateResolutionPayload(resolutionNote)
  if (validationError) throw new Error(validationError)

  return toDisplayDispute(await api.patch(`/disputes/${disputeId}/resolve`, {
    outcome,
    resolutionNote: resolutionNote.trim(),
  }))
}

export function useDisputesQuery(filters = {}) {
  return useQuery({ queryKey: disputeKeys.list(filters), queryFn: () => fetchDisputes(filters) })
}

export function useDisputeDetailQuery(disputeId, options = {}) {
  return useQuery({
    queryKey: disputeKeys.detail(disputeId),
    queryFn: () => fetchDispute(disputeId),
    enabled: Boolean(disputeId),
    ...options,
  })
}

export function useRaiseDisputeMutation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: raiseDispute,
    onSuccess: (data) => invalidateDisputes(queryClient, data.disputeId),
  })
}

function invalidateDisputes(queryClient, disputeId) {
  queryClient.invalidateQueries({ queryKey: disputeKeys.all })
  if (disputeId) queryClient.invalidateQueries({ queryKey: disputeKeys.detail(disputeId) })
}

export function useStartReviewMutation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: startReviewDispute,
    onSuccess: (data) => invalidateDisputes(queryClient, data.disputeId),
  })
}

export function useResolveDisputeMutation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: resolveDispute,
    onSuccess: (data) => invalidateDisputes(queryClient, data.disputeId),
  })
}
