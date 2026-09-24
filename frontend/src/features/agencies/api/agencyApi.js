import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '../../../lib/api/api.js'

export const agencyKeys = {
  all: ['agencies'],
  list: (filters) => [...agencyKeys.all, { filters }],
  detail: (id) => [...agencyKeys.all, id],
}

/**
 * GET /agencies
 * @param {Object} queryParams e.g. { status: 'Pending' }
 */
export async function getAgencies(queryParams) {
  return api.get('/agencies', { params: queryParams })
}

/**
 * POST /agencies/{id}/verify
 */
export async function verifyAgency(agencyId) {
  return api.post(`/agencies/${agencyId}/verify`)
}

/**
 * POST /agencies/{id}/suspend
 */
/**
 * POST /agencies/{id}/activate
 */
export async function activateAgency(agencyId) {
  return api.post('/agencies/' + agencyId + '/activate')
}

/**
 * POST /agencies/{id}/suspend
 */
export async function suspendAgency(agencyId) {
  return api.post(`/agencies/${agencyId}/suspend`)
}

export function useAgenciesQuery(queryParams, options) {
  return useQuery({
    queryKey: agencyKeys.list(queryParams),
    queryFn: () => getAgencies(queryParams),
    ...options,
  })
}

export function useVerifyAgencyMutation(options) {
  return useMutation({
    mutationFn: verifyAgency,
    ...options,
  })
}

export function useActivateAgencyMutation(options) {
  return useMutation({
    mutationFn: activateAgency,
    ...options,
  })
}

export function useSuspendAgencyMutation(options) {
  return useMutation({
    mutationFn: suspendAgency,
    ...options,
  })
}

