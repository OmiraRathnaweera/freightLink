import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '../../../lib/api/api.js'
import { axiosClient } from '../../../lib/api/axiosClient.js'
import { queryClient } from '../../../lib/api/queryClient.js'

// Query key factory â€” hierarchical so a broad invalidation
// (`loadKeys.lists()`) and a narrow one (`loadKeys.detail(id)`) can both
// be expressed. See docs/load-management-api.md Section 3.
export const loadKeys = {
  all: ['loads'],
  lists: () => [...loadKeys.all, 'list'],
  list: (params) => [...loadKeys.lists(), params],
  details: () => [...loadKeys.all, 'detail'],
  detail: (id) => [...loadKeys.details(), id],
  files: (loadId) => [...loadKeys.detail(loadId), 'files'],
}

/**
 * @typedef {object} LoadListItem
 * @property {string} loadId
 * @property {string} referenceCode
 * @property {string} shipperUserId
 * @property {string} shipperName - Owner's display name; resolved server-side, never fetched separately (no /users/{id} endpoint exists).
 * @property {string} cargoDescription
 * @property {number} weightKg
 * @property {string} pickupAddress
 * @property {string} dropoffAddress
 * @property {string} pickupWindowStart
 * @property {string} pickupWindowEnd
 * @property {number|null} estimatedPrice
 * @property {string} status
 * @property {boolean} hasTrip - True once a Trip has been dispatched for this load's assignment; status alone stays 'Matched' through dispatch, so this is what gates a one-time "Dispatch" action.
 * @property {string} createdAt
 */

/**
 * GET /loads â€” paginated/filterable list (Section 3.3).
 * @param {{ page?: number, pageSize?: number, status?: string, search?: string, sortBy?: string, sortDir?: 'asc'|'desc', createdFrom?: string, createdTo?: string }} [params]
 * @returns {Promise<{ items: LoadListItem[], page: number, pageSize: number, totalItems: number, totalPages: number }>}
 */
export async function listLoads(params) {
  return api.get('/loads', { params })
}

/**
 * @typedef {object} LoadStatusHistoryEntry
 * @property {string} loadStatusHistoryId
 * @property {string|null} fromStatus - null only for the load's very first status row.
 * @property {string} toStatus
 * @property {string|null} reason - only ever set for transitions that require one (e.g. cancellation).
 * @property {string} changedByUserId
 * @property {string} changedAt
 */

/**
 * GET /loads/{id} â€” full LoadResponseDto (Section 3.6), extends
 * LoadListItem with shipperUserId/shipperName (both present here too) plus
 * volumeM3, lat/lng, workflowRunId, updatedAt, and statusHistory. There is
 * no separate status-history endpoint â€” the full transition timeline
 * (newest first) rides along on this response only; POST/PUT/PATCH
 * .../status responses leave statusHistory as an empty array, since the
 * caller already knows the single transition it just made.
 * @returns {Promise<LoadListItem & { volumeM3: number, pickupLat: number, pickupLng: number, dropoffLat: number, dropoffLng: number, workflowRunId: string|null, updatedAt: string, statusHistory: LoadStatusHistoryEntry[] }>}
 */
export async function getLoad(id) {
  return api.get(`/loads/${id}`)
}

/** POST /loads â€” create a load (Section 3.1). */
export async function createLoad(data) {
  return api.post('/loads', data)
}

/** PUT /loads/{id} â€” edit a load, only while Draft/Posted (Section 3.4). */
export async function updateLoad(id, data) {
  return api.put(`/loads/${id}`, data)
}

/**
 * PATCH /loads/{id}/status â€” the single endpoint for every Shipper-initiated
 * load status change (Section 3.5). Only `status: 'Posted'` (publish) and
 * `status: 'Cancelled'` (cancel, `reason` required) are accepted â€” every
 * later status is set only by internal processes, never from the client.
 */
export async function changeLoadStatus(id, { status, reason }) {
  return api.patch(`/loads/${id}/status`, { status, reason })
}

/** Cancels a load via `changeLoadStatus`. */
export async function cancelLoad(id, { reason }) {
  return changeLoadStatus(id, { status: 'Cancelled', reason })
}

/** Publishes a Draft load via `changeLoadStatus`. */
export async function publishLoad(id) {
  return changeLoadStatus(id, { status: 'Posted' })
}

/**
 * POST /loads/{id}/estimate â€” a rough, pre-matching price quote using the load's own
 * pickup/dropoff coordinates (haversine distance, no routing call). Preview only: it is not
 * persisted, and is distinct from the load's own `estimatedPrice` field (the AI agent's price).
 */
