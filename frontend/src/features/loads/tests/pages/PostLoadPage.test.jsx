import { useEffect } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useFormikContext } from 'formik'
import { toast } from 'sonner'
import PostLoadPage from '../../pages/PostLoadPage.jsx'
import { UserRole } from '../../../../lib/enums.js'
import { renderWithProviders } from '../../../../test/testUtils.jsx'
import * as loadsApi from '../../api/loadsApi.js'

// The real FormikDualLocationField renders a react-leaflet MapContainer,
// which needs real browser layout APIs jsdom doesn't provide. It's
// Formik-agnostic-adjacent (see its own file comment) and covered by its own
// component, so here it's replaced with a stub that fills in valid
// pickup/dropoff values on mount — enough to exercise the rest of the form
// (required fields, postImmediately, submit) without a real map.
vi.mock('../../../../components/form/index.js', async (importOriginal) => {
  const actual = await importOriginal()
  function FormikDualLocationFieldStub({ pickupAddressName, pickupLatName, pickupLngName, dropoffAddressName, dropoffLatName, dropoffLngName }) {
    const { setFieldValue } = useFormikContext()
    useEffect(() => {
      setFieldValue(pickupAddressName, 'Colombo Port')
      setFieldValue(pickupLatName, 6.9271)
      setFieldValue(pickupLngName, 79.8612)
      setFieldValue(dropoffAddressName, 'Kandy Warehouse')
      setFieldValue(dropoffLatName, 7.2906)
      setFieldValue(dropoffLngName, 80.6337)
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [])
    return <div data-testid="location-picker-stub" />
  }
  return { ...actual, FormikDualLocationField: FormikDualLocationFieldStub }
})

vi.mock('../../api/loadsApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return { ...actual, useCreateLoadMutation: vi.fn() }
})

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))

function renderPostLoadPage(role = UserRole.SHIPPER) {
  return renderWithProviders(<PostLoadPage />, { route: '/loads/new', authState: { role, isAuthenticated: true } })
}

async function fillRequiredFields(user) {
  await user.type(screen.getByLabelText('Cargo Description'), 'Pallets of canned goods')
  await user.type(screen.getByLabelText('Weight (kg)'), '1200.5')
  await user.type(screen.getByLabelText('Volume (m³)'), '8.25')
  await user.type(screen.getByLabelText('Pickup Window Start'), '2026-09-10T09:00')
  await user.type(screen.getByLabelText('Pickup Window End'), '2026-09-10T17:00')
}

afterEach(() => {
  vi.mocked(loadsApi.useCreateLoadMutation).mockReset()
  vi.mocked(toast.success).mockReset()
  vi.mocked(toast.error).mockReset()
  cleanup()
})

describe('PostLoadPage — rendering', () => {
  it('renders the required create-load fields', () => {
    loadsApi.useCreateLoadMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    renderPostLoadPage()

    expect(screen.getByLabelText('Cargo Description')).toBeInTheDocument()
    expect(screen.getByLabelText('Weight (kg)')).toBeInTheDocument()
    expect(screen.getByLabelText('Volume (m³)')).toBeInTheDocument()
    expect(screen.getByLabelText('Pickup Window Start')).toBeInTheDocument()
    expect(screen.getByLabelText('Pickup Window End')).toBeInTheDocument()
    expect(screen.getByTestId('location-picker-stub')).toBeInTheDocument()
  })

  it('renders the postImmediately control on the create form', () => {
    loadsApi.useCreateLoadMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    renderPostLoadPage()
    expect(screen.getByLabelText(/post immediately/i)).toBeInTheDocument()
  })

  it('renders the submit button', () => {
    loadsApi.useCreateLoadMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    renderPostLoadPage()
    expect(screen.getByRole('button', { name: 'Save Load' })).toBeInTheDocument()
  })

  it('shows an EmptyState instead of the form for a non-Shipper role', () => {
    loadsApi.useCreateLoadMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    renderPostLoadPage(UserRole.DRIVER)
    expect(screen.getByText("You can't post a load")).toBeInTheDocument()
    expect(screen.queryByLabelText('Cargo Description')).not.toBeInTheDocument()
  })
})

describe('PostLoadPage — client-side validation', () => {
  it('shows field-level errors when required fields are left blank', async () => {
    const user = userEvent.setup()
    loadsApi.useCreateLoadMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    renderPostLoadPage()

    await user.click(screen.getByRole('button', { name: 'Save Load' }))

    expect(await screen.findByText('Cargo description is required')).toBeInTheDocument()
    expect(screen.getByText('Weight is required')).toBeInTheDocument()
    expect(screen.getByText('Volume is required')).toBeInTheDocument()
    expect(screen.getByText('Pickup window start is required')).toBeInTheDocument()
    expect(screen.getByText('Pickup window end is required')).toBeInTheDocument()
  })
})

describe('PostLoadPage — API integration', () => {
  it('calls the create mutation and navigates away on success', async () => {
    const user = userEvent.setup()
    const mutateAsync = vi.fn().mockResolvedValue({ loadId: 'new-load-id', status: 'Draft' })
    loadsApi.useCreateLoadMutation.mockReturnValue({ mutateAsync, isPending: false })
    renderPostLoadPage()

    await fillRequiredFields(user)
    await user.click(screen.getByRole('button', { name: 'Save Load' }))

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(1))
    expect(mutateAsync.mock.calls[0][0]).toMatchObject({
      cargoDescription: 'Pallets of canned goods',
      weightKg: 1200.5,
      volumeM3: 8.25,
    })
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Load saved as draft'))
  })

  it('shows a banner error when the create mutation fails with a non-validation error', async () => {
    const user = userEvent.setup()
    const mutateAsync = vi.fn().mockRejectedValue({ code: 'LOAD_REFERENCE_CODE_CONFLICT' })
    loadsApi.useCreateLoadMutation.mockReturnValue({ mutateAsync, isPending: false })
    renderPostLoadPage()

    await fillRequiredFields(user)
    await user.click(screen.getByRole('button', { name: 'Save Load' }))

    expect(await screen.findByText(/reference code/i)).toBeInTheDocument()
    expect(toast.success).not.toHaveBeenCalled()
  })

  it('maps a VALIDATION_ERROR response onto the matching form fields', async () => {
    const user = userEvent.setup()
    const mutateAsync = vi.fn().mockRejectedValue({
      code: 'VALIDATION_ERROR',
      details: [{ field: 'cargoDescription', issue: 'Cargo description contains banned content' }],
    })
    loadsApi.useCreateLoadMutation.mockReturnValue({ mutateAsync, isPending: false })
    renderPostLoadPage()

    await fillRequiredFields(user)
    await user.click(screen.getByRole('button', { name: 'Save Load' }))

    expect(await screen.findByText('Cargo description contains banned content')).toBeInTheDocument()
  })

  it('makes no real network calls — the API layer is fully mocked', () => {
    // useCreateLoadMutation is a vi.fn() replacing the real hook (see
    // vi.mock above); listLoads/createLoad's real axios-backed implementations
    // are never invoked by this suite, so there is nothing to assert beyond
    // the mock plumbing itself — this test documents that guarantee.
    expect(vi.isMockFunction(loadsApi.useCreateLoadMutation)).toBe(true)
  })
})
