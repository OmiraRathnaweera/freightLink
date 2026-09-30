import { useState } from 'react'
import { Formik, Form } from 'formik'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { FormikTextField, FormikPasswordField, FormikSubmitButton } from '../../../components/form/index.js'
import { loginSchema } from '../lib/validationSchemas.js'
import { useAppDispatch } from '../../../hooks/useAppDispatch.js'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { login, logout, clearAuthError } from '../store/authSlice.js'
import { getRoleHomePath } from '../lib/roleHome.js'
import { UserRole } from '../../../lib/enums.js'

// Cloned from the Stitch "Login — FreightLink LK" screen. Public route
// (wrapped in PublicRoute — src/routes/PublicRoute.jsx — so a signed-in
// user never sees this form). Uses the shared Formik field kit
// (src/components/form/) and the existing loginSchema for client-side
// validation; submit dispatches the real `login` thunk (authSlice.js),
// which itself saves tokens then calls GET /auth/me — the login response
// alone is never trusted for profile/role.
function LoginPage() {
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const location = useLocation()
  const apiError = useAppSelector((state) => state.auth.error)
  const [driverError, setDriverError] = useState(null)

  const activeError = driverError || apiError

  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-4">
      <div className="w-full max-w-md rounded-md border border-slate-border bg-surface-container-lowest p-8 shadow-soft">
        <div className="mb-8 text-center">
          <h1 className="text-headline-lg text-primary">FreightLink</h1>
          <p className="mt-2 text-body-md text-on-surface-variant">Secure Operations Portal</p>
        </div>

        {activeError && (
          <div className="mb-4 rounded-md border border-status-red-text bg-status-red-bg px-3 py-2 text-body-md text-status-red-text">
            {activeError}
          </div>
        )}

        <Formik
          initialValues={{ email: '', password: '' }}
          validationSchema={loginSchema}
          onSubmit={async (values, { setSubmitting }) => {
            dispatch(clearAuthError())
            setDriverError(null)
            try {
              const result = await dispatch(login(values)).unwrap()
              if (result.user?.role === UserRole.DRIVER) {
                dispatch(logout())
                setDriverError(
                  'Driver accounts can only access the FreightLink Mobile App. Web portal access is restricted to Shippers, Agencies, and Administrators.',
                )
                return
              }
              const from = location.state?.from?.pathname
              navigate(from ?? getRoleHomePath(result.user?.role), { replace: true })
            } catch {
              // state.auth.error is already set by the thunk's rejected
              // reducer — the banner above renders it.
            } finally {
              setSubmitting(false)
            }
          }}
        >
          <Form className="space-y-form-gap">
            <FormikTextField name="email" label="Email Address" placeholder="operator@lankafreight.lk" />
            <div>
              <FormikPasswordField name="password" label="Password" placeholder="••••••••" />
              <div className="mt-2 flex justify-end">
                <Link to="/forgot-password" className="text-body-md text-status-blue-text hover:underline">
                  Forgot Password?
                </Link>
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
