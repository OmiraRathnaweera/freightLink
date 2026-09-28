import { Formik, Form } from 'formik'
import { AlertCircle, Loader2, Truck, X } from 'lucide-react'
import { toast } from 'sonner'
import Button from '../../../components/Button.jsx'
import { FormikNumberField, FormikTextField } from '../../../components/form/index.js'
import { useEscapeKey } from '../../../hooks/useEscapeKey.js'
import { useAddVehicleMutation, useUpdateVehicleMutation } from '../api/agencyApi.js'
import { addVehicleSchema, updateVehicleSchema } from '../lib/validationSchemas.js'
import { getAgencyErrorMessage, mapValidationDetailsToFormik } from '../lib/errorMessages.js'
import { VEHICLE_TYPE_CONFIG, VEHICLE_TYPES } from '../lib/vehicleClasses.js'

const INITIAL_VALUES = {
  vehicleType: 'Lorry',
  registrationNo: '',
  capacityKg: '',
  volumeM3: '',
}

/**
 * Slide-over right drawer for adding or editing a vehicle in an agency's fleet.
 * Validates with Formik + Yup and maps backend error details directly onto fields.
 *
 * @param {string} agencyId
 * @param {boolean} isOpen
 * @param {object|null} vehicle Existing vehicle to edit, or null when adding.
 * @param {() => void} onClose
 */
