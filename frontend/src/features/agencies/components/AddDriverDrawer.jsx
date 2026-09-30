import { Formik, Form } from 'formik'
import { AlertCircle, Loader2, User, X } from 'lucide-react'
import { toast } from 'sonner'
import Button from '../../../components/Button.jsx'
import { FormikTextField } from '../../../components/form/index.js'
import { useEscapeKey } from '../../../hooks/useEscapeKey.js'
import { useAddDriverMutation } from '../api/agencyApi.js'
import { addDriverSchema } from '../lib/validationSchemas.js'
import { getAgencyErrorMessage, mapValidationDetailsToFormik } from '../lib/errorMessages.js'

const INITIAL_VALUES = {
  fullName: '',
  email: '',
  phoneE164: '',
  licenceNo: '',
  licenceExpiry: '',
}

/**
 * Slide-over right drawer for onboarding a new driver under an agency. There is no password
 * field — the server generates a temporary password and emails it to the driver (also shown
 * once here as a fallback in case delivery is delayed).
 *
 * @param {string} agencyId
 * @param {boolean} isOpen
 * @param {() => void} onClose
 */
function AddDriverDrawer({ agencyId, isOpen, onClose }) {
  const addDriverMutation = useAddDriverMutation()

  useEscapeKey(isOpen, onClose)

  if (!isOpen) return null

  async function handleSubmit(values, { setErrors, setStatus, setSubmitting, resetForm }) {
    setStatus(null)

    try {
      const payload = {
        fullName: values.fullName.trim(),
        email: values.email.trim().toLowerCase(),
        phoneE164: values.phoneE164?.trim() || undefined,
        licenceNo: values.licenceNo.trim(),
        licenceExpiry: values.licenceExpiry,
      }

      const created = await addDriverMutation.mutateAsync({ agencyId, driver: payload })

      toast.success(`Driver ${payload.fullName} added to your roster!`, {
        description: created?.temporaryPassword
          ? `Temporary password (also emailed to ${payload.email}): ${created.temporaryPassword}`
          : `Login credentials have been emailed to ${payload.email}.`,
        duration: 15000,
      })
      resetForm()
      onClose()
    } catch (error) {
      if (error?.code === 'VALIDATION_ERROR' && error?.details) {
        setErrors(mapValidationDetailsToFormik(error.details))
      } else if (error?.code === 'EMAIL_ALREADY_REGISTERED') {
        setErrors({ email: 'An account with this email already exists.' })
      } else if (error?.code === 'LICENCE_ALREADY_REGISTERED' || error?.code === 'DRIVER_LICENCE_ALREADY_REGISTERED') {
        setErrors({ licenceNo: 'This licence number is already registered to another driver.' })
      }
      const message = getAgencyErrorMessage(error)
      setStatus(message)
      toast.error(message)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex justify-end">
      {/* Backdrop */}
      <div
        onClick={onClose}
        className="fixed inset-0 bg-primary/40 backdrop-blur-xs transition-opacity"
        aria-hidden="true"
      />

      {/* Drawer */}
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="add-driver-title"
        className="relative z-50 flex h-full w-full max-w-md flex-col border-l border-slate-border bg-surface-container-lowest shadow-2xl"
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-border px-6 py-4">
          <div className="flex items-center space-x-3">
            <div className="flex h-9 w-9 items-center justify-center rounded-md bg-primary-container text-on-primary">
              <User className="h-5 w-5" />
            </div>
            <div>
              <h2 id="add-driver-title" className="text-body-lg font-bold text-primary">
                Add Driver
              </h2>
              <p className="text-body-xs text-on-surface-variant">Onboard a driver to your agency</p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md p-1.5 text-on-surface-variant hover:bg-slate-100 hover:text-on-surface"
            aria-label="Close drawer"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Content */}
        <Formik initialValues={INITIAL_VALUES} validationSchema={addDriverSchema} onSubmit={handleSubmit}>
          {({ status, isSubmitting }) => (
            <Form className="flex flex-1 flex-col justify-between overflow-y-auto p-6" noValidate>
              <div className="space-y-5">
                {status && (
                  <div
                    role="alert"
                    className="flex items-start gap-2 rounded-md border border-status-red-text bg-status-red-bg px-3.5 py-2.5 text-body-sm text-status-red-text"
                  >
                    <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                    <span>{status}</span>
                  </div>
                )}

                <FormikTextField name="fullName" label="Full Name" required placeholder="e.g. Nimal Perera" />

                <FormikTextField
                  name="email"
                  label="Login Email"
                  type="email"
                  required
                  placeholder="driver@example.com"
                  helperText="A temporary password will be generated and emailed to this address so the driver can sign in."
                />

                <FormikTextField
                  name="phoneE164"
                  label="Phone Number"
                  type="tel"
                  placeholder="+94771234567"
                  helperText="Optional — E.164 format with country code."
                />

                <div className="grid grid-cols-2 gap-4">
                  <FormikTextField name="licenceNo" label="Licence Number" required mono placeholder="e.g. B1234567" />
                  <FormikTextField name="licenceExpiry" label="Licence Expiry" type="date" required />
                </div>
              </div>

              {/* Footer buttons */}
              <div className="mt-8 border-t border-slate-border pt-4">
                <div className="flex items-center justify-end space-x-3">
                  <Button
                    type="button"
                    variant="secondary"
                    onClick={onClose}
                    disabled={isSubmitting || addDriverMutation.isPending}
                  >
                    Cancel
                  </Button>
                  <Button
                    type="submit"
                    disabled={isSubmitting || addDriverMutation.isPending}
                    className="inline-flex items-center gap-1.5"
                  >
                    {(isSubmitting || addDriverMutation.isPending) && (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    )}
                    {isSubmitting || addDriverMutation.isPending ? 'Adding...' : 'Add to Roster'}
                  </Button>
                </div>
              </div>
            </Form>
          )}
        </Formik>
      </div>
    </div>
  )
}

export default AddDriverDrawer
