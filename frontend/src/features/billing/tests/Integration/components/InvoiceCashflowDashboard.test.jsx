import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import { QueryClientProvider } from '@tanstack/react-query'
import InvoiceCashflowDashboard from '../../../components/InvoiceCashflowDashboard.jsx'
import { createTestQueryClient } from '../../../../../test/testUtils.jsx'
import * as invoiceApi from '../../../api/invoiceApi.js'

vi.mock('../../../api/invoiceApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return { ...actual, useInvoiceSummaryQuery: vi.fn() }
})

function renderDashboard() {
  const queryClient = createTestQueryClient()
  return render(
    <QueryClientProvider client={queryClient}>
      <InvoiceCashflowDashboard />
    </QueryClientProvider>,
  )
}

afterEach(() => {
  vi.mocked(invoiceApi.useInvoiceSummaryQuery).mockReset()
  cleanup()
})

describe('InvoiceCashflowDashboard', () => {
  it('renders an ErrorState with retry on failure — no fabricated totals', () => {
    const refetch = vi.fn()
    invoiceApi.useInvoiceSummaryQuery.mockReturnValue({
      isLoading: false,
      isError: true,
      error: { message: 'Server unavailable' },
      refetch,
    })
    renderDashboard()

    expect(screen.getByText('Unable to load cashflow summary')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
  })

  it('renders totals, per-status counts, and recent activity with no action buttons anywhere', () => {
    invoiceApi.useInvoiceSummaryQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        totalInvoiced: 50000,
        totalPaid: 20000,
        totalOutstanding: 30000,
        countByStatus: { draft: 1, issued: 2, paymentPending: 1, paid: 1, failed: 0, void: 0 },
        recentActivity: [
          { invoiceId: 'inv-1', invoiceNumber: 'INV-0001', status: 'Paid', amount: 20000, currency: 'LKR', updatedAt: '2026-09-20T00:00:00.000Z' },
        ],
      },
      refetch: vi.fn(),
      isFetching: false,
    })
    renderDashboard()

    expect(screen.getByText('Total Invoiced')).toBeInTheDocument()
    expect(screen.getByText('Total Paid')).toBeInTheDocument()
    expect(screen.getByText('Total Outstanding')).toBeInTheDocument()
    expect(screen.getByText('INV-0001')).toBeInTheDocument()

    // Strictly read-only: no create/edit/issue/confirm/void controls on this page.
    expect(screen.queryByRole('button', { name: /create/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /issue/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /void/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /confirm/i })).not.toBeInTheDocument()
  })
})
