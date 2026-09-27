import { Formik, Form } from 'formik'
import { AlertCircle, Loader2, Pencil, X } from 'lucide-react'
import { toast } from 'sonner'
import Button from '../../../components/Button.jsx'
import { FormikTextField } from '../../../components/form/index.js'
import { useEscapeKey } from '../../../hooks/useEscapeKey.js'
import { useUpdateDriverMutation } from '../api/agencyApi.js'
import { updateDriverSchema } from '../lib/validationSchemas.js'
import { getAgencyErrorMessage, mapValidationDetailsToFormik } from '../lib/errorMessages.js'

/**
 * Slide-over right drawer for editing an existing driver's details. Email and status are not
 * editable here — email is immutable after creation and status has its own Remove/Reinstate action.
 *
 * @param {string} agencyId
 * @param {object|null} driver - the driver row being edited, or null when the drawer is closed
 * @param {() => void} onClose
 */
function EditDriverDrawer({ agencyId, driver, onClose }) {
  const updateDriverMutation = useUpdateDriverMutation()

  const isOpen = Boolean(driver)
  useEscapeKey(isOpen, onClose)

  if (!isOpen) return null

  const initialValues = {
    fullName: driver.fullName ?? '',
    licenceNo: driver.licenceNo ?? '',
    licenceExpiry: driver.licenceExpiry ? driver.licenceExpiry.slice(0, 10) : '',
  }

  async function handleSubmit(values, { setErrors, setStatus, setSubmitting }) {
    setStatus(null)

    try {
      const payload = {
        fullName: values.fullName.trim(),
        licenceNo: values.licenceNo.trim(),
        licenceExpiry: values.licenceExpiry,
      }

      await updateDriverMutation.mutateAsync({ agencyId, driverId: driver.driverId, driver: payload })

      toast.success(`Driver ${payload.fullName} updated.`)
      onClose()
    } catch (error) {
      if (error?.code === 'VALIDATION_ERROR' && error?.details) {
        setErrors(mapValidationDetailsToFormik(error.details))
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
        aria-labelledby="edit-driver-title"
        className="relative z-50 flex h-full w-full max-w-md flex-col border-l border-slate-border bg-surface-container-lowest shadow-2xl"
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-border px-6 py-4">
          <div className="flex items-center space-x-3">
            <div className="flex h-9 w-9 items-center justify-center rounded-md bg-primary-container text-on-primary">
              <Pencil className="h-5 w-5" />
            </div>
            <div>
              <h2 id="edit-driver-title" className="text-body-lg font-bold text-primary">
                Edit Driver
              </h2>
              <p className="text-body-xs text-on-surface-variant">Update {driver.fullName}'s details</p>
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
        <Formik
          initialValues={initialValues}
          validationSchema={updateDriverSchema}
          onSubmit={handleSubmit}
          enableReinitialize
        >
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
                    disabled={isSubmitting || updateDriverMutation.isPending}
                  >
                    Cancel
                  </Button>
                  <Button
                    type="submit"
                    disabled={isSubmitting || updateDriverMutation.isPending}
                    className="inline-flex items-center gap-1.5"
                  >
                    {(isSubmitting || updateDriverMutation.isPending) && (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    )}
                    {isSubmitting || updateDriverMutation.isPending ? 'Saving...' : 'Save Changes'}
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

export default EditDriverDrawer
