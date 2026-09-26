import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { toast } from 'sonner'
import RegisterPage from '../../pages/RegisterPage.jsx'
import { renderWithProviders } from '../../../../test/testUtils.jsx'
import * as authApi from '../../api/authApi.js'

vi.mock('../../api/authApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useRegisterShipperMutation: vi.fn(),
    useRegisterAgencyMutation: vi.fn(),
  }
})

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}))

function renderRegisterPage(initialEntry = '/register') {
  return renderWithProviders(<RegisterPage />, {
    route: '/register',
    initialEntries: [initialEntry],
  })
}

afterEach(() => {
  vi.mocked(authApi.useRegisterShipperMutation).mockReset()
  vi.mocked(authApi.useRegisterAgencyMutation).mockReset()
  vi.mocked(toast.success).mockReset()
  cleanup()
})

describe('RegisterPage — Role Selection', () => {
  it('renders account type options when no role is in the URL', () => {
    renderRegisterPage('/register')

    expect(screen.getByRole('heading', { name: /Choose your account type to register/i })).toBeInTheDocument()
    expect(screen.getByText('Register as Shipper')).toBeInTheDocument()
    expect(screen.getByText('Join Agency Network')).toBeInTheDocument()
  })
})

describe('RegisterPage — Shipper Registration Form', () => {
  it('renders the shipper form with all expected fields', () => {
    vi.mocked(authApi.useRegisterShipperMutation).mockReturnValue({
      mutateAsync: vi.fn(),
      isPending: false,
    })

    renderRegisterPage('/register?role=shipper')

    expect(screen.getByRole('heading', { name: 'Register as Shipper' })).toBeInTheDocument()
    expect(screen.getByLabelText(/Full Name/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Email Address/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/^Password/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Phone Number/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Company Name/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Business Registration Number/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Billing Address/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Complete Shipper Registration/i })).toBeInTheDocument()
  })

  it('validates every input in advance and blocks submission on empty fields', async () => {
    const mutateAsync = vi.fn()
    vi.mocked(authApi.useRegisterShipperMutation).mockReturnValue({
      mutateAsync,
      isPending: false,
    })

    const user = userEvent.setup()
    renderRegisterPage('/register?role=shipper')

    await user.click(screen.getByRole('button', { name: /Complete Shipper Registration/i }))

    await waitFor(() => {
      expect(screen.getByText('Full name is required')).toBeInTheDocument()
      expect(screen.getByText('Email is required')).toBeInTheDocument()
      expect(screen.getByText('Password is required')).toBeInTheDocument()
      expect(screen.getByText('Company name is required')).toBeInTheDocument()
      expect(screen.getByText('Billing address is required')).toBeInTheDocument()
    })

    expect(mutateAsync).not.toHaveBeenCalled()
  })

  it('validates password requirements and phone format in advance', async () => {
    const mutateAsync = vi.fn()
    vi.mocked(authApi.useRegisterShipperMutation).mockReturnValue({
      mutateAsync,
      isPending: false,
    })

    const user = userEvent.setup()
    renderRegisterPage('/register?role=shipper')

    await user.type(screen.getByLabelText(/^Password/i), 'weak')
    await user.type(screen.getByLabelText(/Phone Number/i), '0771234567')
    await user.click(screen.getByRole('button', { name: /Complete Shipper Registration/i }))

    await waitFor(() => {
      expect(screen.getByText(/Password must be at least 8 characters/i)).toBeInTheDocument()
      expect(screen.getByText(/Phone number must be in E.164 format/i)).toBeInTheDocument()
    })

    expect(mutateAsync).not.toHaveBeenCalled()
  })

  it('submits valid shipper payload successfully and shows success toast', async () => {
    const mutateAsync = vi.fn().mockResolvedValueOnce({ id: 'shipper-1' })
    vi.mocked(authApi.useRegisterShipperMutation).mockReturnValue({
      mutateAsync,
      isPending: false,
    })

    const user = userEvent.setup()
    renderRegisterPage('/register?role=shipper')

    await user.type(screen.getByLabelText(/Full Name/i), 'Jane Doe')
    await user.type(screen.getByLabelText(/Email Address/i), 'jane@company.lk')
    await user.type(screen.getByLabelText(/^Password/i), 'SecurePass123!')
    await user.type(screen.getByLabelText(/Phone Number/i), '+94771234567')
    await user.type(screen.getByLabelText(/Company Name/i), 'Acme Freight Corp')
    await user.type(screen.getByLabelText(/Business Registration Number/i), 'PV12345')
    await user.type(screen.getByLabelText(/Billing Address/i), '123 Galle Road, Colombo')

    await user.click(screen.getByRole('button', { name: /Complete Shipper Registration/i }))

    await waitFor(() => {
      expect(mutateAsync).toHaveBeenCalledWith({
        fullName: 'Jane Doe',
        email: 'jane@company.lk',
        password: 'SecurePass123!',
        phoneE164: '+94771234567',
        companyName: 'Acme Freight Corp',
        businessRegNo: 'PV12345',
        billingAddress: '123 Galle Road, Colombo',
      })
      expect(toast.success).toHaveBeenCalledWith(expect.stringContaining('Registration successful'))
    })
  })

  it('surfaces backend validation errors onto the specific form fields', async () => {
    const mutateAsync = vi.fn().mockRejectedValueOnce({
      code: 'VALIDATION_ERROR',
      details: [{ field: 'email', issue: 'Email address is already in use.' }],
    })
    vi.mocked(authApi.useRegisterShipperMutation).mockReturnValue({
      mutateAsync,
      isPending: false,
    })

    const user = userEvent.setup()
    renderRegisterPage('/register?role=shipper')

    await user.type(screen.getByLabelText(/Full Name/i), 'Jane Doe')
    await user.type(screen.getByLabelText(/Email Address/i), 'taken@company.lk')
    await user.type(screen.getByLabelText(/^Password/i), 'SecurePass123!')
    await user.type(screen.getByLabelText(/Company Name/i), 'Acme Freight')
    await user.type(screen.getByLabelText(/Billing Address/i), '123 Galle Road, Colombo')

    await user.click(screen.getByRole('button', { name: /Complete Shipper Registration/i }))

    await waitFor(() => {
      expect(screen.getByText('Email address is already in use.')).toBeInTheDocument()
    })
  })
})

