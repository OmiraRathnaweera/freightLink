import { useState } from 'react'
import { Formik, Form } from 'formik'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import ErrorState from '../../../components/ErrorState.jsx'
import Skeleton from '../../../components/Skeleton.jsx'
import { FormikDateTimeField, FormikDualLocationField, FormikNumberField, FormikSubmitButton, FormikTextArea } from '../../../components/form/index.js'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { useLoadDetailQuery, useUpdateLoadMutation } from '../api/loadsApi.js'
import { editLoadSchema } from '../lib/validationSchemas.js'
import { getLoadErrorMessage, mapValidationDetailsToFormik } from '../lib/errorMessages.js'
import { canPublishLoad, isLoadEditable } from '../lib/loadPermissions.js'
import { fromDateTimeLocalInput, toDateTimeLocalInput } from '../lib/format.js'
import PublishLoadDialog from '../components/PublishLoadDialog.jsx'

// Edit Load — PUT /api/v1/loads/{id}, only while Draft/Posted
// (docs/load-management-api.md Section 3.4). Same field set as Post a
// Load minus postImmediately (status isn't editable through this
// endpoint).
function EditLoadPage() {
  const { loadId } = useParams()
  const navigate = useNavigate()
  const [isPublishOpen, setIsPublishOpen] = useState(false)
  const role = useAppSelector((state) => state.auth.role)
  const loadQuery = useLoadDetailQuery(loadId)
  const updateMutation = useUpdateLoadMutation(loadId)

  if (loadQuery.isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-96 w-full" />
      </div>
    )
  }

  if (loadQuery.isError) {
    return (
      <Card>
        <ErrorState description={getLoadErrorMessage(loadQuery.error)} onRetry={loadQuery.refetch} />
      </Card>
    )
  }

  const load = loadQuery.data

  if (!isLoadEditable(load.status)) {
    return (
      <div className="space-y-6">
        <Card>
          <EmptyState
            title="This load can no longer be edited"
            description={`Loads in "${load.status}" status cannot be changed.`}
            action={
              <Button as={Link} to={`/loads/${load.loadId}`} variant="secondary">
                Back to load
              </Button>
            }
          />
        </Card>
      </div>
    )
  }

  const initialValues = {
    cargoDescription: load.cargoDescription,
    weightKg: load.weightKg,
    volumeM3: load.volumeM3,
    pickupAddress: load.pickupAddress,
    pickupLat: load.pickupLat,
    pickupLng: load.pickupLng,
    dropoffAddress: load.dropoffAddress,
    dropoffLat: load.dropoffLat,
    dropoffLng: load.dropoffLng,
    pickupWindowStart: toDateTimeLocalInput(load.pickupWindowStart),
    pickupWindowEnd: toDateTimeLocalInput(load.pickupWindowEnd),
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-headline-lg text-on-surface">Edit Load {load.referenceCode}</h1>
        <div className="flex items-center gap-2">
          {canPublishLoad(role, load.status) && (
            <Button variant="status" status="blue" onClick={() => setIsPublishOpen(true)}>
              Publish
            </Button>
          )}
          <Button as={Link} to={`/loads/${load.loadId}`} variant="secondary">
            Discard
          </Button>
        </div>
      </div>

      <Formik
        initialValues={initialValues}
        validationSchema={editLoadSchema}
        onSubmit={async (values, { setErrors, setStatus }) => {
          setStatus(undefined)
          try {
            const payload = {
              ...values,
              pickupWindowStart: fromDateTimeLocalInput(values.pickupWindowStart),
              pickupWindowEnd: fromDateTimeLocalInput(values.pickupWindowEnd),
            }
            await updateMutation.mutateAsync(payload)
            toast.success('Load updated')
            navigate(`/loads/${load.loadId}`)
          } catch (error) {
            // 422 INVALID_LOAD_STATUS_TRANSITION and 409
            // LOAD_CONCURRENCY_CONFLICT both fall through to the banner —
            // neither maps to a specific field.
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

              <FormikTextArea name="cargoDescription" label="Cargo Description" rows={3} />

              <div className="grid grid-cols-1 gap-form-gap sm:grid-cols-2">
                <FormikNumberField name="weightKg" label="Total Weight (kg)" mono />
                <FormikNumberField name="volumeM3" label="Volume (m³)" mono />
              </div>

              <div className="grid grid-cols-1 gap-form-gap sm:grid-cols-2">
                <FormikDateTimeField name="pickupWindowStart" label="Pickup Window Start" />
                <FormikDateTimeField name="pickupWindowEnd" label="Pickup Window End" />
              </div>

              <div className="flex justify-end gap-2 pt-2">
                <Button as={Link} to={`/loads/${load.loadId}`} variant="secondary">
                  Cancel
                </Button>
                <FormikSubmitButton>Save changes</FormikSubmitButton>
              </div>
            </Card>
          </Form>
        )}
      </Formik>

      {isPublishOpen && <PublishLoadDialog loadId={load.loadId} onClose={() => setIsPublishOpen(false)} />}
    </div>
  )
}

export default EditLoadPage
