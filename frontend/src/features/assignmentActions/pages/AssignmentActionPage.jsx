import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { respondToAssignmentAction } from '../api/assignmentActionsApi.js'

// Deliberately does NOT auto-fire on mount, unlike VerifyEmailPage: an email client's link
// pre-scanner (or an accidental re-open of the email) must never silently consume a
// single-use Accept/Decline token before a human actually clicks anything on this page.
function AssignmentActionPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token')
  const [state, setState] = useState({ status: token ? 'idle' : 'missing-token' })

  const handleConfirm = async () => {
    setState({ status: 'loading' })
    try {
      const result = await respondToAssignmentAction(token)
      setState({ status: 'success', result })
    } catch (error) {
      setState({ status: 'error', message: error.message || 'This link is invalid or has expired.' })
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-4">
      <div className="w-full max-w-md rounded-md border border-slate-border bg-surface-container-lowest p-8 shadow-soft">
        <h1 className="text-headline-lg text-primary">Job proposal response</h1>

        {state.status === 'missing-token' && (
          <p role="alert" className="mt-4 rounded-md bg-status-red-bg p-3 text-body-md text-status-red-text">
            This link is missing its token and cannot be used. Please use the Accept or Decline
            button from the original proposal email.
          </p>
        )}

        {state.status === 'idle' && (
          <>
            <p className="mt-4 text-body-md text-on-surface-variant">
              Click confirm to record your agency&apos;s response to this job proposal.
            </p>
            <button
              type="button"
              onClick={handleConfirm}
              className="mt-5 w-full rounded-md bg-primary px-4 py-2.5 text-body-md font-semibold text-on-primary hover:opacity-90"
            >
              Confirm response
            </button>
          </>
        )}

        {state.status === 'loading' && (
          <p role="status" className="mt-4 rounded-md bg-surface-container p-3 text-body-md text-on-surface-variant">
            Recording your response…
          </p>
        )}

        {state.status === 'success' && (
          <div className="mt-4 space-y-3">
            <p role="status" className="rounded-md bg-status-green-bg p-3 text-body-md text-status-green-text">
              Proposal <strong>{state.result.status}</strong>
              {state.result.referenceCode ? ` for load ${state.result.referenceCode}` : ''}.
            </p>
            {state.result.status === 'Accepted' && (
              <p className="text-body-sm text-on-surface-variant">
                A trip has been created for this assignment. Open the FreightLink app to view trip
                details and manage dispatch.
              </p>
            )}
          </div>
        )}

        {state.status === 'error' && (
          <p role="alert" className="mt-4 rounded-md bg-status-red-bg p-3 text-body-md text-status-red-text">
            {state.message}
          </p>
        )}

        <p className="mt-6 text-center text-body-md text-on-surface-variant">
          <Link to="/login" className="font-medium text-status-blue-text hover:underline">
            Go to FreightLink sign in
          </Link>
        </p>
      </div>
    </div>
  )
}

export default AssignmentActionPage