function AddVehicleDrawer({ agencyId, isOpen, vehicle = null, onClose }) {
  const addVehicleMutation = useAddVehicleMutation()
  const updateVehicleMutation = useUpdateVehicleMutation()
  const isEditing = Boolean(vehicle)
  const isPending = addVehicleMutation.isPending || updateVehicleMutation.isPending

  useEscapeKey(isOpen, onClose)

  if (!isOpen) return null

  const initialValues = isEditing
    ? {
        vehicleType: vehicle.vehicleType ?? 'Lorry',
        registrationNo: vehicle.registrationNo ?? '',
        capacityKg: vehicle.capacityKg ?? '',
        volumeM3: vehicle.volumeM3 ?? '',
      }
    : INITIAL_VALUES

  async function handleSubmit(values, { setErrors, setStatus, setSubmitting, resetForm }) {
    setStatus(null)

    try {
      const payload = {
        vehicleType: values.vehicleType,
        registrationNo: values.registrationNo.trim().toUpperCase(),
        capacityKg: Number(values.capacityKg),
        volumeM3: Number(values.volumeM3),
      }

      if (isEditing) {
        await updateVehicleMutation.mutateAsync({ agencyId, vehicleId: vehicle.vehicleId, vehicle: payload })
      } else {
        await addVehicleMutation.mutateAsync({ agencyId, vehicle: payload })
      }

      toast.success(isEditing
        ? `Vehicle ${payload.registrationNo} updated.`
        : `Vehicle ${payload.registrationNo} registered successfully!`)
      resetForm()
      onClose()
    } catch (error) {
      if (error?.code === 'VALIDATION_ERROR' && error?.details) {
        setErrors(mapValidationDetailsToFormik(error.details))
      } else if (error?.code === 'VEHICLE_REGISTRATION_ALREADY_EXISTS') {
        setErrors({ registrationNo: 'This registration number is already in your fleet.' })
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
        aria-labelledby={isEditing ? 'edit-vehicle-title' : 'add-vehicle-title'}
        className="relative z-50 flex h-full w-full max-w-md flex-col border-l border-slate-border bg-surface-container-lowest shadow-2xl"
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-border px-6 py-4">
          <div className="flex items-center space-x-3">
            <div className="flex h-9 w-9 items-center justify-center rounded-md bg-primary-container text-on-primary">
              <Truck className="h-5 w-5" />
            </div>
            <div>
              <h2 id={isEditing ? 'edit-vehicle-title' : 'add-vehicle-title'} className="text-body-lg font-bold text-primary">
                {isEditing ? 'Edit Fleet Vehicle' : 'Add Fleet Vehicle'}
              </h2>
              <p className="text-body-xs text-on-surface-variant">
                {isEditing ? `Update ${vehicle.registrationNo}'s details` : 'Register a new truck or lorry'}
              </p>
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
          validationSchema={isEditing ? updateVehicleSchema(vehicle.registrationNo) : addVehicleSchema}
          onSubmit={handleSubmit}
          enableReinitialize
        >
          {({ values, errors, touched, status, isSubmitting, setFieldValue }) => {
            const activeTypeConfig =
              VEHICLE_TYPE_CONFIG[values.vehicleType] || VEHICLE_TYPE_CONFIG.Lorry

            return (
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

                  <div>
                    <label className="mb-2 block text-body-md font-semibold text-primary">
                      Vehicle Type <span className="text-status-red-text">*</span>
                    </label>
                    <div className="space-y-2">
                      {VEHICLE_TYPES.map((type) => (
                        <label
                          key={type.value}
                          className={`flex cursor-pointer items-start space-x-3 rounded-lg border p-3 transition-colors ${
                            values.vehicleType === type.value
                              ? 'border-primary bg-primary/5 ring-1 ring-primary/20'
                              : 'border-slate-border hover:bg-slate-50'
                          }`}
                        >
                          <input
                            type="radio"
                            id={`vehicle-type-${type.value}`}
                            name="vehicleType"
                            value={type.value}
                            aria-label={type.label}
                            checked={values.vehicleType === type.value}
                            onChange={() => setFieldValue('vehicleType', type.value)}
                            className="mt-1 text-primary focus:ring-primary"
                          />
                          <div>
                            <p className="text-body-md font-medium text-on-surface">{type.label}</p>
                            <p className="text-body-xs text-on-surface-variant">{type.desc}</p>
                          </div>
                        </label>
                      ))}
                    </div>
                    {touched.vehicleType && errors.vehicleType && (
                      <p role="alert" className="mt-1 text-body-xs text-status-red-text">
                        {errors.vehicleType}
                      </p>
                    )}
                  </div>

                  <div>
                    <FormikTextField
                      name="registrationNo"
                      label="Registration Number"
                      required
                      mono
                      placeholder="e.g. WP AB-1234 or CAB-5678"
                      helperText="Sri Lankan registration format (e.g. WP-CAB-1234, WP-CAD-1020, or CAB-5678)"
                    />
                  </div>

                  <div className="grid grid-cols-2 gap-4">
                    <FormikNumberField
                      name="capacityKg"
                      label="Capacity (Kg)"
                      required
                      mono
                      min={activeTypeConfig.minCapacityKg}
                      max={activeTypeConfig.maxCapacityKg}
                      step="any"
                      placeholder={activeTypeConfig.placeholderCapacity}
                      helperText={activeTypeConfig.capacityHint}
                    />

                    <FormikNumberField
                      name="volumeM3"
                      label="Volume (m³)"
                      required
                      mono
                      min="0.1"
                      step="any"
                      placeholder={activeTypeConfig.placeholderVolume || '18'}
                    />
                  </div>
                </div>

              {/* Footer buttons */}
              <div className="mt-8 border-t border-slate-border pt-4">
                <div className="flex items-center justify-end space-x-3">
                  <Button
                    type="button"
                    variant="secondary"
                    onClick={onClose}
                    disabled={isSubmitting || isPending}
                  >
                    Cancel
                  </Button>
                  <Button
                    type="submit"
                    disabled={isSubmitting || isPending}
                    className="inline-flex items-center gap-1.5"
                  >
                    {(isSubmitting || isPending) && (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    )}
                    {isSubmitting || isPending
                      ? (isEditing ? 'Saving...' : 'Registering...')
                      : (isEditing ? 'Save Changes' : 'Add to Fleet')}
                  </Button>
                </div>
              </div>
            </Form>
          )}}
        </Formik>
      </div>
    </div>
  )
}

export default AddVehicleDrawer
