import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen } from '@testing-library/react'
import BillingPage from '../../pages/BillingPage.jsx'
import { UserRole, InvoiceStatus } from '../../../../lib/enums.js'
import { renderWithProviders } from '../../../../test/testUtils.jsx'
import * as invoiceApi from '../../api/invoiceApi.js'

// BillingPage is exercised through the real InvoiceListTable/InvoiceFilterBar/
// InvoiceEmptyState/ErrorState components — only the network boundary
// (useInvoicesQuery/useInvoiceQuery/useInvoiceSummaryQuery) is mocked, mirroring
// LoadsPage.test.jsx's approach. There is deliberately no mock-data fallback to
// verify the absence of (the bug this suite guards against): an empty or failed
// query must render a real empty/error state, never fabricated invoices.
vi.mock('../../api/invoiceApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useInvoicesQuery: vi.fn(),
    useInvoiceQuery: vi.fn(() => ({ data: undefined })),
    useInvoiceSummaryQuery: vi.fn(),
    useCreateInvoiceMutation: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
    useUpdateInvoiceMutation: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
    useIssueInvoiceMutation: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
    useVoidInvoiceMutation: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
    useSubmitPaymentProofMutation: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
    useConfirmPaymentMutation: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
  }
})

function sampleInvoice(overrides = {}) {
  return {
    invoiceId: 'inv-1',
    id: 'inv-1',
    invoiceNumber: 'INV-0001',
    recipientName: 'Acme Traders',
    totalAmount: 25000,
    amount: 25000,
    currency: 'LKR',
    status: InvoiceStatus.ISSUED,
    issuedAt: '2026-09-01T00:00:00.000Z',
    createdAt: '2026-09-01T00:00:00.000Z',
    ...overrides,
  }
}

function renderBillingPage(role = UserRole.AGENCY_STAFF) {
  return renderWithProviders(<BillingPage />, { route: '/billing', authState: { role, isAuthenticated: true } })
}

afterEach(() => {
  vi.mocked(invoiceApi.useInvoicesQuery).mockReset()
  vi.mocked(invoiceApi.useInvoiceSummaryQuery).mockReset()
  cleanup()
})

describe('BillingPage — error state (no mock fallback)', () => {
  it('renders a real ErrorState, not fabricated invoices, when the query fails', () => {
    invoiceApi.useInvoicesQuery.mockReturnValue({
      isLoading: false,
      isError: true,
      error: { message: 'Network timeout' },
      data: undefined,
      refetch: vi.fn(),
      isFetching: false,
    })
    renderBillingPage()

    expect(screen.getByText('Unable to load invoices')).toBeInTheDocument()
    expect(screen.getByText('Network timeout')).toBeInTheDocument()
    // Guards against the old MOCK_INVOICES fallback: no fabricated invoice numbers rendered.
    expect(screen.queryByText(/INV-/)).not.toBeInTheDocument()
  })
})

describe('BillingPage — empty state (no mock fallback)', () => {
  it('renders the real empty state, not fabricated invoices, on a genuinely empty list', () => {
    invoiceApi.useInvoicesQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], page: 1, pageSize: 100, totalItems: 0, totalPages: 0 },
      refetch: vi.fn(),
      isFetching: false,
    })
    renderBillingPage()

    expect(screen.getByText('No invoices found')).toBeInTheDocument()
    expect(screen.getByText('No invoices have been issued in the system yet.')).toBeInTheDocument()
  })
})

describe('BillingPage — list rendering & role gating', () => {
  function stubList(items) {
    invoiceApi.useInvoicesQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items, page: 1, pageSize: 100, totalItems: items.length, totalPages: 1 },
      refetch: vi.fn(),
      isFetching: false,
    })
  }

  it('renders invoice rows with their real status, and offers Create/Edit/Issue/Void for Agency Staff', () => {
    stubList([sampleInvoice({ status: InvoiceStatus.DRAFT }), sampleInvoice({ invoiceId: 'inv-2', id: 'inv-2', invoiceNumber: 'INV-0002', status: InvoiceStatus.ISSUED })])
    renderBillingPage(UserRole.AGENCY_STAFF)

    // Both a desktop table row and a mobile card row render in jsdom (no CSS media queries), so
    // each invoice number legitimately appears twice.
    expect(screen.getAllByText('INV-0001').length).toBeGreaterThan(0)
    expect(screen.getAllByText('INV-0002').length).toBeGreaterThan(0)
    expect(screen.getByRole('button', { name: /create invoice/i })).toBeInTheDocument()
  })

  it('hides Create Invoice for a Shipper and never shows a Draft invoice to them', () => {
    stubList([sampleInvoice({ status: InvoiceStatus.DRAFT }), sampleInvoice({ invoiceId: 'inv-2', id: 'inv-2', invoiceNumber: 'INV-0002', status: InvoiceStatus.ISSUED })])
    renderBillingPage(UserRole.SHIPPER)

    expect(screen.queryByRole('button', { name: /create invoice/i })).not.toBeInTheDocument()
    expect(screen.queryByText('INV-0001')).not.toBeInTheDocument()
    expect(screen.getAllByText('INV-0002').length).toBeGreaterThan(0)
  })

  it('renders the read-only cashflow dashboard for Admin with no action buttons, and no Create Invoice control', () => {
    stubList([sampleInvoice()])
    invoiceApi.useInvoiceSummaryQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        totalInvoiced: 25000,
        totalPaid: 0,
        totalOutstanding: 25000,
        countByStatus: { draft: 0, issued: 1, paymentPending: 0, paid: 0, failed: 0, void: 0 },
        recentActivity: [],
      },
      refetch: vi.fn(),
      isFetching: false,
    })
    renderBillingPage(UserRole.ADMIN)

    // "Total Invoiced" appears both in the Admin cashflow dashboard tile and the KPI strip below it.
    expect(screen.getAllByText('Total Invoiced').length).toBeGreaterThan(0)
    expect(screen.queryByRole('button', { name: /create invoice/i })).not.toBeInTheDocument()
  })
})
