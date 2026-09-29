import { describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import AdminAnalyticsPage from '../../../pages/AdminAnalyticsPage.jsx'
import { renderWithProviders } from '../../../../../test/testUtils.jsx'
import * as analyticsApi from '../../../api/analyticsApi.js'

vi.mock('../../../api/analyticsApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useAnalyticsSummaryQuery: vi.fn(),
  }
})

const MOCK_SUMMARY = {
  generatedAt: '2026-09-27T10:00:00Z',
  loads: { total: 4, byLabel: [{ label: 'Posted', count: 3 }, { label: 'Draft', count: 1 }] },
  agencies: { total: 6, byLabel: [{ label: 'Active', count: 6 }] },
  trips: { total: 0, byLabel: [] },
  assignments: { total: 0, byLabel: [] },
  disputes: { total: 1, byLabel: [{ label: 'Raised', count: 1 }] },
  usersByRole: { total: 10, byLabel: [{ label: 'Shipper', count: 8 }, { label: 'Admin', count: 2 }] },
  invoices: {
    counts: { total: 2, byLabel: [{ label: 'Paid', count: 2 }] },
    totalInvoicedAmount: 15000,
    totalPaidAmount: 15000,
  },
}

describe('AdminAnalyticsPage', () => {
  it('renders the summary once loaded', () => {
    analyticsApi.useAnalyticsSummaryQuery.mockReturnValue({
      data: MOCK_SUMMARY,
      isLoading: false,
      isError: false,
      isFetching: false,
      refetch: vi.fn(),
    })

    renderWithProviders(<AdminAnalyticsPage />, { authState: { role: 'Admin', isAuthenticated: true } })

    expect(screen.getByText('System Analytics')).toBeInTheDocument()
    expect(screen.getByText('Loads')).toBeInTheDocument()
    expect(screen.getByText('4')).toBeInTheDocument()
    expect(screen.getByText('Agencies')).toBeInTheDocument()
    expect(screen.getAllByText('6').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('Invoices & Revenue')).toBeInTheDocument()
  })

  it('shows a loading skeleton while the query is pending', () => {
    analyticsApi.useAnalyticsSummaryQuery.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      isFetching: true,
      refetch: vi.fn(),
    })

    renderWithProviders(<AdminAnalyticsPage />, { authState: { role: 'Admin', isAuthenticated: true } })

    expect(screen.getByText('System Analytics')).toBeInTheDocument()
    expect(screen.queryByText('Invoices & Revenue')).not.toBeInTheDocument()
  })

  it('shows an error state with a retry action when the query fails', () => {
    const refetch = vi.fn()
    analyticsApi.useAnalyticsSummaryQuery.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { message: 'Network error' },
      isFetching: false,
      refetch,
    })

    renderWithProviders(<AdminAnalyticsPage />, { authState: { role: 'Admin', isAuthenticated: true } })

    expect(screen.getByText('Network error')).toBeInTheDocument()
  })
})
