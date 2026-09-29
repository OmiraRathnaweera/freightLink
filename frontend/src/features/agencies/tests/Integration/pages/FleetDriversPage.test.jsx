import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import FleetDriversPage from '../../../pages/FleetDriversPage.jsx'
import { UserRole } from '../../../../../lib/enums.js'
import { renderWithProviders } from '../../../../../test/testUtils.jsx'
import * as agencyApi from '../../../api/agencyApi.js'
import * as authApi from '../../../../auth/api/authApi.js'

const mockAddMutateAsync = vi.fn()
const mockUpdateStatusMutateAsync = vi.fn()

vi.mock('../../../api/agencyApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useDriversQuery: vi.fn(),
    useAddDriverMutation: vi.fn(() => ({
      mutateAsync: mockAddMutateAsync,
      isPending: false,
    })),
    useUpdateDriverStatusMutation: vi.fn(() => ({
      mutateAsync: mockUpdateStatusMutateAsync,
      isPending: false,
    })),
  }
})

vi.mock('../../../../auth/api/authApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useCurrentUserQuery: vi.fn(),
  }
})

const MOCK_USER = {
  id: 'user-1',
  email: 'staff@logistics.com',
  role: 'AgencyStaff',
  agencyId: 'agency-101',
}

const MOCK_DRIVERS = [
  {
    driverId: 'drv-1',
    fullName: 'Nimal Perera',
    email: 'nimal@example.com',
    licenceNo: 'B1234567',
    licenceExpiry: '2028-01-01',
    status: 'Active',
  },
  {
    driverId: 'drv-2',
    fullName: 'Kamal Silva',
    email: 'kamal@example.com',
    licenceNo: 'B7654321',
    licenceExpiry: '2027-06-01',
    status: 'OnTrip',
  },
  {
    driverId: 'drv-3',
    fullName: 'Sunil Fernando',
    email: 'sunil@example.com',
    licenceNo: 'B1112223',
    licenceExpiry: '2026-12-01',
    status: 'Inactive',
  },
]

function renderPage() {
  return renderWithProviders(<FleetDriversPage />, {
    route: '/agencies/drivers',
    authState: { role: UserRole.AGENCY_STAFF, isAuthenticated: true },
  })
}

afterEach(() => {
  vi.clearAllMocks()
  cleanup()
})