describe('RegisterPage — Agency Registration Form', () => {
  it('renders all agency and yard inputs', () => {
    vi.mocked(authApi.useRegisterAgencyMutation).mockReturnValue({
      mutateAsync: vi.fn(),
      isPending: false,
    })

    renderRegisterPage('/register?role=agency')

    expect(screen.getByRole('heading', { name: 'Join Agency Network' })).toBeInTheDocument()
    expect(screen.getByLabelText(/Full Name/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Work Email Address/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/^Password/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Agency Organization Name/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Business Registration Number/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Yard Depot Address/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Yard Latitude/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Yard Longitude/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Complete Agency Registration/i })).toBeInTheDocument()
  })

  it('validates every agency input in advance', async () => {
    const mutateAsync = vi.fn()
    vi.mocked(authApi.useRegisterAgencyMutation).mockReturnValue({
      mutateAsync,
      isPending: false,
    })

    const user = userEvent.setup()
    renderRegisterPage('/register?role=agency')

    await user.click(screen.getByRole('button', { name: /Complete Agency Registration/i }))

    await waitFor(() => {
      expect(screen.getByText('Full name is required')).toBeInTheDocument()
      expect(screen.getByText('Email is required')).toBeInTheDocument()
      expect(screen.getByText('Password is required')).toBeInTheDocument()
      expect(screen.getByText('Agency name is required')).toBeInTheDocument()
      expect(screen.getByText('Business registration number is required')).toBeInTheDocument()
      expect(screen.getByText('Yard address is required')).toBeInTheDocument()
      expect(screen.getByText('Yard latitude is required')).toBeInTheDocument()
      expect(screen.getByText('Yard longitude is required')).toBeInTheDocument()
    })

    expect(mutateAsync).not.toHaveBeenCalled()
  })

  it('validates yard coordinate limits in advance', async () => {
    const mutateAsync = vi.fn()
    vi.mocked(authApi.useRegisterAgencyMutation).mockReturnValue({
      mutateAsync,
      isPending: false,
    })

    const user = userEvent.setup()
    renderRegisterPage('/register?role=agency')

    await user.type(screen.getByLabelText(/Yard Latitude/i), '120')
    await user.type(screen.getByLabelText(/Yard Longitude/i), '200')
    await user.click(screen.getByRole('button', { name: /Complete Agency Registration/i }))

    await waitFor(() => {
      expect(screen.getByText(/Yard latitude must be at most 90/i)).toBeInTheDocument()
      expect(screen.getByText(/Yard longitude must be at most 180/i)).toBeInTheDocument()
    })

    expect(mutateAsync).not.toHaveBeenCalled()
  })

  it('submits valid agency payload successfully and navigates', async () => {
    const mutateAsync = vi.fn().mockResolvedValueOnce({ id: 'agency-1' })
    vi.mocked(authApi.useRegisterAgencyMutation).mockReturnValue({
      mutateAsync,
      isPending: false,
    })

    const user = userEvent.setup()
    renderRegisterPage('/register?role=agency')

    await user.type(screen.getByLabelText(/Full Name/i), 'John Doe')
    await user.type(screen.getByLabelText(/Work Email Address/i), 'john@fastfreight.lk')
    await user.type(screen.getByLabelText(/^Password/i), 'StrongPass123!')
    await user.type(screen.getByLabelText(/Job Title/i), 'Fleet Manager')
    await user.type(screen.getByLabelText(/Agency Organization Name/i), 'Fast Freight Logistics')
    await user.type(screen.getByLabelText(/Business Registration Number/i), 'PV98765')
    await user.type(screen.getByLabelText(/Yard Depot Address/i), '45 Harbor Road, Peliyagoda')
    await user.type(screen.getByLabelText(/Yard Latitude/i), '6.9583')
    await user.type(screen.getByLabelText(/Yard Longitude/i), '79.8833')

    await user.click(screen.getByRole('button', { name: /Complete Agency Registration/i }))

    await waitFor(() => {
      expect(mutateAsync).toHaveBeenCalledWith({
        fullName: 'John Doe',
        email: 'john@fastfreight.lk',
        password: 'StrongPass123!',
        jobTitle: 'Fleet Manager',
        agencyName: 'Fast Freight Logistics',
        businessRegNo: 'PV98765',
        yardAddress: '45 Harbor Road, Peliyagoda',
        yardLat: 6.9583,
        yardLng: 79.8833,
      })
      expect(toast.success).toHaveBeenCalledWith(expect.stringContaining('Registration successful'))
    })
  })
})
