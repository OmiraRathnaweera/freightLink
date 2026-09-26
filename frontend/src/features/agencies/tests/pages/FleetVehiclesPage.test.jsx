import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import FleetVehiclesPage from '../../pages/FleetVehiclesPage.jsx'
import { UserRole } from '../../../../lib/enums.js'
import { renderWithProviders } from '../../../../test/testUtils.jsx'
import * as agencyApi from '../../api/agencyApi.js'
import * as authApi from '../../../auth/api/authApi.js'

vi.mock('../../api/agencyApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useVehiclesQuery: vi.fn(),
    useAddVehicleMutation: vi.fn(() => ({
      mutateAsync: vi.fn(),
      isPending: false,
    })),
  }
})

vi.mock('../../../auth/api/authApi.js', async (importOriginal) => {
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

const MOCK_VEHICLES = [
  {
    vehicleId: 'veh-1',
    registrationNo: 'WP-CAD-1020',
    vehicleType: 'MiniTruck',
    capacityKg: 1500,
    volumeM3: 6.5,
    status: 'Available',
    createdAt: '2026-09-01T10:00:00Z',
  },
  {
    vehicleId: 'veh-2',
    registrationNo: 'WP-LH-5544',
    vehicleType: 'MediumLorry',
    capacityKg: 5000,
    volumeM3: 18.0,
    status: 'InUse',
    createdAt: '2026-09-05T10:00:00Z',
  },
  {
    vehicleId: 'veh-3',
    registrationNo: 'CP-CONT-9988',
    vehicleType: 'ContainerTruck',
    capacityKg: 20000,
    volumeM3: 65.0,
    status: 'Maintenance',
    createdAt: '2026-09-10T10:00:00Z',
  },
]

function renderPage() {
  return renderWithProviders(<FleetVehiclesPage />, {
    route: '/agencies/vehicles',
    authState: { role: UserRole.AGENCY_STAFF, isAuthenticated: true },
  })
}

afterEach(() => {
  vi.clearAllMocks()
  cleanup()
})

describe('FleetVehiclesPage', () => {
  it('renders fleet metrics and vehicle list correctly', () => {
    authApi.useCurrentUserQuery.mockReturnValue({
      data: MOCK_USER,
      isLoading: false,
    })
    agencyApi.useVehiclesQuery.mockReturnValue({
      data: MOCK_VEHICLES,
      isLoading: false,
    })

    renderPage()

    // Title & subtitle
    expect(screen.getByRole('heading', { level: 1, name: 'Fleet Vehicles' })).toBeInTheDocument()

    // Metrics summary
    expect(screen.getByText('Total Fleet')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument() // 3 total vehicles
    expect(screen.getAllByText('Available').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('In Use / Transit')).toBeInTheDocument()

    // Vehicles table entries
    expect(screen.getByText('WP-CAD-1020')).toBeInTheDocument()
    expect(screen.getByText('WP-LH-5544')).toBeInTheDocument()
    expect(screen.getByText('CP-CONT-9988')).toBeInTheDocument()
  })

  it('filters vehicles by search text query', async () => {
    const user = userEvent.setup()
    authApi.useCurrentUserQuery.mockReturnValue({
      data: MOCK_USER,
      isLoading: false,
    })
    agencyApi.useVehiclesQuery.mockReturnValue({
      data: MOCK_VEHICLES,
      isLoading: false,
    })

    renderPage()

    const searchInput = screen.getByPlaceholderText(/search by registration number or class/i)
    await user.type(searchInput, 'CONT')

    // Only container truck should match
    expect(screen.getByText('CP-CONT-9988')).toBeInTheDocument()
    expect(screen.queryByText('WP-CAD-1020')).not.toBeInTheDocument()
    expect(screen.queryByText('WP-LH-5544')).not.toBeInTheDocument()
  })

  it('renders empty state when no vehicles are registered', () => {
    authApi.useCurrentUserQuery.mockReturnValue({
      data: MOCK_USER,
      isLoading: false,
    })
    agencyApi.useVehiclesQuery.mockReturnValue({
      data: [],
      isLoading: false,
    })

    renderPage()

    expect(screen.getByText('No vehicles registered yet')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /register first vehicle/i })).toBeInTheDocument()
  })

  it('opens add vehicle drawer when "Add New Vehicle" is clicked', async () => {
    const user = userEvent.setup()
    authApi.useCurrentUserQuery.mockReturnValue({
      data: MOCK_USER,
      isLoading: false,
    })
    agencyApi.useVehiclesQuery.mockReturnValue({
      data: MOCK_VEHICLES,
      isLoading: false,
    })

    renderPage()

    const addBtn = screen.getByRole('button', { name: /add new vehicle/i })
    await user.click(addBtn)

    // Drawer should open with dialog role
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: /add fleet vehicle/i })).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/WP AB-1234/i)).toBeInTheDocument()
  })
})
