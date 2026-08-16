import { Formik, Form } from 'formik'
import { FormikTextField, FormikPasswordField, FormikSubmitButton } from '../../../components/form/index.js'
import { loginSchema } from '../lib/validationSchemas.js'

// Cloned from the Stitch "Login — FreightLink LK" screen. Public route, no
// auth required. Uses the shared Formik field kit (src/components/form/)
// and the existing loginSchema (src/features/auth/lib/validationSchemas.js)
// for real client-side validation — the login() thunk itself still throws
// "not implemented yet" (src/features/auth/store/authSlice.js), so submit
// stays a placeholder rather than dispatching a call that's known to fail.
function LoginPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-4">
      <div className="w-full max-w-md rounded-md border border-slate-border bg-surface-container-lowest p-8 shadow-soft">
        <div className="mb-8 text-center">
          <h1 className="text-headline-lg text-primary">FreightLink</h1>
          <p className="mt-2 text-body-md text-on-surface-variant">Secure Operations Portal</p>
        </div>

        <Formik
          initialValues={{ email: '', password: '' }}
          validationSchema={loginSchema}
          onSubmit={(values, { setSubmitting }) => {
            // TODO: dispatch(login(values)) once /auth/login is implemented
            console.log('Login submit (placeholder):', values)
            setSubmitting(false)
          }}
        >
          <Form className="space-y-form-gap">
            <FormikTextField name="email" label="Email Address" placeholder="operator@lankafreight.lk" />
            <div>
              <FormikPasswordField name="password" label="Password" placeholder="••••••••" />
              <div className="mt-2 flex justify-end">
                <a href="#" className="text-body-md text-status-blue-text hover:underline">
                  Forgot Password?
                </a>
              </div>
            </div>
            <FormikSubmitButton className="w-full">Sign In</FormikSubmitButton>
          </Form>
        </Formik>
      </div>
    </div>
  )
}

export default LoginPage
