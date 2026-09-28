import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { toast } from 'sonner'
import AccountSettingsPage from '../../pages/AccountSettingsPage.jsx'
import { renderWithProviders } from '../../../../test/testUtils.jsx'
import * as authApi from '../../api/authApi.js'

vi.mock('../../api/authApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useUpdateProfileMutation: vi.fn(),
    useChangePasswordMutation: vi.fn(),
  }
})

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}))

const sampleUser = {
  userId: 'user-1',
  email: 'shipper@example.com',
  fullName: 'Jane Shipper',
  phoneE164: '+14155552671',
  role: 'Shipper',
  isActive: true,
  isEmailVerified: true,
}

function renderPage(authOverrides = {}) {
  return renderWithProviders(<AccountSettingsPage />, {
    route: '/account',
    authState: {
      user: sampleUser,
      role: 'Shipper',
      isAuthenticated: true,
      ...authOverrides,
    },
  })
}

afterEach(() => {
  vi.mocked(authApi.useUpdateProfileMutation).mockReset()
  vi.mocked(authApi.useChangePasswordMutation).mockReset()
  vi.mocked(toast.success).mockReset()
  cleanup()
})

describe('AccountSettingsPage — Profile section', () => {
  it('pre-fills fields from the signed-in user', () => {
    vi.mocked(authApi.useUpdateProfileMutation).mockReturnValue({ mutateAsync: vi.fn() })
    vi.mocked(authApi.useChangePasswordMutation).mockReturnValue({ mutateAsync: vi.fn() })

    renderPage()

    expect(screen.getByLabelText(/Full Name/i)).toHaveValue('Jane Shipper')
    expect(screen.getByLabelText(/Email Address/i)).toHaveValue('shipper@example.com')
    expect(screen.getByLabelText(/Phone Number/i)).toHaveValue('+14155552671')
  })

  it('saves the edited profile, updates Redux, and shows a success toast', async () => {
    const mutateAsync = vi.fn().mockResolvedValue({ ...sampleUser, fullName: 'Jane Updated' })
    vi.mocked(authApi.useUpdateProfileMutation).mockReturnValue({ mutateAsync })
    vi.mocked(authApi.useChangePasswordMutation).mockReturnValue({ mutateAsync: vi.fn() })

    const user = userEvent.setup()
    const { store } = renderPage()

    const nameField = screen.getByLabelText(/Full Name/i)
    await user.clear(nameField)
    await user.type(nameField, 'Jane Updated')
    await user.click(screen.getByRole('button', { name: 'Save Changes' }))

    await waitFor(() => {
      expect(mutateAsync).toHaveBeenCalledWith({
        fullName: 'Jane Updated',
        email: 'shipper@example.com',
        phoneE164: '+14155552671',
      })
    })
    await waitFor(() => expect(store.getState().auth.user.fullName).toBe('Jane Updated'))
    expect(toast.success).toHaveBeenCalledWith('Profile updated successfully.')
  })

  it('shows the backend error message when the update fails', async () => {
    const mutateAsync = vi.fn().mockRejectedValue({ status: 409, code: 'EMAIL_ALREADY_REGISTERED' })
    vi.mocked(authApi.useUpdateProfileMutation).mockReturnValue({ mutateAsync })
    vi.mocked(authApi.useChangePasswordMutation).mockReturnValue({ mutateAsync: vi.fn() })

    const user = userEvent.setup()
    renderPage()

    await user.click(screen.getByRole('button', { name: 'Save Changes' }))

    await waitFor(() => {
      expect(screen.getByText('An account or organization with these details already exists.')).toBeInTheDocument()
    })
  })
})

describe('AccountSettingsPage — Password section', () => {
  it('blocks submission and shows validation errors for empty fields', async () => {
    const changePasswordMutate = vi.fn()
    vi.mocked(authApi.useUpdateProfileMutation).mockReturnValue({ mutateAsync: vi.fn() })
    vi.mocked(authApi.useChangePasswordMutation).mockReturnValue({ mutateAsync: changePasswordMutate })

    const user = userEvent.setup()
    renderPage()

    await user.click(screen.getByRole('button', { name: 'Change Password' }))

    await waitFor(() => {
      expect(screen.getByText('Current password is required')).toBeInTheDocument()
      expect(screen.getByText('Password is required')).toBeInTheDocument()
    })
    expect(changePasswordMutate).not.toHaveBeenCalled()
  })

  it('changes the password and signs the user out locally on success', async () => {
    const mutateAsync = vi.fn().mockResolvedValue({ message: 'ok' })
    vi.mocked(authApi.useUpdateProfileMutation).mockReturnValue({ mutateAsync: vi.fn() })
    vi.mocked(authApi.useChangePasswordMutation).mockReturnValue({ mutateAsync })

    const user = userEvent.setup()
    const { store } = renderPage()

    await user.type(screen.getByLabelText(/Current Password/i), 'CurrentPass1!')
    await user.type(screen.getByLabelText(/New Password/i), 'N3w$trongPass!')
    await user.click(screen.getByRole('button', { name: 'Change Password' }))

    await waitFor(() => {
      expect(mutateAsync).toHaveBeenCalledWith({
        currentPassword: 'CurrentPass1!',
        newPassword: 'N3w$trongPass!',
      })
    })
    expect(toast.success).toHaveBeenCalledWith('Password changed. Please sign in again.')
    await waitFor(() => expect(store.getState().auth.isAuthenticated).toBe(false))
  })

  it('shows the backend error message for an incorrect current password', async () => {
    const mutateAsync = vi.fn().mockRejectedValue({ status: 401, code: 'INCORRECT_CURRENT_PASSWORD' })
    vi.mocked(authApi.useUpdateProfileMutation).mockReturnValue({ mutateAsync: vi.fn() })
    vi.mocked(authApi.useChangePasswordMutation).mockReturnValue({ mutateAsync })

    const user = userEvent.setup()
    renderPage()

    await user.type(screen.getByLabelText(/Current Password/i), 'WrongPass1!')
    await user.type(screen.getByLabelText(/New Password/i), 'N3w$trongPass!')
    await user.click(screen.getByRole('button', { name: 'Change Password' }))

    await waitFor(() => {
      expect(screen.getByText('The current password you entered is incorrect.')).toBeInTheDocument()
    })
  })
})
