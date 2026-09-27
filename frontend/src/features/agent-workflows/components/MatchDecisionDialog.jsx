import { Formik, Form } from 'formik'
import * as Yup from 'yup'
import { FormikTextArea, FormikSubmitButton } from '../../../components/form/index.js'
import Button from '../../../components/Button.jsx'
import { useRejectMatchMutation, useReviseMatchMutation } from '../api/agentWorkflowsApi.js'

const reasonSchema = Yup.object({
  reason: Yup.string()
    .trim()
    .min(5, 'Reason must be at least 5 characters')
    .max(1000, 'Reason must be 1000 characters or fewer')
    .required('A reason is required'),
})

const COPY = {
  reject: {
    title: 'Reject this match recommendation?',
    description: "Agent 3's current recommendation will be discarded. Tell us why so we can keep a record.",
    submitLabel: 'Reject match',
    submitStatus: 'red',
  },
  revise: {
    title: 'Request a revised match?',
    description: 'Agent 3 will be asked to recompute a new recommendation for this load. Let us know what should change.',
    submitLabel: 'Request revision',
    submitStatus: 'amber',
  },
}

// Modal for POST /loads/{loadId}/match/reject and /match/revise — rendered by
// AgentWorkflowConsolePage when the shipper clicks Reject/Revise on
// MatchRecommendationCard. z-[1100] matches CancelLoadDialog's convention
// (must clear Leaflet's zoom-control panes, which sit at z-index:1000).
function MatchDecisionDialog({ loadId, decisionType, onClose, onDecided }) {
  const rejectMutation = useRejectMatchMutation()
  const reviseMutation = useReviseMatchMutation()
  const mutation = decisionType === 'reject' ? rejectMutation : reviseMutation
  const copy = COPY[decisionType]

  return (
    <div className="fixed inset-0 z-[1100] flex items-center justify-center bg-primary/40 p-4">
      <div className="w-full max-w-md rounded-md border border-slate-border bg-surface-container-lowest p-6 shadow-soft">
        <h2 className="text-headline-md text-on-surface">{copy.title}</h2>
        <p className="mt-1 text-body-md text-on-surface-variant">{copy.description}</p>

        <Formik
          initialValues={{ reason: '' }}
          validationSchema={reasonSchema}
          onSubmit={async (values, { setStatus }) => {
            setStatus(undefined)
            try {
              const result = await mutation.mutateAsync({ loadId, reason: values.reason })
              onDecided?.(result)
              onClose()
            } catch (error) {
              setStatus(
                error?.response?.data?.error?.message ||
                  error?.response?.data?.message ||
                  error?.message ||
                  'Failed to submit your decision. Please try again.'
              )
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
              <FormikTextArea
                name="reason"
                label="Reason"
                placeholder="e.g. Price is above budget, need a different vehicle class"
                rows={3}
              />
              <div className="flex justify-end gap-2 pt-2">
                <Button variant="secondary" type="button" onClick={onClose}>
                  Cancel
                </Button>
                <FormikSubmitButton variant="status" status={copy.submitStatus}>
                  {copy.submitLabel}
                </FormikSubmitButton>
              </div>
            </Form>
          )}
        </Formik>
      </div>
    </div>
  )
}

export default MatchDecisionDialog
