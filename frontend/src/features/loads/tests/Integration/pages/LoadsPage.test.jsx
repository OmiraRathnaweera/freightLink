import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import LoadsPage from '../../../pages/LoadsPage.jsx'
import { UserRole } from '../../../../../lib/enums.js'
import { renderWithProviders } from '../../../../../test/testUtils.jsx'
import * as loadsApi from '../../../api/loadsApi.js'

// LoadsPage (the "My Loads" dashboard) is exercised through the real
// LoadsTable/RowActionsMenu/EmptyState/ErrorState/Skeleton components — only
// the network boundary (useLoadsQuery) is mocked, so this also covers "no
// real network calls are made" for the list-fetch path (Part E).
vi.mock('../../../api/loadsApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return { ...actual, useLoadsQuery: vi.fn() }
})

function sampleLoad(overrides = {}) {
  return {
    loadId: 'load-1',
    referenceCode: 'LD-0001',
    shipperUserId: 'user-1',
    shipperName: 'Acme Traders',
    cargoDescription: 'Canned goods',
    weightKg: 1200,
    pickupAddress: 'Colombo',
    dropoffAddress: 'Kandy',
    pickupWindowStart: '2026-09-10T09:00:00.000Z',
    pickupWindowEnd: '2026-09-10T17:00:00.000Z',
    estimatedPrice: 15000,
    status: 'Posted',
    createdAt: '2026-09-01T00:00:00.000Z',
    ...overrides,
  }
}

function renderLoadsPage(role = UserRole.SHIPPER) {
  return renderWithProviders(<LoadsPage />, { route: '/loads', authState: { role, isAuthenticated: true } })
}

afterEach(() => {
  vi.mocked(loadsApi.useLoadsQuery).mockReset()
  cleanup()
})

describe('LoadsPage — loading state', () => {
  it('renders skeleton rows while the query is loading', () => {
    loadsApi.useLoadsQuery.mockReturnValue({ isLoading: true, isError: false, data: undefined })
    const { container } = renderLoadsPage()
    expect(container.querySelectorAll('.animate-pulse').length).toBeGreaterThan(0)
    expect(screen.queryByText('No loads found')).not.toBeInTheDocument()
  })
})

describe('LoadsPage — error state', () => {
  it('renders ErrorState with a retry action when the query fails', async () => {
    const refetch = vi.fn()
    loadsApi.useLoadsQuery.mockReturnValue({
      isLoading: false,
      isError: true,
      error: { code: 'LOAD_NOT_FOUND' },
      refetch,
    })
    renderLoadsPage()
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
  })
})

describe('LoadsPage — empty state', () => {
  it('renders EmptyState when there are no loads', () => {
    loadsApi.useLoadsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 },
    })
    renderLoadsPage()
    expect(screen.getByText('No loads found')).toBeInTheDocument()
  })

  it('offers a "Post a Load" action for a Shipper', () => {
    loadsApi.useLoadsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 },
    })
    renderLoadsPage(UserRole.SHIPPER)
    expect(screen.getAllByRole('link', { name: 'Post a Load' }).length).toBeGreaterThan(0)
  })

  it('does not offer "Post a Load" for a non-Shipper role', () => {
    loadsApi.useLoadsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 },
    })
    renderLoadsPage(UserRole.ADMIN)
    expect(screen.queryByRole('link', { name: 'Post a Load' })).not.toBeInTheDocument()
  })
})

describe('LoadsPage — list rendering', () => {
  it('renders a row per load with its reference, cargo and status badge', () => {
    loadsApi.useLoadsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [sampleLoad({ loadId: 'load-1', referenceCode: 'LD-0001', status: 'Draft' }), sampleLoad({ loadId: 'load-2', referenceCode: 'LD-0002', status: 'Matched' })],
        page: 1,
        pageSize: 20,
        totalItems: 2,
        totalPages: 1,
      },
    })
    renderLoadsPage()

    const table = within(screen.getByRole('table'))
    expect(table.getByRole('link', { name: 'LD-0001' })).toBeInTheDocument()
    expect(table.getByRole('link', { name: 'LD-0002' })).toBeInTheDocument()
    expect(table.getAllByText('Canned goods')).toHaveLength(2)
    // "Draft"/"Matched" also appear as <option>s in the status filter above
    // the table, so these assertions are scoped to the table itself.
    expect(table.getByText('Draft')).toBeInTheDocument()
    expect(table.getByText('Matched')).toBeInTheDocument()
  })

  it('shows Edit and Cancel for a Draft row but hides both for a Matched row', async () => {
    const user = userEvent.setup()
    loadsApi.useLoadsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [
          sampleLoad({ loadId: 'load-draft', referenceCode: 'LD-D', status: 'Draft' }),
          sampleLoad({ loadId: 'load-matched', referenceCode: 'LD-M', status: 'Matched' }),
        ],
        page: 1,
        pageSize: 20,
        totalItems: 2,
        totalPages: 1,
      },
    })
    renderLoadsPage()

    await user.click(screen.getByRole('button', { name: 'Actions for load-draft' }))
    let menu = within(screen.getByRole('menu'))
    expect(menu.getByRole('menuitem', { name: /edit/i })).toBeInTheDocument()
    expect(menu.getByRole('menuitem', { name: /cancel/i })).toBeInTheDocument()
    await user.keyboard('{Escape}')

    await user.click(screen.getByRole('button', { name: 'Actions for load-matched' }))
    menu = within(screen.getByRole('menu'))
    expect(menu.queryByRole('menuitem', { name: /edit/i })).not.toBeInTheDocument()
    expect(menu.queryByRole('menuitem', { name: /cancel/i })).not.toBeInTheDocument()
  })
})
