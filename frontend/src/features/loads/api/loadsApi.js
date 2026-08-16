import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '../../../lib/api/api.js'
import { queryClient } from '../../../lib/api/queryClient.js'

// Query key factory — hierarchical so a broad invalidation
// (`loadKeys.lists()`) and a narrow one (`loadKeys.detail(id)`) can both
// be expressed. See api-contract-openapi-skeleton.md Section 4.2.
export const loadKeys = {
  all: ['loads'],
  lists: () => [...loadKeys.all, 'list'],
  list: (params) => [...loadKeys.lists(), params],
  details: () => [...loadKeys.all, 'detail'],
  detail: (id) => [...loadKeys.details(), id],
}

/**
 * GET /loads — paginated/filterable list.
 * @param {{ page?: number, pageSize?: number, status?: string, search?: string, sortBy?: string, sortDir?: 'asc'|'desc' }} [params]
 * @returns {Promise<{ items: object[], page: number, pageSize: number, totalItems: number, totalPages: number }>}
 */
export async function listLoads(params) {
  return api.get('/loads', { params })
}

/** GET /loads/{id} — load detail incl. status timeline and workflowRunId. */
export async function getLoad(id) {
  return api.get(`/loads/${id}`)
}

/** POST /loads — create a load. */
export async function createLoad(data) {
  return api.post('/loads', data)
}

/** Example list query, e.g. `useLoadsQuery({ page: 1, status: 'Posted' })`. */
export function useLoadsQuery(params, options) {
  return useQuery({ queryKey: loadKeys.list(params), queryFn: () => listLoads(params), ...options })
}

/** Example detail query — disabled until an `id` is available. */
export function useLoadDetailQuery(id, options) {
  return useQuery({
    queryKey: loadKeys.detail(id),
    queryFn: () => getLoad(id),
    enabled: Boolean(id),
    ...options,
  })
}

/** Example create mutation — invalidates the list cache on success. */
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
