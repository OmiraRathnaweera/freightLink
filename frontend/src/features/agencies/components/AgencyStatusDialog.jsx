import { Formik, Form } from 'formik'
import { toast } from 'sonner'
import { FormikTextArea, FormikSubmitButton } from '../../../components/form/index.js'
import Button from '../../../components/Button.jsx'
import { AgencyStatus } from '../../../lib/enums.js'
import { useUpdateAgencyStatusMutation } from '../api/agencyApi.js'
import { getAgencyErrorMessage } from '../lib/errorMessages.js'
import { agencyStatusChangeSchema } from '../lib/validationSchemas.js'

const COPY = {
  [AgencyStatus.SUSPENDED]: {
    title: 'Suspend this agency?',
    description:
      "The agency won't appear in load matching and its staff can't make changes until it's reactivated. Loads already assigned to it are not changed.",
    label: 'Suspension reason',
    placeholder: 'e.g. Compliance document under review',
    confirm: 'Suspend agency',
    success: 'Agency suspended',
    tone: 'red',
  },
  [AgencyStatus.ACTIVE]: {
    title: 'Reactivate this agency?',
    description:
      'The agency will be active again: its staff can work normally and it can appear in load matching. Loads already assigned to it are not changed.',
    label: 'Reactivation reason',
    placeholder: 'e.g. Suspended in error',
    confirm: 'Reactivate agency',
    success: 'Agency reactivated',
    tone: 'green',
  },
}

// Confirm dialog for PATCH /agencies/{id}/status (issue #56). Rendered by AgenciesPage for one
// agency at a time; `targetStatus` is Suspended or Active (reactivation), and the page owns the
// open/close state, passing agency + onClose.
function AgencyStatusDialog({ agency, targetStatus, onClose }) {
  const mutation = useUpdateAgencyStatusMutation()
  const copy = COPY[targetStatus]

  return (
    // z-[1100]: stays above Leaflet's panes (see CancelLoadDialog).
    <div className="fixed inset-0 z-[1100] flex items-center justify-center bg-primary/40 p-4">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="agency-status-dialog-title"
        className="w-full max-w-md rounded-md border border-slate-border bg-surface-container-lowest p-6 shadow-soft"
      >
        <h2 id="agency-status-dialog-title" className="text-headline-md text-on-surface">
          {copy.title}
        </h2>
        <p className="mt-1 text-body-md font-semibold text-on-surface">{agency.name}</p>
        <p className="mt-1 text-body-md text-on-surface-variant">{copy.description}</p>

        <Formik
          initialValues={{ reason: '' }}
          validationSchema={agencyStatusChangeSchema}
          onSubmit={async (values, { setStatus }) => {
            setStatus(undefined)
            try {
              await mutation.mutateAsync({
                agencyId: agency.agencyId,
                status: targetStatus,
                reason: values.reason.trim(),
              })
              toast.success(copy.success)
              onClose()
            } catch (error) {
              setStatus(getAgencyErrorMessage(error))
            }
          }}
        >
          {({ status }) => (
            <Form className="mt-4 space-y-form-gap">
              {status && (
                <div className="rounded-md border border-status-red-text bg-status-red-bg px-3 py-2 text-body-md text-status-red-text">
                  {status}
                </div>
              )}
              <FormikTextArea name="reason" label={copy.label} placeholder={copy.placeholder} rows={3} />
              <div className="flex justify-end gap-2 pt-2">
                <Button variant="secondary" type="button" onClick={onClose}>
                  Cancel
                </Button>
                <FormikSubmitButton variant="status" status={copy.tone}>
                  {copy.confirm}
                </FormikSubmitButton>
              </div>
            </Form>
          )}
        </Formik>
      </div>
    </div>
  )
}

export default AgencyStatusDialog
