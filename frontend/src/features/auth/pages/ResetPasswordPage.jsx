import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Form, Formik } from 'formik'
import { toast } from 'sonner'
import { FormikPasswordField, FormikSubmitButton } from '../../../components/form/index.js'
import { resetPassword } from '../api/authApi.js'
import { resetPasswordSchema } from '../lib/validationSchemas.js'

function ResetPasswordPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token')

  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-4">
      <div className="w-full max-w-md rounded-md border border-slate-border bg-surface-container-lowest p-8 shadow-soft">
        <h1 className="text-headline-lg text-primary">Choose a new password</h1>
        {!token ? (
          <p role="alert" className="mt-4 rounded-md bg-status-red-bg p-3 text-body-md text-status-red-text">
            This password-reset link is missing its token. Request a new reset email.
          </p>
        ) : (
          <Formik
            initialValues={{ password: '', confirmPassword: '' }}
            validationSchema={resetPasswordSchema}
            onSubmit={async ({ password }, { setStatus, setSubmitting }) => {
              try {
                const response = await resetPassword({ token, newPassword: password })
                toast.success(response.message)
                navigate('/login', { replace: true })
              } catch (error) {
                setStatus({ error: error.message })
              } finally {
                setSubmitting(false)
              }
            }}
          >
            {({ status }) => (
              <Form className="mt-6 space-y-5">
                {status?.success && <p role="status" className="rounded-md bg-status-green-bg p-3 text-body-md text-status-green-text">{status.success}</p>}
                {status?.error && <p role="alert" className="rounded-md bg-status-red-bg p-3 text-body-md text-status-red-text">{status.error}</p>}
                <FormikPasswordField name="password" label="New password" helperText="Use 8+ characters with uppercase, lowercase, number, and special character." />
                <FormikPasswordField name="confirmPassword" label="Confirm new password" />
                <FormikSubmitButton className="w-full">Reset password</FormikSubmitButton>
              </Form>
            )}
          </Formik>
        )}
        <p className="mt-6 text-center text-body-md text-on-surface-variant">
          <Link to="/forgot-password" className="font-medium text-status-blue-text hover:underline">Request another reset link</Link>
        </p>
      </div>
    </div>
  )
}

export default ResetPasswordPage
