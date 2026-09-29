import { afterEach, describe, expect, it, vi } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClientProvider } from '@tanstack/react-query'
import { api } from '../../../../../lib/api/api.js'
import { queryClient } from '../../../../../lib/api/queryClient.js'
import {
  invoiceKeys,
  useIssueInvoiceMutation,
  useVoidInvoiceMutation,
  useConfirmPaymentMutation,
} from '../../../api/invoiceApi.js'

vi.mock('../../../../../lib/api/api.js', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() },
}))

// The mutation hooks invalidate the app-wide `queryClient` singleton directly
// (see lib/api/queryClient.js), not necessarily the QueryClient handed to the
// provider — so the spy must sit on that same singleton instance.
function wrapper({ children }) {
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
}

afterEach(() => {
  vi.mocked(api.post).mockReset()
  vi.restoreAllMocks()
})

describe('invoice mutation hooks — cache invalidation', () => {
  it('useIssueInvoiceMutation invalidates the invoice list, its detail, and the summary', async () => {
    api.post.mockResolvedValue({ invoiceId: 'inv-1', invoiceNumber: 'INV-0001', status: 'Issued' })
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries')

    const { result } = renderHook(() => useIssueInvoiceMutation(), { wrapper })
    result.current.mutate('inv-1')

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: invoiceKeys.lists() })
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: invoiceKeys.detail('inv-1') })
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: invoiceKeys.summary() })
  })

  it('useVoidInvoiceMutation invalidates the invoice list, its detail, and the summary', async () => {
    api.post.mockResolvedValue({ invoiceId: 'inv-1', invoiceNumber: 'INV-0001', status: 'Void' })
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries')

    const { result } = renderHook(() => useVoidInvoiceMutation(), { wrapper })
    result.current.mutate({ id: 'inv-1', reason: 'Customer cancelled order' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: invoiceKeys.lists() })
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: invoiceKeys.detail('inv-1') })
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: invoiceKeys.summary() })
  })

  it('useConfirmPaymentMutation invalidates the invoice list, its detail, and the summary', async () => {
    api.post.mockResolvedValue({ invoiceId: 'inv-1', invoiceNumber: 'INV-0001', status: 'Paid' })
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries')

    const { result } = renderHook(() => useConfirmPaymentMutation(), { wrapper })
    result.current.mutate('inv-1')

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: invoiceKeys.lists() })
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: invoiceKeys.detail('inv-1') })
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: invoiceKeys.summary() })
  })
})
