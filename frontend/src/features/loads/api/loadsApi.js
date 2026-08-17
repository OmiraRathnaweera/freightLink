import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '../../../lib/api/api.js'
import { axiosClient } from '../../../lib/api/axiosClient.js'
import { queryClient } from '../../../lib/api/queryClient.js'

// Query key factory — hierarchical so a broad invalidation
// (`loadKeys.lists()`) and a narrow one (`loadKeys.detail(id)`) can both
// be expressed. See docs/load-management-api.md Section 3.
export const loadKeys = {
  all: ['loads'],
  lists: () => [...loadKeys.all, 'list'],
  list: (params) => [...loadKeys.lists(), params],
  details: () => [...loadKeys.all, 'detail'],
  detail: (id) => [...loadKeys.details(), id],
  files: (loadId) => [...loadKeys.detail(loadId), 'files'],
  statusHistory: (id) => [...loadKeys.detail(id), 'status-history'],
}

/**
 * GET /loads — paginated/filterable list (Section 3.3).
 * @param {{ page?: number, pageSize?: number, status?: string, search?: string, sortBy?: string, sortDir?: 'asc'|'desc', createdFrom?: string, createdTo?: string }} [params]
 * @returns {Promise<{ items: object[], page: number, pageSize: number, totalItems: number, totalPages: number }>}
 */
export async function listLoads(params) {
  return api.get('/loads', { params })
}

/** GET /loads/{id} — full LoadResponseDto (Section 3.6). */
export async function getLoad(id) {
  return api.get(`/loads/${id}`)
}

/** POST /loads — create a load (Section 3.1). */
export async function createLoad(data) {
  return api.post('/loads', data)
}

/** PUT /loads/{id} — edit a load, only while Draft/Posted (Section 3.4). */
export async function updateLoad(id, data) {
  return api.put(`/loads/${id}`, data)
}

/** PATCH /loads/{id}/cancel — cancel a load (Section 3.5). */
export async function cancelLoad(id, { reason }) {
  return api.patch(`/loads/${id}/cancel`, { reason })
}

/**
 * GET /loads/{id}/status-history — full status transition timeline. NOT
 * YET IMPLEMENTED on the backend as of this writing (confirmed directly
 * against backend/Controllers/LoadsController.cs — no matching route —
 * and docs/api-contract-openapi-skeleton.md:149, which marks it "not yet
 * implemented"). The LoadStatusHistory table exists and is written to on
 * every transition (ADR-019); there's just no route serving it yet, so
 * this call 404s today. The shape below is inferred from
 * backend/Entities/LoadStatusHistory.cs's fields under this project's
 * camelCase convention — verify against the real response once the
 * endpoint ships, field names aren't contract-guaranteed yet.
 * @returns {Promise<{ loadStatusHistoryId: string, loadId: string, changedByUserId: string, fromStatus: string|null, toStatus: string, reason: string|null, changedAt: string }[]>}
 */
export async function getLoadStatusHistory(id) {
  return api.get(`/loads/${id}/status-history`)
}

/**
 * POST /files/single — the upload half of the two-step attach flow
 * (Section 4.0). Goes through `axiosClient` directly, not the JSON-only
 * `api.post` helper, since this needs a `multipart/form-data` body — axios
 * detects the `FormData` payload and clears the instance's default
 * `Content-Type: application/json` header itself, letting the browser set
 * the correct header with its boundary.
 */
export async function uploadFile(file) {
  const formData = new FormData()
  formData.append('file', file)
  const response = await axiosClient.post('/files/single', formData)
  return response.data
}

/** GET /loads/{loadId}/files — plain array, not a paging envelope (Section 4.2). */
export async function listLoadFiles(loadId) {
  return api.get(`/loads/${loadId}/files`)
}

/** POST /loads/{loadId}/files — attach an uploaded file's publicId (Section 4.1). */
export async function attachLoadFile(loadId, { publicId, fileType }) {
  return api.post(`/loads/${loadId}/files`, { publicId, fileType })
}

/** DELETE /loads/{loadId}/files/{fileId} — detach only, upload itself is untouched (Section 4.3). */
export async function detachLoadFile(loadId, fileId) {
  return api.delete(`/loads/${loadId}/files/${fileId}`)
}

/** List query, e.g. `useLoadsQuery({ page: 1, status: 'Posted' })`. */
export function useLoadsQuery(params, options) {
  return useQuery({ queryKey: loadKeys.list(params), queryFn: () => listLoads(params), ...options })
}

/** Detail query — disabled until an `id` is available. */
export function useLoadDetailQuery(id, options) {
  return useQuery({
    queryKey: loadKeys.detail(id),
    queryFn: () => getLoad(id),
    enabled: Boolean(id),
    ...options,
  })
}

/** Create mutation — invalidates the list cache on success. */
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

/** Update mutation, scoped to one load — invalidates its detail + the list cache. */
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

/** Cancel mutation, scoped to one load — invalidates its detail + the list cache. */
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

/** Status-history query — see getLoadStatusHistory's comment: 404s until the backend endpoint ships. */
export function useLoadStatusHistoryQuery(id, options) {
  return useQuery({
    queryKey: loadKeys.statusHistory(id),
    queryFn: () => getLoadStatusHistory(id),
    enabled: Boolean(id),
    retry: false, // the route doesn't exist yet — no point retrying a 404
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

/** Upload mutation — step 1 of the attach flow, no cache to invalidate on its own. */
export function useUploadFileMutation(options) {
  return useMutation({ mutationFn: uploadFile, ...options })
}

/** Attach mutation, scoped to one load — invalidates its files list. */
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

/** Detach mutation, scoped to one load — invalidates its files list. */
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
