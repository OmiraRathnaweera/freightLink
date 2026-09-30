import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '../../../lib/api/api.js'
import { queryClient } from '../../../lib/api/queryClient.js'

// Query key factory — hierarchical so a broad invalidation (`invoiceKeys.lists()`)
// and a narrow one (`invoiceKeys.detail(id)`) can both be expressed. Mirrors the
// pattern in features/loads/api/loadsApi.js.
export const invoiceKeys = {
  all: ['invoices'],
  lists: () => [...invoiceKeys.all, 'list'],
  list: (params) => [...invoiceKeys.lists(), params],
  details: () => [...invoiceKeys.all, 'detail'],
  detail: (id) => [...invoiceKeys.details(), id],
  summary: () => [...invoiceKeys.all, 'summary'],
}

/** GET /invoices — paginated/filterable invoice list, scoped server-side by role. */
export async function fetchInvoices(params = {}) {
  return api.get('/invoices', { params })
}

/** GET /invoices/{id} — full invoice detail with line items and audit trail. */
export async function fetchInvoiceById(id) {
  return api.get(`/invoices/${id}`)
}

/** POST /invoices — create a Draft or Issued invoice with manual line items (Agent only). */
export async function createInvoice(payload) {
  return api.post('/invoices', payload)
}

/** PUT /invoices/{id} — edit a Draft invoice (Agent only). */
export async function updateInvoice(id, payload) {
  return api.put(`/invoices/${id}`, payload)
}

/** POST /invoices/{id}/issue — Draft -> Issued (Agent only). */
export async function issueInvoice(id) {
  return api.post(`/invoices/${id}/issue`)
}

/** POST /invoices/{id}/void — void with a mandatory reason (Agent only). */
export async function voidInvoice(id, voidReason) {
  return api.post(`/invoices/${id}/void`, { voidReason })
}

/**
 * Submits an already-uploaded file (via POST /files/single) as the Shipper's proof-of-payment
 * receipt for an invoice. Advances the invoice to PaymentPending for Agency review.
 */
export async function uploadPaymentProof(id, publicId) {
  return api.post(`/invoices/${id}/payment-proof`, { publicId })
}

/** Confirms a Shipper's submitted payment receipt and closes the invoice as Paid (Agency only). */
export async function confirmPayment(id) {
  return api.post(`/invoices/${id}/confirm-payment`)
}

/** GET /invoices/summary — Admin-only, read-only cashflow aggregate. */
export async function fetchInvoiceSummary() {
  return api.get('/invoices/summary')
}

// --- TanStack Query hooks ---

/** List query, e.g. `useInvoicesQuery({ status: 'Issued' })`. */
export function useInvoicesQuery(params, options) {
  return useQuery({ queryKey: invoiceKeys.list(params), queryFn: () => fetchInvoices(params), ...options })
}

/** Detail query — disabled until an `id` is available. */
export function useInvoiceQuery(id, options) {
  return useQuery({
    queryKey: invoiceKeys.detail(id),
    queryFn: () => fetchInvoiceById(id),
    enabled: Boolean(id),
    ...options,
  })
}

/** Admin-only cashflow summary query. */
export function useInvoiceSummaryQuery(options) {
  return useQuery({ queryKey: invoiceKeys.summary(), queryFn: fetchInvoiceSummary, ...options })
}

/** Create mutation — invalidates the list + summary caches on success. */
export function useCreateInvoiceMutation(options) {
  return useMutation({
    mutationFn: createInvoice,
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: invoiceKeys.lists() })
      queryClient.invalidateQueries({ queryKey: invoiceKeys.summary() })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}

// The remaining mutations back a list page (many rows, one action each) rather than a single
// detail page, so — unlike the Loads feature's per-id hooks — the target id travels in the
// mutate() variables instead of being bound at hook-creation time.

/** Update mutation — call as `mutate({ id, payload })`. Invalidates that invoice's detail + the list. */
export function useUpdateInvoiceMutation(options) {
  return useMutation({
    mutationFn: ({ id, payload }) => updateInvoice(id, payload),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: invoiceKeys.lists() })
      queryClient.invalidateQueries({ queryKey: invoiceKeys.detail(variables.id) })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}

/** Issue mutation — call as `mutate(id)`. Invalidates that invoice's detail + list + summary. */
export function useIssueInvoiceMutation(options) {
  return useMutation({
    mutationFn: (id) => issueInvoice(id),
    onSuccess: (data, id, context) => {
      queryClient.invalidateQueries({ queryKey: invoiceKeys.lists() })
      queryClient.invalidateQueries({ queryKey: invoiceKeys.detail(id) })
      queryClient.invalidateQueries({ queryKey: invoiceKeys.summary() })
      options?.onSuccess?.(data, id, context)
    },
    ...options,
  })
}

/** Void mutation — call as `mutate({ id, reason })`. Invalidates that invoice's detail + list + summary. */
export function useVoidInvoiceMutation(options) {
  return useMutation({
    mutationFn: ({ id, reason }) => voidInvoice(id, reason),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: invoiceKeys.lists() })
      queryClient.invalidateQueries({ queryKey: invoiceKeys.detail(variables.id) })
      queryClient.invalidateQueries({ queryKey: invoiceKeys.summary() })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}

/** Payment-proof-submission mutation (Shipper) — call as `mutate({ id, publicId })`. */
export function useSubmitPaymentProofMutation(options) {
  return useMutation({
    mutationFn: ({ id, publicId }) => uploadPaymentProof(id, publicId),
    onSuccess: (data, variables, context) => {
      queryClient.invalidateQueries({ queryKey: invoiceKeys.lists() })
      queryClient.invalidateQueries({ queryKey: invoiceKeys.detail(variables.id) })
      options?.onSuccess?.(data, variables, context)
    },
    ...options,
  })
}

/** Confirm-payment mutation (Agent) — call as `mutate(id)`. Invalidates list + detail + summary. */
export function useConfirmPaymentMutation(options) {
  return useMutation({
    mutationFn: (id) => confirmPayment(id),
    onSuccess: (data, id, context) => {
      queryClient.invalidateQueries({ queryKey: invoiceKeys.lists() })
      queryClient.invalidateQueries({ queryKey: invoiceKeys.detail(id) })
      queryClient.invalidateQueries({ queryKey: invoiceKeys.summary() })
      options?.onSuccess?.(data, id, context)
    },
    ...options,
  })
}