export async function estimateLoadPrice(id) {
  return api.post(`/loads/${id}/estimate`)
}

/**
 * POST /files/single â€” the upload half of the two-step attach flow
 * (Section 4.0). Goes through `axiosClient` directly, not the JSON-only
 * `api.post` helper, since this needs a `multipart/form-data` body â€” axios
 * detects the `FormData` payload and clears the instance's default
 * `Content-Type: application/json` header itself, letting the browser set
 * the correct header with its boundary.
 */
export async function uploadFile(file) {
  const formData = new FormData()
  formData.append('file', file)
  const response = await axiosClient.post('/files/single', formData, { headers: { 'Content-Type': undefined } })
  return response.data
}

/** GET /loads/{loadId}/files â€” plain array, not a paging envelope (Section 4.2). */
export async function listLoadFiles(loadId) {
  return api.get(`/loads/${loadId}/files`)
}

/** POST /loads/{loadId}/files â€” attach an uploaded file's publicId (Section 4.1). */
export async function attachLoadFile(loadId, { publicId, fileType }) {
  return api.post(`/loads/${loadId}/files`, { publicId, fileType })
}

/** DELETE /loads/{loadId}/files/{fileId} â€” detach only, upload itself is untouched (Section 4.3). */
export async function detachLoadFile(loadId, fileId) {
  return api.delete(`/loads/${loadId}/files/${fileId}`)
}

/** List query, e.g. `useLoadsQuery({ page: 1, status: 'Posted' })`. */
export function useLoadsQuery(params, options) {
  return useQuery({ queryKey: loadKeys.list(params), queryFn: () => listLoads(params), ...options })
}

/** Detail query â€” disabled until an `id` is available. */
export function useLoadDetailQuery(id, options) {
  return useQuery({
    queryKey: loadKeys.detail(id),
    queryFn: () => getLoad(id),
    enabled: Boolean(id),
    ...options,
  })
}

/** Create mutation â€” invalidates the list cache on success. */
export function useCreateLoadMutation(options) {
  return useMutation({
    mutationFn: createLoad,
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: loadKeys.lists() })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}

/** Update mutation, scoped to one load â€” invalidates its detail + the list cache. */
export function useUpdateLoadMutation(id, options) {
  return useMutation({
    mutationFn: (data) => updateLoad(id, data),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: loadKeys.lists() })
      queryClient.invalidateQueries({ queryKey: loadKeys.detail(id) })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}

/** Cancel mutation, scoped to one load â€” invalidates its detail + the list cache. */
export function useCancelLoadMutation(id, options) {
  return useMutation({
    mutationFn: ({ reason }) => cancelLoad(id, { reason }),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: loadKeys.lists() })
      queryClient.invalidateQueries({ queryKey: loadKeys.detail(id) })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}

/** Publish mutation, scoped to one load â€” invalidates its detail + the list cache. */
export function usePublishLoadMutation(id, options) {
  return useMutation({
    mutationFn: () => publishLoad(id),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: loadKeys.lists() })
      queryClient.invalidateQueries({ queryKey: loadKeys.detail(id) })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}

/** Price-estimate mutation, scoped to one load â€” no cache invalidation, since nothing is persisted. */
export function useEstimateLoadPriceMutation(id, options) {
  return useMutation({
    mutationFn: () => estimateLoadPrice(id),
    ...options,
  })
}

/** Files-list query for a load's attachments. */
export function useLoadFilesQuery(loadId, options) {
  return useQuery({
    queryKey: loadKeys.files(loadId),
    queryFn: () => listLoadFiles(loadId),
    enabled: Boolean(loadId),
    ...options,
  })
}

/** Upload mutation â€” step 1 of the attach flow, no cache to invalidate on its own. */
export function useUploadFileMutation(options) {
  return useMutation({ mutationFn: uploadFile, ...options })
}

/** Attach mutation, scoped to one load â€” invalidates its files list. */
export function useAttachLoadFileMutation(loadId, options) {
  return useMutation({
    mutationFn: (payload) => attachLoadFile(loadId, payload),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: loadKeys.files(loadId) })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}

/** Detach mutation, scoped to one load â€” invalidates its files list. */
export function useDetachLoadFileMutation(loadId, options) {
  return useMutation({
    mutationFn: (fileId) => detachLoadFile(loadId, fileId),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: loadKeys.files(loadId) })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}

