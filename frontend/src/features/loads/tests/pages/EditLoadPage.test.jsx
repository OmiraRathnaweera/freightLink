import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useFormikContext } from 'formik'
import { toast } from 'sonner'
import EditLoadPage from '../../pages/EditLoadPage.jsx'
import { UserRole } from '../../../../lib/enums.js'
import { renderWithProviders } from '../../../../test/testUtils.jsx'
import * as loadsApi from '../../api/loadsApi.js'

// Same rationale as PostLoadPage.test.jsx — swap the real Leaflet-backed
// picker for a no-op stub so these tests can focus on the rest of the form.
vi.mock('../../../../components/form/index.js', async (importOriginal) => {
  const actual = await importOriginal()
  function FormikDualLocationFieldStub() {
    const { values } = useFormikContext()
    return (
      <div data-testid="location-picker-stub">
        {values.pickupAddress} → {values.dropoffAddress}
      </div>
    )
  }
  return { ...actual, FormikDualLocationField: FormikDualLocationFieldStub }
})

vi.mock('../../api/loadsApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return { ...actual, useLoadDetailQuery: vi.fn(), useUpdateLoadMutation: vi.fn() }
})

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))

function sampleLoad(overrides = {}) {
  return {
    loadId: 'load-1',
    referenceCode: 'LD-0001',
    cargoDescription: 'Canned goods',
    weightKg: 1200,
    volumeM3: 8.25,
    pickupAddress: 'Colombo Port',
    pickupLat: 6.9271,
    pickupLng: 79.8612,
    dropoffAddress: 'Kandy Warehouse',
    dropoffLat: 7.2906,
    dropoffLng: 80.6337,
    pickupWindowStart: '2026-09-10T09:00:00.000Z',
    pickupWindowEnd: '2026-09-10T17:00:00.000Z',
    status: 'Draft',
    ...overrides,
  }
}

function renderEditLoadPage(role = UserRole.SHIPPER) {
  return renderWithProviders(<EditLoadPage />, {
    route: '/loads/:loadId/edit',
    initialEntries: ['/loads/load-1/edit'],
    authState: { role, isAuthenticated: true },
  })
}

afterEach(() => {
  vi.mocked(loadsApi.useLoadDetailQuery).mockReset()
  vi.mocked(loadsApi.useUpdateLoadMutation).mockReset()
  vi.mocked(toast.success).mockReset()
  cleanup()
})

describe('EditLoadPage — loading and error states', () => {
  it('renders skeletons while the load is loading', () => {
    loadsApi.useLoadDetailQuery.mockReturnValue({ isLoading: true, isError: false })
    loadsApi.useUpdateLoadMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    const { container } = renderEditLoadPage()
    expect(container.querySelectorAll('.animate-pulse').length).toBeGreaterThan(0)
  })

  it('renders ErrorState with retry when the load fails to fetch', () => {
    const refetch = vi.fn()
    loadsApi.useLoadDetailQuery.mockReturnValue({ isLoading: false, isError: true, error: { code: 'LOAD_NOT_FOUND' }, refetch })
    loadsApi.useUpdateLoadMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    renderEditLoadPage()
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
  })

  it('renders an EmptyState instead of the form for a non-editable status', () => {
    loadsApi.useLoadDetailQuery.mockReturnValue({ isLoading: false, isError: false, data: sampleLoad({ status: 'Delivered' }) })
    loadsApi.useUpdateLoadMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    renderEditLoadPage()
    expect(screen.getByText('This load can no longer be edited')).toBeInTheDocument()
    expect(screen.getByText('Loads in "Delivered" status cannot be changed.')).toBeInTheDocument()
  })

  it('renders a role-specific EmptyState message for a non-Shipper role', () => {
    loadsApi.useLoadDetailQuery.mockReturnValue({ isLoading: false, isError: false, data: sampleLoad({ status: 'Draft' }) })
    loadsApi.useUpdateLoadMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    renderEditLoadPage(UserRole.ADMIN)
    expect(screen.getByText('Only the Shipper who owns this load can edit it.')).toBeInTheDocument()
  })
})

describe('EditLoadPage — rendering with existing values', () => {
  it('pre-fills the form fields from the loaded record', () => {
    loadsApi.useLoadDetailQuery.mockReturnValue({ isLoading: false, isError: false, data: sampleLoad() })
    loadsApi.useUpdateLoadMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    renderEditLoadPage()

    expect(screen.getByLabelText('Cargo Description')).toHaveValue('Canned goods')
    expect(screen.getByLabelText('Total Weight (kg)')).toHaveValue(1200)
    expect(screen.getByLabelText('Volume (m³)')).toHaveValue(8.25)
    expect(screen.getByTestId('location-picker-stub')).toHaveTextContent('Colombo Port → Kandy Warehouse')
  })

  it('renders the submit button', () => {
    loadsApi.useLoadDetailQuery.mockReturnValue({ isLoading: false, isError: false, data: sampleLoad() })
    loadsApi.useUpdateLoadMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    renderEditLoadPage()
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeInTheDocument()
  })

  it('shows a Publish button only when the load is publishable', () => {
    loadsApi.useLoadDetailQuery.mockReturnValue({ isLoading: false, isError: false, data: sampleLoad({ status: 'Draft' }) })
    loadsApi.useUpdateLoadMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    renderEditLoadPage()
    expect(screen.getByRole('button', { name: 'Publish' })).toBeInTheDocument()
  })

  it('hides the Publish button once the load is Posted', () => {
    loadsApi.useLoadDetailQuery.mockReturnValue({ isLoading: false, isError: false, data: sampleLoad({ status: 'Posted' }) })
    loadsApi.useUpdateLoadMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    renderEditLoadPage()
    expect(screen.queryByRole('button', { name: 'Publish' })).not.toBeInTheDocument()
  })
})

describe('EditLoadPage — API integration', () => {
  it('calls the update mutation and shows a success toast', async () => {
    const user = userEvent.setup()
    const mutateAsync = vi.fn().mockResolvedValue(sampleLoad())
    loadsApi.useLoadDetailQuery.mockReturnValue({ isLoading: false, isError: false, data: sampleLoad() })
    loadsApi.useUpdateLoadMutation.mockReturnValue({ mutateAsync, isPending: false })
    renderEditLoadPage()

    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(1))
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Load updated'))
  })

  it('shows a banner error when the update mutation fails', async () => {
    const user = userEvent.setup()
    const mutateAsync = vi.fn().mockRejectedValue({ code: 'LOAD_CONCURRENCY_CONFLICT' })
    loadsApi.useLoadDetailQuery.mockReturnValue({ isLoading: false, isError: false, data: sampleLoad() })
    loadsApi.useUpdateLoadMutation.mockReturnValue({ mutateAsync, isPending: false })
    renderEditLoadPage()

    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText(/someone else may have changed it|conflict|try again/i)).toBeInTheDocument()
    expect(toast.success).not.toHaveBeenCalled()
  })
})