describe('FleetDriversPage', () => {
  it('renders roster metrics and driver list correctly', () => {
    authApi.useCurrentUserQuery.mockReturnValue({ data: MOCK_USER, isLoading: false })
    agencyApi.useDriversQuery.mockReturnValue({ data: MOCK_DRIVERS, isLoading: false })

    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Fleet Drivers' })).toBeInTheDocument()
    expect(screen.getByText('Total Drivers')).toBeInTheDocument()
    expect(screen.getByText('Nimal Perera')).toBeInTheDocument()
    expect(screen.getByText('Kamal Silva')).toBeInTheDocument()
    expect(screen.getByText('Sunil Fernando')).toBeInTheDocument()
  })

  it('filters drivers by search text query', async () => {
    const user = userEvent.setup()
    authApi.useCurrentUserQuery.mockReturnValue({ data: MOCK_USER, isLoading: false })
    agencyApi.useDriversQuery.mockReturnValue({ data: MOCK_DRIVERS, isLoading: false })

    renderPage()

    const searchInput = screen.getByPlaceholderText(/search by name, email, or licence number/i)
    await user.type(searchInput, 'Kamal')

    expect(screen.getByText('Kamal Silva')).toBeInTheDocument()
    expect(screen.queryByText('Nimal Perera')).not.toBeInTheDocument()
    expect(screen.queryByText('Sunil Fernando')).not.toBeInTheDocument()
  })

  it('renders empty state when no drivers are onboarded', () => {
    authApi.useCurrentUserQuery.mockReturnValue({ data: MOCK_USER, isLoading: false })
    agencyApi.useDriversQuery.mockReturnValue({ data: [], isLoading: false })

    renderPage()

    expect(screen.getByText('No drivers onboarded yet')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /add first driver/i })).toBeInTheDocument()
  })

  it('opens add driver drawer when "Add Driver" is clicked', async () => {
    const user = userEvent.setup()
    authApi.useCurrentUserQuery.mockReturnValue({ data: MOCK_USER, isLoading: false })
    agencyApi.useDriversQuery.mockReturnValue({ data: MOCK_DRIVERS, isLoading: false })

    renderPage()

    await user.click(screen.getByRole('button', { name: /^add driver$/i }))

    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: /add driver/i })).toBeInTheDocument()
  })

  it('validates fields and prevents submit on empty fields', async () => {
    const user = userEvent.setup()
    authApi.useCurrentUserQuery.mockReturnValue({ data: MOCK_USER, isLoading: false })
    agencyApi.useDriversQuery.mockReturnValue({ data: MOCK_DRIVERS, isLoading: false })

    renderPage()

    await user.click(screen.getByRole('button', { name: /^add driver$/i }))
    await user.click(screen.getByRole('button', { name: /add to roster/i }))

    expect(await screen.findByText('Full name is required')).toBeInTheDocument()
    expect(screen.getByText('Email is required')).toBeInTheDocument()
    expect(screen.getByText('Licence number is required')).toBeInTheDocument()
    expect(mockAddMutateAsync).not.toHaveBeenCalled()
  })

  it('submits a valid new-driver form and calls addDriverMutation', async () => {
    const user = userEvent.setup()
    mockAddMutateAsync.mockResolvedValueOnce({
      driverId: 'drv-new',
      fullName: 'New Driver',
      email: 'new-driver@example.com',
      licenceNo: 'B9998887',
      status: 'Active',
      temporaryPassword: 'Zx7!qLmP9aRt',
    })

    authApi.useCurrentUserQuery.mockReturnValue({ data: MOCK_USER, isLoading: false })
    agencyApi.useDriversQuery.mockReturnValue({ data: MOCK_DRIVERS, isLoading: false })

    renderPage()

    await user.click(screen.getByRole('button', { name: /^add driver$/i }))

    await user.type(screen.getByLabelText(/full name/i), 'New Driver')
    await user.type(screen.getByLabelText(/login email/i), 'new-driver@example.com')
    await user.type(screen.getByLabelText(/licence number/i), 'B9998887')
    await user.type(screen.getByLabelText(/licence expiry/i), '2028-01-01')

    await user.click(screen.getByRole('button', { name: /add to roster/i }))

    await waitFor(() => {
      expect(mockAddMutateAsync).toHaveBeenCalledWith({
        agencyId: 'agency-101',
        driver: {
          fullName: 'New Driver',
          email: 'new-driver@example.com',
          phoneE164: undefined,
          licenceNo: 'B9998887',
          licenceExpiry: '2028-01-01',
        },
      })
    })

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })
  })

  it('removes an Active driver from the roster when its status is changed to Inactive', async () => {
    const user = userEvent.setup()
    mockUpdateStatusMutateAsync.mockResolvedValueOnce({ driverId: 'drv-1', status: 'Inactive' })

    authApi.useCurrentUserQuery.mockReturnValue({ data: MOCK_USER, isLoading: false })
    agencyApi.useDriversQuery.mockReturnValue({ data: MOCK_DRIVERS, isLoading: false })

    renderPage()

    const row = screen.getByText('Nimal Perera').closest('tr')
    await user.selectOptions(within(row).getByRole('combobox'), 'Inactive')

    await waitFor(() => {
      expect(mockUpdateStatusMutateAsync).toHaveBeenCalledWith({
        agencyId: 'agency-101',
        driverId: 'drv-1',
        status: 'Inactive',
      })
    })
  })

  it('reinstates an Inactive driver when its status is changed to Active', async () => {
    const user = userEvent.setup()
    mockUpdateStatusMutateAsync.mockResolvedValueOnce({ driverId: 'drv-3', status: 'Active' })

    authApi.useCurrentUserQuery.mockReturnValue({ data: MOCK_USER, isLoading: false })
    agencyApi.useDriversQuery.mockReturnValue({ data: MOCK_DRIVERS, isLoading: false })

    renderPage()

    const row = screen.getByText('Sunil Fernando').closest('tr')
    await user.selectOptions(within(row).getByRole('combobox'), 'Active')

    await waitFor(() => {
      expect(mockUpdateStatusMutateAsync).toHaveBeenCalledWith({
        agencyId: 'agency-101',
        driverId: 'drv-3',
        status: 'Active',
      })
    })
  })

  it('disables the status action for a driver who is OnTrip', () => {
    authApi.useCurrentUserQuery.mockReturnValue({ data: MOCK_USER, isLoading: false })
    agencyApi.useDriversQuery.mockReturnValue({ data: MOCK_DRIVERS, isLoading: false })

    renderPage()

    const row = screen.getByText('Kamal Silva').closest('tr')
    expect(within(row).getByRole('combobox')).toBeDisabled()
  })
})
