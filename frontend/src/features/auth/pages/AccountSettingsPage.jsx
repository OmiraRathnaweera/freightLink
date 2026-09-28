import { Form, Formik } from 'formik'
import { toast } from 'sonner'
import Card from '../../../components/Card.jsx'
import { FormikPasswordField, FormikSubmitButton, FormikTextField } from '../../../components/form/index.js'
import { useAppDispatch } from '../../../hooks/useAppDispatch.js'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { logout, setUser } from '../store/authSlice.js'
import { useChangePasswordMutation, useUpdateProfileMutation } from '../api/authApi.js'
import { changePasswordSchema, updateProfileSchema } from '../lib/validationSchemas.js'
import { getAuthErrorMessage, mapValidationDetailsToFormik } from '../lib/errorMessages.js'

// Account Settings — lets any signed-in role (Shipper, Agency Staff, Admin)
// update their own name/email/phone, or change their password. Reachable
// from DashboardLayout's sidebar footer ("My Account"), not from the main
// nav — it's intentionally not in roleAccess.js's ROLE_ALLOWED_PREFIXES map,
// which per isRouteAllowedForRole's own doc comment leaves a path open to
// every authenticated role rather than needing an entry per role.
function AccountSettingsPage() {
  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <h1 className="text-headline-lg text-on-surface">Account Settings</h1>
        <p className="mt-1 text-body-md text-on-surface-variant">Manage your name, email, phone, and password.</p>
      </div>
      <ProfileSection />
      <PasswordSection />
    </div>
  )
}

function ProfileSection() {
  const dispatch = useAppDispatch()
  const user = useAppSelector((state) => state.auth.user)
  const updateProfileMutation = useUpdateProfileMutation()

  const handleSubmit = async (values, { setErrors, setStatus, setSubmitting }) => {
    setStatus(undefined)
    try {
      const payload = {
        fullName: values.fullName.trim(),
        email: values.email.trim(),
      }
      if (values.phoneE164 && values.phoneE164.trim()) {
        payload.phoneE164 = values.phoneE164.trim()
      }

      const updatedUser = await updateProfileMutation.mutateAsync(payload)
      dispatch(setUser(updatedUser))
      toast.success('Profile updated successfully.')
    } catch (error) {
      if (error?.code === 'VALIDATION_ERROR' && error?.details) {
        setErrors(mapValidationDetailsToFormik(error.details))
      }
      setStatus(getAuthErrorMessage(error))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Card>
      <Card.Header className="border-b border-slate-border pb-4">
        <h2 className="text-body-lg font-semibold text-primary">Profile</h2>
      </Card.Header>
      <Card.Body className="pt-4">
        <Formik
          enableReinitialize
          initialValues={{
            fullName: user?.fullName ?? '',
            email: user?.email ?? '',
            phoneE164: user?.phoneE164 ?? '',
          }}
          validationSchema={updateProfileSchema}
          onSubmit={handleSubmit}
        >
          {({ status }) => (
            <Form className="space-y-4">
              {status && (
                <div role="alert" className="rounded-md border border-status-red-text bg-status-red-bg px-4 py-3 text-body-md text-status-red-text">
                  {status}
                </div>
              )}
              <FormikTextField name="fullName" label="Full Name" placeholder="Jane Doe" autoComplete="name" />
              <FormikTextField
                name="email"
                label="Email Address"
                type="email"
                placeholder="you@example.com"
                autoComplete="email"
                helperText="Changing your email will require you to verify it again."
              />
              <FormikTextField
                name="phoneE164"
                label="Phone Number (optional)"
                placeholder="+14155552671"
                autoComplete="tel"
              />
              <FormikSubmitButton>Save Changes</FormikSubmitButton>
            </Form>
          )}
        </Formik>
      </Card.Body>
    </Card>
  )
}

function PasswordSection() {
  const dispatch = useAppDispatch()
  const changePasswordMutation = useChangePasswordMutation()

  const handleSubmit = async (values, { setErrors, setStatus, setSubmitting, resetForm }) => {
    setStatus(undefined)
    try {
      await changePasswordMutation.mutateAsync({
        currentPassword: values.currentPassword,
        newPassword: values.newPassword,
      })
      toast.success('Password changed. Please sign in again.')
      resetForm()
      // The backend just revoked every active session, including this one —
      // clear local session state so ProtectedRoute bounces back to /login.
      dispatch(logout())
    } catch (error) {
      if (error?.code === 'VALIDATION_ERROR' && error?.details) {
        setErrors(mapValidationDetailsToFormik(error.details))
      }
      setStatus(getAuthErrorMessage(error))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Card>
      <Card.Header className="border-b border-slate-border pb-4">
        <h2 className="text-body-lg font-semibold text-primary">Change Password</h2>
      </Card.Header>
      <Card.Body className="pt-4">
        <Formik
          initialValues={{ currentPassword: '', newPassword: '' }}
          validationSchema={changePasswordSchema}
          onSubmit={handleSubmit}
        >
          {({ status }) => (
            <Form className="space-y-4">
              {status && (
                <div role="alert" className="rounded-md border border-status-red-text bg-status-red-bg px-4 py-3 text-body-md text-status-red-text">
                  {status}
                </div>
              )}
              <FormikPasswordField name="currentPassword" label="Current Password" autoComplete="current-password" />
              <FormikPasswordField
                name="newPassword"
                label="New Password"
                autoComplete="new-password"
                helperText="At least 8 characters, with an uppercase letter, a lowercase letter, a digit, and a special character."
              />
              <FormikSubmitButton>Change Password</FormikSubmitButton>
            </Form>
          )}
        </Formik>
      </Card.Body>
    </Card>
  )
}

export default AccountSettingsPage
