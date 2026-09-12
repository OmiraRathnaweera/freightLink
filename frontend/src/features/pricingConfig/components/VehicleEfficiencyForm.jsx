import { Formik, Form } from 'formik'
import { toast } from 'sonner'
import {
  FormikSelect,
  FormikNumberField,
  FormikTextField,
  FormikDateTimeField,
  FormikSubmitButton,
} from '../../../components/form/index.js'
import { VehicleClass } from '../../../lib/enums.js'
import { useCreateVehicleEfficiencyMutation } from '../api/pricingConfigApi.js'
import { vehicleEfficiencySchema } from '../lib/validationSchemas.js'
import { getPricingConfigErrorMessage, mapValidationDetailsToFormik } from '../lib/errorMessages.js'
import { fromDateTimeLocalInput } from '../lib/format.js'

const INITIAL_VALUES = {
  classLabel: '',
  minPayloadKg: '',
  maxPayloadKg: '',
  minVolumeM3: '',
  maxVolumeM3: '',
  fuelConsumptionLPer100Km: '',
  source: '',
  effectiveFrom: '',
}

const VEHICLE_CLASS_OPTIONS = Object.values(VehicleClass).map((value) => ({ value, label: value }))

// Insert-only, per ADR-019's versioning rule — always "Add", never "Edit".
// Built on the project's existing Formik + Yup field kit, same pattern as
// FuelRateForm. maxPayloadKg/maxVolumeM3 are left blank for the open-ended
// top tier (e.g. ContainerTruck) and simply omitted from the payload in
// that case.
function VehicleEfficiencyForm() {
  const createMutation = useCreateVehicleEfficiencyMutation()

  async function handleSubmit(values, { setStatus, setErrors, resetForm }) {
    setStatus(undefined)
    try {
      await createMutation.mutateAsync({
        classLabel: values.classLabel,
        minPayloadKg: values.minPayloadKg,
        maxPayloadKg: values.maxPayloadKg === '' ? undefined : values.maxPayloadKg,
        minVolumeM3: values.minVolumeM3,
        maxVolumeM3: values.maxVolumeM3 === '' ? undefined : values.maxVolumeM3,
        fuelConsumptionLPer100Km: values.fuelConsumptionLPer100Km,
        source: values.source.trim(),
        effectiveFrom: fromDateTimeLocalInput(values.effectiveFrom),
      })
      toast.success('Efficiency tier added')
      resetForm()
    } catch (error) {
      setStatus(getPricingConfigErrorMessage(error))
      if (error?.code === 'VALIDATION_ERROR') setErrors(mapValidationDetailsToFormik(error.details))
    }
  }

  return (
    <Formik initialValues={INITIAL_VALUES} validationSchema={vehicleEfficiencySchema} onSubmit={handleSubmit}>
      {({ status }) => (
        <Form className="space-y-4">
          <h4 className="text-headline-md text-primary">Add New Efficiency Tier</h4>

          {status && (
            <div className="rounded-md border border-status-red-text bg-status-red-bg px-3 py-2 text-body-md text-status-red-text">
              {status}
            </div>
          )}

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FormikSelect
              name="classLabel"
              label="Vehicle Class"
              placeholder="Select vehicle class…"
              options={VEHICLE_CLASS_OPTIONS}
            />
            <FormikNumberField
              name="fuelConsumptionLPer100Km"
              label="Fuel Consumption (L / 100 km)"
              step="0.01"
              min="0.01"
              mono
            />
            <FormikNumberField name="minPayloadKg" label="Min Payload (kg)" step="0.01" min="0" mono />
            <FormikNumberField
              name="maxPayloadKg"
              label="Max Payload (kg)"
              helperText="Leave blank for an open-ended tier"
              step="0.01"
              min="0"
              mono
            />
            <FormikNumberField name="minVolumeM3" label="Min Volume (m³)" step="0.01" min="0" mono />
            <FormikNumberField
              name="maxVolumeM3"
              label="Max Volume (m³)"
              helperText="Leave blank for an open-ended tier"
              step="0.01"
              min="0"
              mono
            />
            <div className="sm:col-span-2">
              <FormikTextField
                name="source"
                label="Source"
                placeholder="e.g. Fleet fuel-consumption survey, Aug 2026"
              />
            </div>
            <FormikDateTimeField name="effectiveFrom" label="Effective From" />
          </div>

          <div className="flex justify-end">
            <FormikSubmitButton>Add Efficiency Tier</FormikSubmitButton>
          </div>
        </Form>
      )}
    </Formik>
  )
}

export default VehicleEfficiencyForm
