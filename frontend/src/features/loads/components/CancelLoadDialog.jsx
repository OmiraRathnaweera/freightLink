import { Formik, Form } from 'formik'
import { toast } from 'sonner'
import { FormikTextArea, FormikSubmitButton } from '../../../components/form/index.js'
import Button from '../../../components/Button.jsx'
import { useCancelLoadMutation } from '../api/loadsApi.js'
import { cancelLoadSchema } from '../lib/validationSchemas.js'
import { getLoadErrorMessage } from '../lib/errorMessages.js'

// Confirm dialog for PATCH /loads/{id}/status (docs/load-management-api.md
// Section 3.5) — only ever rendered for a Shipper-owned load in
// Draft/Posted/Matched (canCancelLoad — loadPermissions.js), by
// RowActionsMenu (table row) or LoadDetailPage (detail action bar), each
// owning their own open/close state and passing loadId + onClose.
function CancelLoadDialog({ loadId, onClose }) {
  const cancelMutation = useCancelLoadMutation(loadId)

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-primary/40 p-4">
      <div className="w-full max-w-md rounded-md border border-slate-border bg-surface-container-lowest p-6 shadow-soft">
        <h2 className="text-headline-md text-on-surface">Cancel this load?</h2>
        <p className="mt-1 text-body-md text-on-surface-variant">This can't be undone. Provide a reason for the cancellation.</p>

        <Formik
          initialValues={{ reason: '' }}
          validationSchema={cancelLoadSchema}
          onSubmit={async (values, { setStatus }) => {
            setStatus(undefined)
            try {
              await cancelMutation.mutateAsync(values)
              toast.success('Load cancelled')
              onClose()
            } catch (error) {
              setStatus(getLoadErrorMessage(error))
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
              <FormikTextArea name="reason" label="Cancellation reason" placeholder="e.g. Shipper found alternate carrier" rows={3} />
              <div className="flex justify-end gap-2 pt-2">
                <Button variant="secondary" type="button" onClick={onClose}>
                  Keep load
                </Button>
                <FormikSubmitButton variant="status" status="red">
                  Cancel load
                </FormikSubmitButton>
              </div>
            </Form>
          )}
        </Formik>
      </div>
    </div>
  )
}

export default CancelLoadDialog
