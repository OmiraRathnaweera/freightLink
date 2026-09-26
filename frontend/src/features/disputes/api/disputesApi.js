import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { INITIAL_MOCK_DISPUTES } from '../data/mockDisputes.js'
import { DisputeStatus, canStartReview, canResolveDispute, validateResolutionPayload } from '../lib/disputeRules.js'

const STORAGE_KEY = 'freightlink_admin_disputes_v1'

function getStoredDisputes() {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (raw) {
      return JSON.parse(raw)
    }
  } catch {
    // ignore parse error and fallback
  }
  return [...INITIAL_MOCK_DISPUTES]
}

function persistDisputes(disputes) {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(disputes))
  } catch {
    // ignore storage quota issues
  }
}

// In-memory working copy initialized from session storage
let currentDisputes = getStoredDisputes()

export const disputeKeys = {
  all: ['disputes'],
  list: (filters) => [...disputeKeys.all, 'list', filters],
  detail: (id) => [...disputeKeys.all, 'detail', id],
}

/**
 * Fetch disputes with optional filtering by status, search, and category.
 */
export async function fetchDisputes({ status = 'All', search = '', category = 'All' } = {}) {
  // Simulate network latency (250ms)
  await new Promise((resolve) => setTimeout(resolve, 250))

  let list = [...currentDisputes]

  // Filter by lifecycle status
  if (status && status !== 'All') {
    list = list.filter((item) => item.status === status)
  }

  // Filter by category
  if (category && category !== 'All') {
    list = list.filter((item) => item.category === category)
  }

  // Search by Dispute ID, Trip ID, or User Name / Email
  if (search && search.trim() !== '') {
    const q = search.trim().toLowerCase()
    list = list.filter((item) => {
      const matchDisplayId = item.displayId?.toLowerCase().includes(q)
      const matchDisputeId = item.disputeId?.toLowerCase().includes(q)
      const matchTripId = item.trip?.tripId?.toLowerCase().includes(q)
      const matchUserName = item.raisedByUser?.name?.toLowerCase().includes(q)
      const matchUserEmail = item.raisedByUser?.email?.toLowerCase().includes(q)
      const matchCompany = item.raisedByUser?.company?.toLowerCase().includes(q)
      const matchRoute = item.trip?.routeSummary?.toLowerCase().includes(q)
      return (
        matchDisplayId ||
        matchDisputeId ||
        matchTripId ||
        matchUserName ||
        matchUserEmail ||
        matchCompany ||
        matchRoute
      )
    })
  }

  return list
}

/**
 * Transition dispute from Raised -> UnderReview.
 * Strict state machine enforcement: Only 'Raised' can be moved to 'UnderReview'.
 */
export async function startReviewDispute(disputeId) {
  await new Promise((resolve) => setTimeout(resolve, 350))

  const index = currentDisputes.findIndex((d) => d.disputeId === disputeId)
  if (index === -1) {
    throw new Error(`Dispute not found: ${disputeId}`)
  }

  const dispute = currentDisputes[index]
  if (!canStartReview(dispute.status)) {
    throw new Error(
      `Invalid transition: Dispute #${dispute.displayId} is currently in '${dispute.status}' and cannot transition to 'UnderReview'.`,
    )
  }

  const updated = {
    ...dispute,
    status: DisputeStatus.UNDER_REVIEW,
    reviewStartedAt: new Date().toISOString(),
    reviewedBy: 'Admin (Operations)',
    updatedAt: new Date().toISOString(),
  }

  currentDisputes[index] = updated
  persistDisputes(currentDisputes)
  return updated
}

/**
 * Transition dispute from UnderReview -> Resolved.
 * Strict state machine enforcement:
 * 1. Must currently be in 'UnderReview' (cannot jump from 'Raised').
 * 2. Mandatory non-empty resolution note.
 */
export async function resolveDispute({ disputeId, outcome = 'Upheld', resolutionNote }) {
  await new Promise((resolve) => setTimeout(resolve, 450))

  const noteError = validateResolutionPayload(resolutionNote)
  if (noteError) {
    throw new Error(noteError)
  }

  const index = currentDisputes.findIndex((d) => d.disputeId === disputeId)
  if (index === -1) {
    throw new Error(`Dispute not found: ${disputeId}`)
  }

  const dispute = currentDisputes[index]
  if (!canResolveDispute(dispute.status)) {
    throw new Error(
      `Invalid state machine transition: Dispute #${dispute.displayId} is in '${dispute.status}'. It must be in 'UnderReview' before it can be marked as 'Resolved'.`,
    )
  }

  const updated = {
    ...dispute,
    status: DisputeStatus.RESOLVED,
    updatedAt: new Date().toISOString(),
    resolution: {
      outcome,
      notes: resolutionNote.trim(),
      resolvedAt: new Date().toISOString(),
      resolvedByUser: {
        name: 'Admin (Dispute Resolution)',
        email: 'admin.disputes@freightlink.lk',
      },
    },
  }

  currentDisputes[index] = updated
  persistDisputes(currentDisputes)
  return updated
}

/**
 * Helper to reset disputes back to initial mock set for demonstration.
 */
export async function resetMockDisputes() {
  currentDisputes = [...INITIAL_MOCK_DISPUTES]
  persistDisputes(currentDisputes)
  return currentDisputes
}

/* React Query Hooks */

export function useDisputesQuery(filters = {}) {
  return useQuery({
    queryKey: disputeKeys.list(filters),
    queryFn: () => fetchDisputes(filters),
  })
}

export function useStartReviewMutation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: startReviewDispute,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: disputeKeys.all })
    },
  })
}

export function useResolveDisputeMutation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: resolveDispute,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: disputeKeys.all })
    },
  })
}

export function useResetDisputesMutation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: resetMockDisputes,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: disputeKeys.all })
    },
  })
}
