import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import FleetVehiclesPage from '../../pages/FleetVehiclesPage.jsx'
import { UserRole } from '../../../../lib/enums.js'
import { renderWithProviders } from '../../../../test/testUtils.jsx'
import * as agencyApi from '../../api/agencyApi.js'
import * as authApi from '../../../auth/api/authApi.js'

const mockMutateAsync = vi.fn()
const mockUpdateVehicle = vi.fn()
const mockUpdateVehicleStatus = vi.fn()

vi.mock('../../api/agencyApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useVehiclesQuery: vi.fn(),
    useAddVehicleMutation: vi.fn(() => ({
      mutateAsync: mockMutateAsync,
      isPending: false,
    })),
    useUpdateVehicleMutation: vi.fn(() => ({
      mutateAsync: mockUpdateVehicle,
      isPending: false,
    })),
    useUpdateVehicleStatusMutation: vi.fn(() => ({
      mutateAsync: mockUpdateVehicleStatus,
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
    vehicleType: 'Lorry',
    capacityKg: 5000,
    volumeM3: 18.0,
    status: 'Available',
    createdAt: '2026-09-01T10:00:00Z',
  },
  {
    vehicleId: 'veh-2',
    registrationNo: 'WP-LH-5544',
    vehicleType: 'FlatBed',
    capacityKg: 20000,
    volumeM3: 40.0,
    status: 'OnTrip',
    createdAt: '2026-09-05T10:00:00Z',
  },
  {
    vehicleId: 'veh-3',
    registrationNo: 'CP-CONT-9988',
    vehicleType: 'Container',
    capacityKg: 35000,
    volumeM3: 65.0,
    status: 'Maintenance',
    createdAt: '2026-09-10T10:00:00Z',
  },
]

function renderPage(role = UserRole.AGENCY_STAFF) {
  return renderWithProviders(<FleetVehiclesPage />, {
    route: '/agencies/vehicles',
    authState: { role, isAuthenticated: true },
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
    expect(screen.getAllByText('On Trip').length).toBeGreaterThanOrEqual(1)

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

  it('enforces type-based capacity limits dynamically based on vehicle type selection', async () => {
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

    // Default is Lorry (up to 10,000 kg). Type 12000 kg.
    await user.type(screen.getByPlaceholderText(/WP AB-1234/i), 'WP-CAD-1234')
    await user.type(screen.getByPlaceholderText('5000'), '12000')
    await user.type(screen.getByPlaceholderText('18'), '10')

    await user.click(screen.getByRole('button', { name: /add to fleet/i }))

    // Should show error for Lorry exceeding 10,000 kg
    expect(
      await screen.findByText(/capacity for lorry cannot exceed 10,000 kg/i),
    ).toBeInTheDocument()
    expect(mockMutateAsync).not.toHaveBeenCalled()

    // Switch to Container (10,000 - 100,000 kg)
    await user.click(screen.getByLabelText(/^container$/i))

    // Type 5000 kg for Container (below 10,000 kg)
    const capacityInput = screen.getByDisplayValue('12000')
    await user.clear(capacityInput)
    await user.type(capacityInput, '5000')

    await user.click(screen.getByRole('button', { name: /add to fleet/i }))

    expect(
      await screen.findByText(/capacity for container must be at least 10,000 kg/i),
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
    await user.type(screen.getByPlaceholderText('5000'), '1500')
    await user.type(screen.getByPlaceholderText('18'), '10')

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
      vehicleType: 'Lorry',
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

    // Fill valid fields (default Lorry)
    await user.type(screen.getByPlaceholderText(/WP AB-1234/i), 'WP-DA-9988')
    await user.type(screen.getByPlaceholderText('5000'), '4000')
    await user.type(screen.getByPlaceholderText('18'), '15')

    await user.click(screen.getByRole('button', { name: /add to fleet/i }))

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({
        agencyId: 'agency-101',
        vehicle: {
          vehicleType: 'Lorry',
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

    // Select Container so 25000 kg is within type range (10,000 - 100,000 kg)
    await user.click(screen.getByLabelText(/^container$/i))

    await user.type(screen.getByPlaceholderText(/WP AB-1234/i), 'WP-CAD-1020')
    await user.type(screen.getByPlaceholderText('25000'), '25000')
    await user.type(screen.getByPlaceholderText('65'), '10')

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
    await user.type(screen.getByPlaceholderText('5000'), '2000')
    await user.type(screen.getByPlaceholderText('18'), '12')

    await user.click(screen.getByRole('button', { name: /add to fleet/i }))

    // Banner error should be visible
    expect(
      await screen.findByText(/compliance documents must be verified by an administrator/i),
    ).toBeInTheDocument()
  })

  it('displays user-friendly alert message when backend throws VehicleType conversion error', async () => {
    const user = userEvent.setup()
    const backendError = new Error(
      'The JSON value could not be converted to FreightLink.Api.Entities.Enums.VehicleType. Path: $.vehicleType | LineNumber: 0 | BytePositionInLine: 26.',
    )
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

    await user.type(screen.getByPlaceholderText(/WP AB-1234/i), 'WP-CAD-1020')
    await user.type(screen.getByPlaceholderText('5000'), '3000')
    await user.type(screen.getByPlaceholderText('18'), '10')

    await user.click(screen.getByRole('button', { name: /add to fleet/i }))

    expect(
      await screen.findByText(/invalid vehicle type\. please select a supported vehicle type/i),
    ).toBeInTheDocument()
  })

  it('edits a vehicle using prefilled values and the update API', async () => {
    const user = userEvent.setup()
    mockUpdateVehicle.mockResolvedValueOnce({ ...MOCK_VEHICLES[0], registrationNo: 'WP-CAD-2020' })
    authApi.useCurrentUserQuery.mockReturnValue({ data: MOCK_USER, isLoading: false })
    agencyApi.useVehiclesQuery.mockReturnValue({ data: MOCK_VEHICLES, isLoading: false })

    renderPage()
    const row = screen.getByText('WP-CAD-1020').closest('tr')
    await user.click(within(row).getByRole('button', { name: /edit/i }))

    expect(screen.getByRole('heading', { name: 'Edit Fleet Vehicle' })).toBeInTheDocument()
    const registration = screen.getByDisplayValue('WP-CAD-1020')
    await user.clear(registration)
    await user.type(registration, 'wp-cad-2020')
    await user.click(screen.getByRole('button', { name: /save changes/i }))

    await waitFor(() => expect(mockUpdateVehicle).toHaveBeenCalledWith({
      agencyId: 'agency-101',
      vehicleId: 'veh-1',
      vehicle: {
        registrationNo: 'WP-CAD-2020',
        vehicleType: 'Lorry',
        capacityKg: 5000,
        volumeM3: 18,
      },
    }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('shows a registration conflict on the edit field', async () => {
    const user = userEvent.setup()
    const conflict = new Error('A vehicle with this registration number already exists.')
    conflict.code = 'VEHICLE_REGISTRATION_ALREADY_EXISTS'
    mockUpdateVehicle.mockRejectedValueOnce(conflict)
    authApi.useCurrentUserQuery.mockReturnValue({ data: MOCK_USER, isLoading: false })
    agencyApi.useVehiclesQuery.mockReturnValue({ data: MOCK_VEHICLES, isLoading: false })

    renderPage()
    await user.click(within(screen.getByText('WP-CAD-1020').closest('tr')).getByRole('button', { name: /edit/i }))
    await user.click(screen.getByRole('button', { name: /save changes/i }))

    expect(await screen.findByText('This registration number is already in your fleet.')).toBeInTheDocument()
  })

  it('changes availability and confirms retirement before submitting', async () => {
    const user = userEvent.setup()
    mockUpdateVehicleStatus.mockResolvedValue({})
    authApi.useCurrentUserQuery.mockReturnValue({ data: MOCK_USER, isLoading: false })
    agencyApi.useVehiclesQuery.mockReturnValue({ data: MOCK_VEHICLES, isLoading: false })

    renderPage()
    const row = screen.getByText('WP-CAD-1020').closest('tr')
    const select = within(row).getByRole('combobox', { name: 'Change status for WP-CAD-1020' })
    await user.selectOptions(select, 'Maintenance')
    await waitFor(() => expect(mockUpdateVehicleStatus).toHaveBeenCalledWith({
      agencyId: 'agency-101', vehicleId: 'veh-1', status: 'Maintenance',
    }))

    await user.selectOptions(select, 'Retired')
    expect(screen.getByRole('alertdialog')).toBeInTheDocument()
    expect(mockUpdateVehicleStatus).toHaveBeenCalledTimes(1)
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Cancel' }))
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()

    await user.selectOptions(select, 'Retired')
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Retire vehicle' }))
    await waitFor(() => expect(mockUpdateVehicleStatus).toHaveBeenCalledWith({
      agencyId: 'agency-101', vehicleId: 'veh-1', status: 'Retired',
    }))
  })

  it('uses backend statuses in filters and locks OnTrip or Retired row actions', async () => {
    const user = userEvent.setup()
    authApi.useCurrentUserQuery.mockReturnValue({ data: MOCK_USER, isLoading: false })
    agencyApi.useVehiclesQuery.mockReturnValue({
      data: [...MOCK_VEHICLES, { ...MOCK_VEHICLES[0], vehicleId: 'veh-4', registrationNo: 'WP-CAD-4040', status: 'Retired' }],
      isLoading: false,
    })

    renderPage()
    const onTripRow = screen.getByText('WP-LH-5544').closest('tr')
    expect(within(onTripRow).getByRole('button', { name: /edit/i })).toBeDisabled()
    expect(within(onTripRow).getByRole('combobox', { name: /change status/i })).toBeDisabled()
    const retiredRow = screen.getByText('WP-CAD-4040').closest('tr')
    expect(within(retiredRow).getByRole('button', { name: /edit/i })).toBeDisabled()
    expect(within(retiredRow).getByRole('combobox', { name: /change status/i })).toBeDisabled()

    const statusFilter = screen.getAllByRole('combobox')[1]
    await user.selectOptions(statusFilter, 'Retired')
    expect(screen.getByText('WP-CAD-4040')).toBeInTheDocument()
    expect(screen.queryByText('WP-CAD-1020')).not.toBeInTheDocument()
    await user.selectOptions(statusFilter, 'OnTrip')
    expect(screen.getByText('WP-LH-5544')).toBeInTheDocument()
    expect(screen.queryByText('WP-CAD-4040')).not.toBeInTheDocument()
  })

  it('does not expose vehicle mutations to Admin web users', () => {
    authApi.useCurrentUserQuery.mockReturnValue({ data: MOCK_USER, isLoading: false })
    agencyApi.useVehiclesQuery.mockReturnValue({ data: MOCK_VEHICLES, isLoading: false })
    renderPage(UserRole.ADMIN)
    expect(screen.queryByRole('button', { name: /add new vehicle/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /edit/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: /change status/i })).not.toBeInTheDocument()
  })
})
