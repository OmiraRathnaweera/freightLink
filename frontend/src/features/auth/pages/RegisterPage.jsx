import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Form, Formik } from 'formik'
import { ArrowRight, Building2, PackageCheck } from 'lucide-react'
import { toast } from 'sonner'
import Button from '../../../components/Button.jsx'
import Card from '../../../components/Card.jsx'
import {
  FormikPasswordField,
  FormikSubmitButton,
  FormikTextField,
  FormikYardLocationField,
} from '../../../components/form/index.js'
import { useRegisterAgencyMutation, useRegisterShipperMutation } from '../api/authApi.js'
import { getAuthErrorMessage, mapValidationDetailsToFormik } from '../lib/errorMessages.js'
import { agencyRegisterSchema, shipperRegisterSchema } from '../lib/validationSchemas.js'

const INITIAL_SHIPPER_VALUES = {
  fullName: '',
  email: '',
  password: '',
  phoneE164: '',
  companyName: '',
  businessRegNo: '',
  billingAddress: '',
}

const INITIAL_AGENCY_VALUES = {
  fullName: '',
  email: '',
  password: '',
  phoneE164: '',
  jobTitle: '',
  agencyName: '',
  businessRegNo: '',
  yardAddress: '',
  yardLat: '',
  yardLng: '',
}

export default function RegisterPage() {
  const [searchParams] = useSearchParams()
  const role = searchParams.get('role')

  if (role === 'agency') {
    return <AgencyRegisterForm />
  }

  if (role === 'shipper') {
    return <ShipperRegisterForm />
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-4">
      <div className="w-full max-w-md rounded-md border border-slate-border bg-surface-container-lowest p-8 shadow-soft">
        <div className="mb-8 text-center">
          <h1 className="text-headline-lg font-bold text-primary">FreightLink</h1>
          <h2 className="mt-2 text-body-md text-on-surface-variant">Choose your account type to register</h2>
        </div>

        <div className="space-y-4">
          <Link
            to="/register?role=shipper"
            className="group block rounded-md border border-slate-border p-4 transition-all hover:border-primary hover:bg-surface-container-low"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center space-x-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-md bg-secondary-container text-on-secondary-container">
                  <PackageCheck className="h-5 w-5" />
                </div>
                <div>
                  <h2 className="text-body-md font-semibold text-primary group-hover:text-primary">
                    Register as Shipper
                  </h2>
                  <p className="text-body-sm text-on-surface-variant">
                    Post loads, track shipments, and review AI matches
                  </p>
                </div>
              </div>
              <ArrowRight className="h-4 w-4 text-on-surface-variant group-hover:text-primary" />
            </div>
          </Link>

          <Link
            to="/register?role=agency"
            className="group block rounded-md border border-slate-border p-4 transition-all hover:border-primary hover:bg-surface-container-low"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center space-x-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-md bg-primary-container text-on-primary">
                  <Building2 className="h-5 w-5" />
                </div>
                <div>
                  <h2 className="text-body-md font-semibold text-primary group-hover:text-primary">
                    Join Agency Network
                  </h2>
                  <p className="text-body-sm text-on-surface-variant">
                    Manage fleet, dispatch drivers, and receive proposals
                  </p>
                </div>
              </div>
              <ArrowRight className="h-4 w-4 text-on-surface-variant group-hover:text-primary" />
            </div>
          </Link>
        </div>

        <div className="mt-8 border-t border-slate-border pt-6 text-center">
          <p className="text-body-md text-on-surface-variant">
            Already have an account?{' '}
            <Link to="/login" className="font-medium text-status-blue-text hover:underline">
              Sign In
            </Link>
          </p>
        </div>
      </div>
    </div>
  )
}

