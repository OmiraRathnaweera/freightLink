import { Link } from 'react-router-dom'
import { Form, Formik } from 'formik'
import { FormikSubmitButton, FormikTextField } from '../../../components/form/index.js'
import { forgotPassword } from '../api/authApi.js'
import { forgotPasswordSchema } from '../lib/validationSchemas.js'

function ForgotPasswordPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-4">
      <div className="w-full max-w-md rounded-md border border-slate-border bg-surface-container-lowest p-8 shadow-soft">
        <h1 className="text-headline-lg text-primary">Reset your password</h1>
        <p className="mt-2 text-body-md text-on-surface-variant">
          Enter your email address and we’ll send a one-time reset link if an active account exists.
        </p>
        <Formik
          initialValues={{ email: '' }}
          validationSchema={forgotPasswordSchema}
          onSubmit={async ({ email }, { setStatus, setSubmitting }) => {
            try {
              const response = await forgotPassword(email.trim())
              setStatus({ success: response.message })
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
              <FormikTextField name="email" type="email" label="Email address" placeholder="you@example.com" />
              <FormikSubmitButton className="w-full">Send reset link</FormikSubmitButton>
            </Form>
          )}
        </Formik>
        <p className="mt-6 text-center text-body-md text-on-surface-variant">
          Remembered your password? <Link to="/login" className="font-medium text-status-blue-text hover:underline">Sign in</Link>
        </p>
      </div>
    </div>
  )
}

export default ForgotPasswordPage
