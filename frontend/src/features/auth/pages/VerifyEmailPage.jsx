import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Form, Formik } from 'formik'
import { FormikSubmitButton, FormikTextField } from '../../../components/form/index.js'
import { resendVerification, verifyEmail } from '../api/authApi.js'
import { forgotPasswordSchema } from '../lib/validationSchemas.js'

function VerifyEmailPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token')
  const email = searchParams.get('email') ?? ''
  const [verification, setVerification] = useState(token ? { status: 'loading', message: 'Verifying your email address…' } : null)

  useEffect(() => {
    if (!token) return undefined

    let active = true
    verifyEmail(token)
      .then((response) => {
        if (active) setVerification({ status: 'success', message: response.message })
      })
      .catch((error) => {
        if (active) setVerification({ status: 'error', message: error.message })
      })

    return () => {
      active = false
    }
  }, [token])

  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-4">
      <div className="w-full max-w-md rounded-md border border-slate-border bg-surface-container-lowest p-8 shadow-soft">
        <h1 className="text-headline-lg text-primary">Verify your email</h1>
        {verification && (
          <p
            role={verification.status === 'error' ? 'alert' : 'status'}
            className={`mt-4 rounded-md p-3 text-body-md ${verification.status === 'error' ? 'bg-status-red-bg text-status-red-text' : verification.status === 'success' ? 'bg-status-green-bg text-status-green-text' : 'bg-surface-container text-on-surface-variant'}`}
          >
            {verification.message}
          </p>
        )}
        <p className="mt-5 text-body-md text-on-surface-variant">Need a new verification link? Enter the email address used to register.</p>
        <Formik
          initialValues={{ email }}
          enableReinitialize
          validationSchema={forgotPasswordSchema}
          onSubmit={async ({ email }, { setStatus, setSubmitting }) => {
            try {
              const response = await resendVerification(email.trim())
              setStatus({ success: response.message })
            } catch (error) {
              setStatus({ error: error.message })
            } finally {
              setSubmitting(false)
            }
          }}
        >
          {({ status }) => (
            <Form className="mt-4 space-y-4">
              {status?.success && <p role="status" className="rounded-md bg-status-green-bg p-3 text-body-md text-status-green-text">{status.success}</p>}
              {status?.error && <p role="alert" className="rounded-md bg-status-red-bg p-3 text-body-md text-status-red-text">{status.error}</p>}
              <FormikTextField name="email" type="email" label="Email address" />
              <FormikSubmitButton className="w-full">Resend verification link</FormikSubmitButton>
            </Form>
          )}
        </Formik>
        <p className="mt-6 text-center text-body-md text-on-surface-variant"><Link to="/login" className="font-medium text-status-blue-text hover:underline">Back to sign in</Link></p>
      </div>
    </div>
  )
}

export default VerifyEmailPage
