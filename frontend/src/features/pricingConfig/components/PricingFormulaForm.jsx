import { Formik, Form } from 'formik'
import { toast } from 'sonner'
import { FormikNumberField, FormikTextField, FormikDateTimeField, FormikSubmitButton } from '../../../components/form/index.js'
import { useCreateFormulaConfigMutation } from '../api/pricingConfigApi.js'
import { pricingFormulaSchema } from '../lib/validationSchemas.js'
import { getPricingConfigErrorMessage, mapValidationDetailsToFormik } from '../lib/errorMessages.js'
import { fromDateTimeLocalInput } from '../lib/format.js'

const INITIAL_VALUES = {
  baseFare: '',
  ratePerKg: '',
  driverCostPerKm: '',
  maintenanceAllowancePerKm: '',
  marginPercent: '',
  source: '',
  effectiveFrom: '',
}

// Insert-only, per ADR-019's versioning rule — always "Add", never "Edit".
// Same pattern as FuelRateForm/VehicleEfficiencyForm.
function PricingFormulaForm() {
  const createMutation = useCreateFormulaConfigMutation()

  async function handleSubmit(values, { setStatus, setErrors, resetForm }) {
    setStatus(undefined)
    try {
      await createMutation.mutateAsync({
        baseFare: values.baseFare,
        ratePerKg: values.ratePerKg,
        driverCostPerKm: values.driverCostPerKm,
        maintenanceAllowancePerKm: values.maintenanceAllowancePerKm,
        marginPercent: values.marginPercent,
        source: values.source.trim(),
        effectiveFrom: fromDateTimeLocalInput(values.effectiveFrom),
      })
      toast.success('Pricing formula configuration added')
      resetForm()
    } catch (error) {
      setStatus(getPricingConfigErrorMessage(error))
      if (error?.code === 'VALIDATION_ERROR') setErrors(mapValidationDetailsToFormik(error.details))
    }
  }

  return (
    <Formik initialValues={INITIAL_VALUES} validationSchema={pricingFormulaSchema} onSubmit={handleSubmit}>
      {({ status }) => (
        <Form className="space-y-4">
          <h4 className="text-headline-md text-primary">Add New Configuration</h4>

          {status && (
            <div className="rounded-md border border-status-red-text bg-status-red-bg px-3 py-2 text-body-md text-status-red-text">
              {status}
            </div>
          )}

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FormikNumberField name="baseFare" label="Base Fare (LKR)" step="0.01" min="0" mono />
            <FormikNumberField name="ratePerKg" label="Rate per Kg (LKR)" step="0.01" min="0" mono />
            <FormikNumberField name="driverCostPerKm" label="Driver Cost per Km (LKR)" step="0.01" min="0" mono />
            <FormikNumberField
              name="maintenanceAllowancePerKm"
              label="Maintenance Allowance per Km (LKR)"
              step="0.01"
              min="0"
              mono
            />
            <FormikNumberField
              name="marginPercent"
              label="Margin"
              helperText="Fraction, e.g. 0.15 = 15% (max 1 = 100%)"
              step="0.01"
              min="0"
              max="1"
              mono
            />
            <div className="sm:col-span-2">
              <FormikTextField
                name="source"
                label="Source"
                placeholder="e.g. Q3 2026 pricing review"
              />
            </div>
            <FormikDateTimeField name="effectiveFrom" label="Effective From" />
          </div>

          <div className="flex justify-end">
            <FormikSubmitButton>Add Configuration</FormikSubmitButton>
          </div>
        </Form>
      )}
    </Formik>
  )
}

export default PricingFormulaForm
