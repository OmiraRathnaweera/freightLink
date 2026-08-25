import { Formik, Form } from 'formik'
import { toast } from 'sonner'
import {
  FormikSelect,
  FormikNumberField,
  FormikTextField,
  FormikDateTimeField,
  FormikSubmitButton,
} from '../../../components/form/index.js'
import { FuelType } from '../../../lib/enums.js'
import { useCreateFuelRateMutation } from '../api/pricingConfigApi.js'
import { fuelRateSchema } from '../lib/validationSchemas.js'
import { getPricingConfigErrorMessage, mapValidationDetailsToFormik } from '../lib/errorMessages.js'
import { fromDateTimeLocalInput } from '../lib/format.js'

const INITIAL_VALUES = { fuelType: '', pricePerLitre: '', source: '', effectiveFrom: '' }

const FUEL_TYPE_OPTIONS = Object.values(FuelType).map((value) => ({ value, label: value }))

// Insert-only, per ADR-019's versioning rule — there is no in-place edit,
// so this form is always "Add", never "Edit". Built on the project's
// existing Formik + Yup field kit (src/components/form/), same pattern as
// CancelLoadDialog/PostLoadPage — Yup schema stays minimal (required +
// numeric-only) since the backend's DataAnnotations remain the source of
// truth for business rules.
function FuelRateForm() {
  const createMutation = useCreateFuelRateMutation()

  async function handleSubmit(values, { setStatus, setErrors, resetForm }) {
    setStatus(undefined)
    try {
      await createMutation.mutateAsync({
        fuelType: values.fuelType,
        pricePerLitre: values.pricePerLitre,
        source: values.source.trim(),
        effectiveFrom: fromDateTimeLocalInput(values.effectiveFrom),
      })
      toast.success('Fuel rate added')
      resetForm()
    } catch (error) {
      setStatus(getPricingConfigErrorMessage(error))
      if (error?.code === 'VALIDATION_ERROR') setErrors(mapValidationDetailsToFormik(error.details))
    }
  }

  return (
    <Formik initialValues={INITIAL_VALUES} validationSchema={fuelRateSchema} onSubmit={handleSubmit}>
      {({ status }) => (
        <Form className="space-y-4">
          <h4 className="text-headline-md text-primary">Add New Fuel Rate</h4>

          {status && (
            <div className="rounded-md border border-status-red-text bg-status-red-bg px-3 py-2 text-body-md text-status-red-text">
              {status}
            </div>
          )}

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FormikSelect name="fuelType" label="Fuel Type" placeholder="Select fuel type…" options={FUEL_TYPE_OPTIONS} />
            <FormikNumberField name="pricePerLitre" label="Price per Litre (LKR)" step="0.01" min="0.01" mono />
            <div className="sm:col-span-2">
              <FormikTextField name="source" label="Source" placeholder="e.g. CPC official price list, ceypetco.gov.lk" />
            </div>
            <FormikDateTimeField name="effectiveFrom" label="Effective From" />
          </div>

          <div className="flex justify-end">
            <FormikSubmitButton>Add Fuel Rate</FormikSubmitButton>
          </div>
        </Form>
      )}
    </Formik>
  )
}

export default FuelRateForm
