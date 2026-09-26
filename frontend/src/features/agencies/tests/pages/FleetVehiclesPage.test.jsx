import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import FleetVehiclesPage from '../../pages/FleetVehiclesPage.jsx'
import { UserRole } from '../../../../lib/enums.js'
import { renderWithProviders } from '../../../../test/testUtils.jsx'
import * as agencyApi from '../../api/agencyApi.js'
import * as authApi from '../../../auth/api/authApi.js'

const mockMutateAsync = vi.fn()

vi.mock('../../api/agencyApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useVehiclesQuery: vi.fn(),
    useAddVehicleMutation: vi.fn(() => ({
      mutateAsync: mockMutateAsync,
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

  it('validates fields in advance using Formik and Yup and prevents submit on empty fields', async () => {
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

    await user.click(screen.getByRole('button', { name: /add new vehicle/i }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()

    // Click submit with empty required fields
    await user.click(screen.getByRole('button', { name: /add to fleet/i }))

    // Validation errors should appear
    expect(await screen.findByText('Registration number is required')).toBeInTheDocument()
    expect(screen.getByText('Capacity is required')).toBeInTheDocument()
    expect(screen.getByText('Volume is required')).toBeInTheDocument()
    expect(mockMutateAsync).not.toHaveBeenCalled()
  })

  it('enforces class-based capacity limits dynamically based on vehicle class selection', async () => {
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

    await user.click(screen.getByRole('button', { name: /add new vehicle/i }))

    // Default is Mini Truck (up to 2,500 kg). Type 3000 kg.
    await user.type(screen.getByPlaceholderText(/WP AB-1234/i), 'WP-CAD-1234')
    await user.type(screen.getByPlaceholderText('1500'), '3000')
    await user.type(screen.getByPlaceholderText('12.5'), '10')

    await user.click(screen.getByRole('button', { name: /add to fleet/i }))

    // Should show error for Mini Truck exceeding 2,500 kg
    expect(
      await screen.findByText(/capacity for mini truck cannot exceed 2,500 kg/i),
    ).toBeInTheDocument()
    expect(mockMutateAsync).not.toHaveBeenCalled()

    // Switch to Medium Lorry (2,500 - 10,000 kg)
    await user.click(screen.getByLabelText(/medium lorry/i))

    // Type 2000 kg for Medium Lorry (below 2,500 kg)
    const capacityInput = screen.getByDisplayValue('3000')
    await user.clear(capacityInput)
    await user.type(capacityInput, '2000')

    await user.click(screen.getByRole('button', { name: /add to fleet/i }))

    expect(
      await screen.findByText(/capacity for medium lorry must be at least 2,500 kg/i),
    ).toBeInTheDocument()
    expect(mockMutateAsync).not.toHaveBeenCalled()
  })

  it('rejects invalid non-Sri Lankan vehicle registration numbers and prevents submit', async () => {
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

    await user.click(screen.getByRole('button', { name: /add new vehicle/i }))

    await user.type(screen.getByPlaceholderText(/WP AB-1234/i), 'INVALID-99')
    await user.type(screen.getByPlaceholderText('1500'), '1500')
    await user.type(screen.getByPlaceholderText('12.5'), '10')

    await user.click(screen.getByRole('button', { name: /add to fleet/i }))

    expect(
      await screen.findByText(/enter a valid sri lankan vehicle registration number/i),
    ).toBeInTheDocument()
    expect(mockMutateAsync).not.toHaveBeenCalled()
  })

  it('submits valid form successfully and calls addVehicleMutation', async () => {
    const user = userEvent.setup()
    mockMutateAsync.mockResolvedValueOnce({
      vehicleId: 'veh-new',
      registrationNo: 'WP-DA-9988',
      vehicleType: 'MediumLorry',
      capacityKg: 4000,
      volumeM3: 15,
      status: 'Available',
    })

    authApi.useCurrentUserQuery.mockReturnValue({
      data: MOCK_USER,
      isLoading: false,
    })
    agencyApi.useVehiclesQuery.mockReturnValue({
      data: MOCK_VEHICLES,
      isLoading: false,
    })

    renderPage()

    await user.click(screen.getByRole('button', { name: /add new vehicle/i }))

    // Select Medium Lorry
    await user.click(screen.getByLabelText(/medium lorry/i))

    // Fill valid fields
    await user.type(screen.getByPlaceholderText(/WP AB-1234/i), 'WP-DA-9988')
    await user.type(screen.getByPlaceholderText('5000'), '4000')
    await user.type(screen.getByPlaceholderText('12.5'), '15')

    await user.click(screen.getByRole('button', { name: /add to fleet/i }))

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({
        agencyId: 'agency-101',
        vehicle: {
          vehicleType: 'MediumLorry',
          registrationNo: 'WP-DA-9988',
          capacityKg: 4000,
          volumeM3: 15,
        },
      })
    })

    // Drawer closes on success
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })
  })

  it('surfaces backend validation errors onto the specific form fields', async () => {
    const user = userEvent.setup()
    const backendError = new Error('One or more validation errors occurred.')
    backendError.code = 'VALIDATION_ERROR'
    backendError.details = [
      { field: 'RegistrationNo', issue: 'Registration number already exists.' },
      { field: 'CapacityKg', issue: 'Capacity exceeds authorized vehicle class maximum.' },
    ]
    mockMutateAsync.mockRejectedValueOnce(backendError)

    authApi.useCurrentUserQuery.mockReturnValue({
      data: MOCK_USER,
      isLoading: false,
    })
    agencyApi.useVehiclesQuery.mockReturnValue({
      data: MOCK_VEHICLES,
      isLoading: false,
    })

    renderPage()

    await user.click(screen.getByRole('button', { name: /add new vehicle/i }))

    // Select Container Truck so 25000 kg is within class range (10,000 - 100,000 kg)
    await user.click(screen.getByLabelText(/container truck/i))

    await user.type(screen.getByPlaceholderText(/WP AB-1234/i), 'WP-CAD-1020')
    await user.type(screen.getByPlaceholderText('20000'), '25000')
    await user.type(screen.getByPlaceholderText('12.5'), '10')

    await user.click(screen.getByRole('button', { name: /add to fleet/i }))

    // Field-level backend errors should be displayed
    expect(await screen.findByText('Registration number already exists.')).toBeInTheDocument()
    expect(screen.getByText('Capacity exceeds authorized vehicle class maximum.')).toBeInTheDocument()
  })

  it('displays top-level alert banner when backend throws non-validation error', async () => {
    const user = userEvent.setup()
    const backendError = new Error('Agency is not active. Compliance documents must be verified before adding fleet vehicles.')
    backendError.code = 'AGENCY_NOT_ACTIVE'
    mockMutateAsync.mockRejectedValueOnce(backendError)

    authApi.useCurrentUserQuery.mockReturnValue({
      data: MOCK_USER,
      isLoading: false,
    })
    agencyApi.useVehiclesQuery.mockReturnValue({
      data: MOCK_VEHICLES,
      isLoading: false,
    })

    renderPage()

    await user.click(screen.getByRole('button', { name: /add new vehicle/i }))

    await user.type(screen.getByPlaceholderText(/WP AB-1234/i), 'WP-NEW-1122')
    await user.type(screen.getByPlaceholderText('1500'), '2000')
    await user.type(screen.getByPlaceholderText('12.5'), '12')

    await user.click(screen.getByRole('button', { name: /add to fleet/i }))

    // Banner error should be visible
    expect(
      await screen.findByText(/compliance documents must be verified by an administrator/i),
    ).toBeInTheDocument()
  })
})
