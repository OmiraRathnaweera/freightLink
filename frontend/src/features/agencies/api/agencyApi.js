import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
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
 * GET /agencies/{id}
 */
export async function getAgency(agencyId) {
  return api.get(`/agencies/${agencyId}`)
}

export function useAgencyQuery(agencyId, options) {
  return useQuery({
    queryKey: agencyKeys.detail(agencyId),
    queryFn: () => getAgency(agencyId),
    ...options,
  })
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


export async function getComplianceDocs(agencyId) {
  return api.get(`/agencies/${agencyId}/compliance-docs`)
}

export async function addComplianceDoc({ agencyId, doc }) {
  return api.post(`/agencies/${agencyId}/compliance-docs`, doc)
}

export async function getVehicles(agencyId) {
  return api.get(`/agencies/${agencyId}/vehicles`)
}

export async function addVehicle({ agencyId, vehicle }) {
  return api.post(`/agencies/${agencyId}/vehicles`, vehicle)
}

export function useComplianceDocsQuery(agencyId, options) {
  return useQuery({
    queryKey: ['agencies', agencyId, 'compliance-docs'],
    queryFn: () => getComplianceDocs(agencyId),
    ...options
  })
}

export function useAddComplianceDocMutation(options) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: addComplianceDoc,
    onSuccess: (data, variables) => {
      queryClient.invalidateQueries({ queryKey: ['agencies', variables.agencyId, 'compliance-docs'] })
    },
    ...options
  })
}

export function useVehiclesQuery(agencyId, options) {
  return useQuery({
    queryKey: ['agencies', agencyId, 'vehicles'],
    queryFn: () => getVehicles(agencyId),
    ...options
  })
}

export function useAddVehicleMutation(options) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: addVehicle,
    onSuccess: (data, variables) => {
      queryClient.invalidateQueries({ queryKey: ['agencies', variables.agencyId, 'vehicles'] })
    },
    ...options
  })
}