function ShipperRegisterForm() {
  const navigate = useNavigate()
  const registerMutation = useRegisterShipperMutation()

  const handleSubmit = async (values, { setErrors, setStatus, setSubmitting }) => {
    setStatus(undefined)
    try {
      const payload = {
        fullName: values.fullName.trim(),
        email: values.email.trim(),
        password: values.password,
        companyName: values.companyName.trim(),
        billingAddress: values.billingAddress.trim(),
      }

      if (values.phoneE164 && values.phoneE164.trim()) {
        payload.phoneE164 = values.phoneE164.trim()
      }

      if (values.businessRegNo && values.businessRegNo.trim()) {
        payload.businessRegNo = values.businessRegNo.trim()
      }

      const result = await registerMutation.mutateAsync(payload)
      toast.success('Registration successful! Check your email for the verification link.')
      navigate(`/verify-email?email=${encodeURIComponent(result.email)}`)
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
    <div className="flex min-h-screen items-center justify-center bg-background p-4 py-12">
      <Card className="w-full max-w-2xl overflow-hidden p-0">
        <Card.Header className="flex items-center justify-between border-b border-slate-border p-6">
          <div>
            <h1 className="text-headline-sm font-bold text-primary">Register as Shipper</h1>
            <p className="mt-1 text-body-md text-on-surface-variant">
              Create your shipping business profile and manager account
            </p>
          </div>
          <Button as={Link} to="/register" variant="secondary" className="text-xs">
            Switch Role
          </Button>
        </Card.Header>

        <Card.Body className="p-6">
          <Formik
            initialValues={INITIAL_SHIPPER_VALUES}
            validationSchema={shipperRegisterSchema}
            onSubmit={handleSubmit}
          >
            {({ status }) => (
              <Form className="space-y-6">
                {status && (
                  <div
                    role="alert"
                    className="rounded-md border border-status-red-text bg-status-red-bg px-4 py-3 text-body-md text-status-red-text"
                  >
                    {status}
                  </div>
                )}

                <div className="space-y-4">
                  <h2 className="border-b border-slate-border pb-2 text-body-lg font-semibold text-primary">
                    Personal & Account Details
                  </h2>
                  <div className="grid grid-cols-1 gap-form-gap md:grid-cols-2">
                    <FormikTextField
                      name="fullName"
                      label="Full Name"
                      placeholder="Jane Doe"
                      autoComplete="name"
                    />
                    <FormikTextField
                      name="email"
                      label="Email Address"
                      type="email"
                      placeholder="jane@company.lk"
                      autoComplete="email"
                    />
                    <FormikPasswordField
                      name="password"
                      label="Password"
                      placeholder="••••••••"
                      helperText="At least 8 chars with uppercase, lowercase, number & symbol"
                      autoComplete="new-password"
                    />
                    <FormikTextField
                      name="phoneE164"
                      label="Phone Number (Optional)"
                      type="tel"
                      placeholder="+94771234567"
                      helperText="E.164 format with country code"
                      autoComplete="tel"
                    />
                  </div>
                </div>

                <div className="space-y-4">
                  <h2 className="border-b border-slate-border pb-2 text-body-lg font-semibold text-primary">
                    Company Information
                  </h2>
                  <div className="grid grid-cols-1 gap-form-gap md:grid-cols-2">
                    <div className="md:col-span-2">
                      <FormikTextField
                        name="companyName"
                        label="Company Name"
                        placeholder="Acme Logistics Corp"
                      />
                    </div>
                    <div className="md:col-span-2">
                      <FormikTextField
                        name="businessRegNo"
                        label="Business Registration Number (Optional)"
                        placeholder="PV12345 or BR-98765"
                        helperText="Official company registration number if available"
                      />
                    </div>
                    <div className="md:col-span-2">
                      <FormikTextField
                        name="billingAddress"
                        label="Billing Address"
                        placeholder="123 Galle Road, Colombo 03"
                        autoComplete="street-address"
                      />
                    </div>
                  </div>
                </div>

                <div className="pt-2">
                  <FormikSubmitButton className="w-full py-2.5 text-base">
                    Complete Shipper Registration
                  </FormikSubmitButton>
                </div>

                <div className="border-t border-slate-border pt-4 text-center">
                  <p className="text-body-md text-on-surface-variant">
                    Already registered?{' '}
                    <Link to="/login" className="font-medium text-status-blue-text hover:underline">
                      Sign In
                    </Link>
                  </p>
                </div>
              </Form>
            )}
          </Formik>
        </Card.Body>
      </Card>
    </div>
  )
}

function AgencyRegisterForm() {
  const navigate = useNavigate()
  const registerMutation = useRegisterAgencyMutation()

  const handleSubmit = async (values, { setErrors, setStatus, setSubmitting }) => {
    setStatus(undefined)
    try {
      const payload = {
        fullName: values.fullName.trim(),
        email: values.email.trim(),
        password: values.password,
        agencyName: values.agencyName.trim(),
        businessRegNo: values.businessRegNo.trim(),
        yardAddress: values.yardAddress.trim(),
        yardLat: Number(values.yardLat),
        yardLng: Number(values.yardLng),
      }

      if (values.phoneE164 && values.phoneE164.trim()) {
        payload.phoneE164 = values.phoneE164.trim()
      }

      if (values.jobTitle && values.jobTitle.trim()) {
        payload.jobTitle = values.jobTitle.trim()
      }

      const result = await registerMutation.mutateAsync(payload)
      toast.success('Registration successful! Check your email for the verification link.')
      navigate(`/verify-email?email=${encodeURIComponent(result.email)}`)
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
    <div className="flex min-h-screen items-center justify-center bg-background p-4 py-12">
      <Card className="w-full max-w-2xl overflow-hidden p-0">
        <Card.Header className="flex items-center justify-between border-b border-slate-border p-6">
          <div>
            <h1 className="text-headline-sm font-bold text-primary">Join Agency Network</h1>
            <p className="mt-1 text-body-md text-on-surface-variant">
              Create your agency organization, fleet depot yard, and staff account
            </p>
          </div>
          <Button as={Link} to="/register" variant="secondary" className="text-xs">
            Switch Role
          </Button>
        </Card.Header>

        <Card.Body className="p-6">
          <Formik
            initialValues={INITIAL_AGENCY_VALUES}
            validationSchema={agencyRegisterSchema}
            onSubmit={handleSubmit}
          >
            {({ status }) => (
              <Form className="space-y-6">
                {status && (
                  <div
                    role="alert"
                    className="rounded-md border border-status-red-text bg-status-red-bg px-4 py-3 text-body-md text-status-red-text"
                  >
                    {status}
                  </div>
                )}

                <div className="space-y-4">
                  <h2 className="border-b border-slate-border pb-2 text-body-lg font-semibold text-primary">
                    Agency Staff Account
                  </h2>
                  <div className="grid grid-cols-1 gap-form-gap md:grid-cols-2">
                    <FormikTextField
                      name="fullName"
                      label="Full Name"
                      placeholder="John Doe"
                      autoComplete="name"
                    />
                    <FormikTextField
                      name="email"
                      label="Work Email Address"
                      type="email"
                      placeholder="john@agency.lk"
                      autoComplete="email"
                    />
                    <FormikPasswordField
                      name="password"
                      label="Password"
                      placeholder="••••••••"
                      helperText="At least 8 chars with uppercase, lowercase, number & symbol"
                      autoComplete="new-password"
                    />
                    <FormikTextField
                      name="phoneE164"
                      label="Phone Number (Optional)"
                      type="tel"
                      placeholder="+94771234567"
                      helperText="E.164 format with country code"
                      autoComplete="tel"
                    />
                    <div className="md:col-span-2">
                      <FormikTextField
                        name="jobTitle"
                        label="Job Title (Optional)"
                        placeholder="Fleet Operations Dispatcher"
                      />
                    </div>
                  </div>
                </div>

                <div className="space-y-4">
                  <h2 className="border-b border-slate-border pb-2 text-body-lg font-semibold text-primary">
                    Agency & Depot Yard Details
                  </h2>
                  <div className="grid grid-cols-1 gap-form-gap md:grid-cols-2">
                    <div className="md:col-span-2">
                      <FormikTextField
                        name="agencyName"
                        label="Agency Organization Name"
                        placeholder="Lanka Fast Freight Logistics"
                      />
                    </div>
                    <div className="md:col-span-2">
                      <FormikTextField
                        name="businessRegNo"
                        label="Business Registration Number"
                        placeholder="PV12345"
                        helperText="Required for verification and KYC compliance"
                      />
                    </div>
                    <div className="md:col-span-2">
                      <FormikYardLocationField
                        addressName="yardAddress"
                        latName="yardLat"
                        lngName="yardLng"
                        label="Yard Depot Location & Coordinates"
                      />
                    </div>
                  </div>
                </div>

                <div className="pt-2">
                  <FormikSubmitButton className="w-full py-2.5 text-base">
                    Complete Agency Registration
                  </FormikSubmitButton>
                </div>

                <div className="border-t border-slate-border pt-4 text-center">
                  <p className="text-body-md text-on-surface-variant">
                    Already registered?{' '}
                    <Link to="/login" className="font-medium text-status-blue-text hover:underline">
                      Sign In
                    </Link>
                  </p>
                </div>
              </Form>
            )}
          </Formik>
        </Card.Body>
      </Card>
    </div>
  )
}
