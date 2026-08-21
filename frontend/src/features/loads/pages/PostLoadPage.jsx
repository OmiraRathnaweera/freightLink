import { Formik, Form } from 'formik'
import { Link, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import PageHeader from '../../../components/PageHeader.jsx'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import {
  FormikCheckbox,
  FormikDateTimeField,
  FormikDualLocationField,
  FormikNumberField,
  FormikSubmitButton,
  FormikTextArea,
} from '../../../components/form/index.js'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { useCreateLoadMutation } from '../api/loadsApi.js'
import { createLoadSchema } from '../lib/validationSchemas.js'
import { getLoadErrorMessage, mapValidationDetailsToFormik } from '../lib/errorMessages.js'
import { canCreateLoad } from '../lib/loadPermissions.js'
import { fromDateTimeLocalInput } from '../lib/format.js'
import { LoadStatus } from '../../../lib/enums.js'

const INITIAL_VALUES = {
  cargoDescription: '',
  weightKg: '',
  volumeM3: '',
  pickupAddress: '',
  pickupLat: '',
  pickupLng: '',
  dropoffAddress: '',
  dropoffLat: '',
  dropoffLng: '',
  pickupWindowStart: '',
  pickupWindowEnd: '',
  postImmediately: false,
}

// Post a Load — POST /api/v1/loads (docs/load-management-api.md Section
// 3.1). Cloned from the Stitch "Post a Load" screen, rebuilt as a real
// Formik form against createLoadSchema (mirrors the backend's exact field
// constraints) instead of the previous uncontrolled placeholder inputs.
function PostLoadPage() {
  const navigate = useNavigate()
  const role = useAppSelector((state) => state.auth.role)
  const createMutation = useCreateLoadMutation()

  if (!canCreateLoad(role)) {
    return (
      <div className="space-y-6">
        <Card>
          <EmptyState
            title="You can't post a load"
            description="Only Shippers can create loads."
            action={
              <Button as={Link} to="/loads" variant="secondary">
                Back to Loads
              </Button>
            }
          />
        </Card>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader eyebrow="LOADS" title="Post a Load" description="Give agencies the details they need to bid on your shipment." />

      <Formik
        initialValues={INITIAL_VALUES}
        validationSchema={createLoadSchema}
        onSubmit={async (values, { setErrors, setStatus }) => {
          setStatus(undefined)
          try {
            const payload = {
              ...values,
              pickupWindowStart: fromDateTimeLocalInput(values.pickupWindowStart),
              pickupWindowEnd: fromDateTimeLocalInput(values.pickupWindowEnd),
            }
            const created = await createMutation.mutateAsync(payload)
            toast.success(created.status === LoadStatus.POSTED ? 'Load posted' : 'Load saved as draft')
            navigate(`/loads/${created.loadId}`)
          } catch (error) {
            if (error.code === 'VALIDATION_ERROR') {
              setErrors(mapValidationDetailsToFormik(error.details))
            } else {
              setStatus(getLoadErrorMessage(error))
            }
          }
        }}
      >
        {({ status }) => (
          <Form>
            <Card className="space-y-form-gap">
              {status && (
                <div className="rounded-md border border-status-red-text bg-status-red-bg px-3 py-2 text-body-md text-status-red-text">
                  {status}
                </div>
              )}

              <FormikDualLocationField
                pickupAddressName="pickupAddress"
                pickupLatName="pickupLat"
                pickupLngName="pickupLng"
                dropoffAddressName="dropoffAddress"
                dropoffLatName="dropoffLat"
                dropoffLngName="dropoffLng"
              />

              <FormikTextArea
                name="cargoDescription"
                label="Cargo Description"
                placeholder="Describe the cargo — type, packaging, handling notes"
                rows={3}
              />

              <div className="grid grid-cols-1 gap-form-gap sm:grid-cols-2">
                <FormikNumberField name="weightKg" label="Weight (kg)" mono placeholder="1200.5" />
                <FormikNumberField name="volumeM3" label="Volume (m³)" mono placeholder="8.25" />
              </div>

              <div className="grid grid-cols-1 gap-form-gap sm:grid-cols-2">
                <FormikDateTimeField name="pickupWindowStart" label="Pickup Window Start" />
                <FormikDateTimeField name="pickupWindowEnd" label="Pickup Window End" />
              </div>

              <FormikCheckbox
                name="postImmediately"
                label="Post immediately (skip Draft)"
                helperText="Leave unchecked to save as a Draft you can edit later."
              />

              <div className="flex justify-end pt-2">
                <FormikSubmitButton>Save Load</FormikSubmitButton>
              </div>
            </Card>
          </Form>
        )}
      </Formik>
    </div>
  )
}

export default PostLoadPage
