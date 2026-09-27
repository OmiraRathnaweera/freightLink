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

/**
 * GET /agencies/verification-queue
 * Admin-only: every Pending agency bundled with the compliance documents it
 * has uploaded so far, in one call â€” avoids an N+1 round-trip per agency.
 */
export async function getVerificationQueue() {
  return api.get('/agencies/verification-queue')
}

export function useVerificationQueueQuery(options) {
  return useQuery({
    queryKey: ['agencies', 'verification-queue'],
    queryFn: getVerificationQueue,
    ...options,
  })
}


export async function getComplianceDocs(agencyId) {
  return api.get(`/agencies/${agencyId}/compliance-docs`)
}

export async function addComplianceDoc({ agencyId, doc }) {
  return api.post(`/agencies/${agencyId}/compliance-docs`, doc)
}

/**
 * PUT /agencies/{id}/compliance-docs/{docId}
 * Replaces an existing document's file/number/dates in place (re-upload/renewal), rather than
 * inserting a new row â€” reusing addComplianceDoc for this would 500 while the existing document
 * is still Pending/Verified (one-live-document-per-type constraint).
 */
export async function updateComplianceDoc({ agencyId, docId, doc }) {
  return api.put(`/agencies/${agencyId}/compliance-docs/${docId}`, doc)
}

export async function getVehicles(agencyId) {
  return api.get(`/agencies/${agencyId}/vehicles`)
}

export async function addVehicle({ agencyId, vehicle }) {
  return api.post(`/agencies/${agencyId}/vehicles`, vehicle)
}

export async function getDrivers(agencyId) {
  return api.get(`/agencies/${agencyId}/drivers`)
}

export async function addDriver({ agencyId, driver }) {
  return api.post(`/agencies/${agencyId}/drivers`, driver)
}

/**
 * PATCH /agencies/{id}/drivers/{driverId}/status
 * Removes ("Inactive") or reinstates ("Active") a driver on the agency's roster. OnTrip is
 * trip-execution-owned and cannot be set here.
 */
export async function updateDriverStatus({ agencyId, driverId, status }) {
  return api.patch(`/agencies/${agencyId}/drivers/${driverId}/status`, { status })
}

/**
 * PUT /agencies/{id}/drivers/{driverId}
 * Updates a driver's editable details (fullName, licenceNo, licenceExpiry). Email/status are
 * out of scope here — email is immutable after creation and status has its own endpoint above.
 */
export async function updateDriver({ agencyId, driverId, driver }) {
  return api.put(`/agencies/${agencyId}/drivers/${driverId}`, driver)
}

export function useDriversQuery(agencyId, options) {
  return useQuery({
    queryKey: ['agencies', agencyId, 'drivers'],
    queryFn: () => getDrivers(agencyId),
    ...options,
  })
}

export function useAddDriverMutation({ onSuccess, ...options } = {}) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: addDriver,
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: ['agencies', variables.agencyId, 'drivers'] })
      if (onSuccess) onSuccess(data, variables, context)
    },
    ...options,
  })
}

export function useUpdateDriverStatusMutation({ onSuccess, ...options } = {}) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: updateDriverStatus,
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: ['agencies', variables.agencyId, 'drivers'] })
      if (onSuccess) onSuccess(data, variables, context)
    },
    ...options,
  })
}

export function useUpdateDriverMutation({ onSuccess, ...options } = {}) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: updateDriver,
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: ['agencies', variables.agencyId, 'drivers'] })
      if (onSuccess) onSuccess(data, variables, context)
    },
    ...options,
  })
}

export function useComplianceDocsQuery(agencyId, options) {
  return useQuery({
    queryKey: ['agencies', agencyId, 'compliance-docs'],
    queryFn: () => getComplianceDocs(agencyId),
    ...options
  })
}

export function useAddComplianceDocMutation({ onSuccess, ...options } = {}) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: addComplianceDoc,
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: ['agencies', variables.agencyId, 'compliance-docs'] })
      if (onSuccess) onSuccess(data, variables, context)
    },
    ...options
  })
}

export function useUpdateComplianceDocMutation({ onSuccess, ...options } = {}) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: updateComplianceDoc,
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: ['agencies', variables.agencyId, 'compliance-docs'] })
      if (onSuccess) onSuccess(data, variables, context)
    },
    ...options
  })
}

/**
 * POST /agencies/{id}/compliance-docs/{docId}/verify
 * POST /agencies/{id}/compliance-docs/{docId}/reject
 * Admin-only: moves a Pending compliance document to Verified/Rejected.
 */
export async function verifyComplianceDoc({ agencyId, docId }) {
  return api.post(`/agencies/${agencyId}/compliance-docs/${docId}/verify`)
}

export async function rejectComplianceDoc({ agencyId, docId }) {
  return api.post(`/agencies/${agencyId}/compliance-docs/${docId}/reject`)
}

function invalidateComplianceDocQueries(queryClient, agencyId) {
  queryClient.invalidateQueries({ queryKey: ['agencies', agencyId, 'compliance-docs'] })
  queryClient.invalidateQueries({ queryKey: ['agencies', 'verification-queue'] })
}

export function useVerifyComplianceDocMutation({ onSuccess, ...options } = {}) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: verifyComplianceDoc,
    onSuccess: (data, variables, context) => {
      invalidateComplianceDocQueries(queryClient, variables.agencyId)
      if (onSuccess) onSuccess(data, variables, context)
    },
    ...options
  })
}

export function useRejectComplianceDocMutation({ onSuccess, ...options } = {}) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: rejectComplianceDoc,
    onSuccess: (data, variables, context) => {
      invalidateComplianceDocQueries(queryClient, variables.agencyId)
      if (onSuccess) onSuccess(data, variables, context)
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

export function useAddVehicleMutation({ onSuccess, ...options } = {}) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: addVehicle,
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: ['agencies', variables.agencyId, 'vehicles'] })
      if (onSuccess) onSuccess(data, variables, context)
    },
    ...options
  })
}

/**
 * GET /agencies/my/fleet or /agencies/{agencyId}/fleet
 * Combined vehicles + drivers roster for the caller's own agency, or (Admin)
 * a specified agency — backs the Admin dashboard's per-agency fleet drill-down.
 */
export async function getAgencyFleet(agencyId) {
  if (agencyId) {
    return api.get(`/agencies/${agencyId}/fleet`)
  }
  return api.get('/agencies/my/fleet')
}

export function useAgencyFleetQuery(agencyId, options) {
  return useQuery({
    queryKey: ['agencies', agencyId ?? 'my', 'fleet'],
    queryFn: () => getAgencyFleet(agencyId),
    ...options,
  })
}
