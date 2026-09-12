import { useState } from 'react'
import { toast } from 'sonner'
import Button from '../../../components/Button.jsx'
import { usePublishLoadMutation } from '../api/loadsApi.js'
import { getLoadErrorMessage } from '../lib/errorMessages.js'

// Confirm modal for publishing a Draft load — no fields to collect (unlike
// CancelLoadDialog's required reason), so this skips Formik entirely and
// drives the mutation directly. Mirrors CancelLoadDialog.jsx's hand-rolled
// overlay markup (no shared Dialog/Modal component exists yet) for visual
// consistency between the two load-status-change confirmations.
function PublishLoadDialog({ loadId, onClose }) {
  const [error, setError] = useState(undefined)
  const publishMutation = usePublishLoadMutation(loadId)

  async function handlePublish() {
    setError(undefined)
    try {
      await publishMutation.mutateAsync()
      toast.success('Load posted')
      onClose()
    } catch (err) {
      setError(getLoadErrorMessage(err))
    }
  }

  return (
    // z-[1100]: see CancelLoadDialog.jsx — Leaflet's zoom-control panes sit at
    // z-index:1000 and aren't isolated in their own stacking context, so a
    // plain z-50 here would render underneath RouteMapCard's map.
    <div className="fixed inset-0 z-[1100] flex items-center justify-center bg-primary/40 p-4">
      <div className="w-full max-w-md rounded-md border border-slate-border bg-surface-container-lowest p-6 shadow-soft">
        <h2 className="text-headline-md text-on-surface">Publish this load?</h2>
        <p className="mt-1 text-body-md text-on-surface-variant">
          Agencies will be able to see and bid on it once it's posted. You can't save it back to Draft afterward.
        </p>

        {error && (
          <div className="mt-4 rounded-md border border-status-red-text bg-status-red-bg px-3 py-2 text-body-md text-status-red-text">
            {error}
          </div>
        )}

        <div className="mt-4 flex justify-end gap-2 pt-2">
          <Button variant="secondary" type="button" onClick={onClose} disabled={publishMutation.isPending}>
            Not yet
          </Button>
          <Button variant="status" status="blue" onClick={handlePublish} disabled={publishMutation.isPending}>
            {publishMutation.isPending ? 'Publishing…' : 'Publish load'}
          </Button>
        </div>
      </div>
    </div>
  )
}

export default PublishLoadDialog
