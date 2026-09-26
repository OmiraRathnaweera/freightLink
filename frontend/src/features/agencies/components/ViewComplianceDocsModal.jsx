import { useState } from 'react'
import { ExternalLink, FileText, Loader2, X, CheckCircle2, XCircle } from 'lucide-react'
import { toast } from 'sonner'
import Button from '../../../components/Button.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import { useEscapeKey } from '../../../hooks/useEscapeKey.js'
import { getFileUrl } from '../../../lib/api/fileUrl.js'
import { ComplianceDocStatus } from '../../../lib/enums.js'
import { 
  useComplianceDocsQuery, 
  useVerifyComplianceDocMutation, 
  useRejectComplianceDocMutation 
} from '../api/agencyApi.js'
import { getComplianceDocTypeLabel } from '../lib/complianceDocTypes.js'
import { getComplianceDocStatusTone } from '../lib/statusTone.js'

export default function ViewComplianceDocsModal({ isOpen, onClose, agencyId, agencyName }) {
  useEscapeKey(onClose, isOpen)

  const { data: docs, isLoading, isError } = useComplianceDocsQuery(agencyId, {
    enabled: isOpen && !!agencyId,
  })

  const [actionedDocIds, setActionedDocIds] = useState(() => new Set())

  const verifyDocMutation = useVerifyComplianceDocMutation({
    onSuccess: () => toast.success('Document verified.'),
    onError: (err, variables) => {
      if (variables?.docId) {
        setActionedDocIds((prev) => {
          const next = new Set(prev)
          next.delete(variables.docId)
          return next
        })
      }
      toast.error(`Failed to verify document: ${err.message}`)
    },
  })

  const rejectDocMutation = useRejectComplianceDocMutation({
    onSuccess: () => toast.success('Document rejected.'),
    onError: (err, variables) => {
      if (variables?.docId) {
        setActionedDocIds((prev) => {
          const next = new Set(prev)
          next.delete(variables.docId)
          return next
        })
      }
      toast.error(`Failed to reject document: ${err.message}`)
    },
  })

  function handleVerifyDoc(docId) {
    setActionedDocIds((prev) => new Set(prev).add(docId))
    verifyDocMutation.mutate({ agencyId, docId })
  }

  function handleRejectDoc(docId) {
    setActionedDocIds((prev) => new Set(prev).add(docId))
    rejectDocMutation.mutate({ agencyId, docId })
  }

  if (!isOpen) return null

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      {/* Backdrop */}
      <div
        onClick={onClose}
        className="fixed inset-0 bg-primary/40 backdrop-blur-xs transition-opacity"
        aria-hidden="true"
      />

      {/* Modal Dialog Card */}
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="view-docs-title"
        className="relative z-50 w-full max-w-4xl max-h-[90vh] flex flex-col rounded-xl border border-slate-border bg-surface-container-lowest shadow-2xl"
      >
        {/* Header */}
        <div className="flex shrink-0 items-start justify-between border-b border-slate-border p-6">
          <div className="flex items-center space-x-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 text-primary">
              <FileText className="h-5 w-5" />
            </div>
            <div>
              <h2 id="view-docs-title" className="text-title-md font-bold text-on-surface">
                Compliance Documents
              </h2>
              <p className="text-body-sm text-on-surface-variant">
                Viewing documents for <span className="font-semibold text-slate-700">{agencyName}</span>
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-600 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto p-6">
          {isLoading && (
            <div className="flex flex-col items-center justify-center py-12 text-slate-500">
              <Loader2 className="h-8 w-8 animate-spin text-primary mb-4" />
              <p>Loading documents...</p>
            </div>
          )}

          {isError && (
            <div className="rounded-lg bg-red-50 p-6 text-center text-red-600 border border-red-100">
              Failed to load compliance documents. Please try again.
            </div>
          )}

          {!isLoading && !isError && (!docs || docs.length === 0) && (
            <div className="rounded-lg bg-slate-50 p-8 text-center border border-slate-100">
              <FileText className="h-12 w-12 text-slate-300 mx-auto mb-3" />
              <p className="text-slate-500 font-medium">No documents found</p>
              <p className="text-body-sm text-slate-400 mt-1">This agency hasn't uploaded any compliance documents yet.</p>
            </div>
          )}

          {!isLoading && !isError && docs && docs.length > 0 && (
            <div className="overflow-x-auto rounded-lg border border-slate-200">
              <table className="w-full text-left text-sm">
                <thead className="bg-slate-50 text-slate-600 border-b border-slate-200">
                  <tr>
                    <th className="px-4 py-3 font-medium">Document Type</th>
                    <th className="px-4 py-3 font-medium">Doc Number</th>
                    <th className="px-4 py-3 font-medium">Expires On</th>
                    <th className="px-4 py-3 font-medium">Status</th>
                    <th className="px-4 py-3 font-medium text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {docs.map((doc) => {
                    const isThisDocPending = (mutation) =>
                      mutation.isPending &&
                      mutation.variables?.docId === doc.complianceDocId

                    const isVerifyingThis = isThisDocPending(verifyDocMutation)
                    const isRejectingThis = isThisDocPending(rejectDocMutation)
                    const isUpdatingThis = isVerifyingThis || isRejectingThis
                    const isActioned = actionedDocIds.has(doc.complianceDocId)
                    const canAction = doc.status === ComplianceDocStatus.PENDING && !isActioned

                    return (
                      <tr key={doc.complianceDocId} className="hover:bg-slate-50/50">
                        <td className="px-4 py-3 font-medium text-slate-900">
                          {getComplianceDocTypeLabel(doc.docType)}
                        </td>
                        <td className="px-4 py-3 text-slate-600 font-mono text-xs">
                          {doc.docNumber || 'N/A'}
                        </td>
                        <td className="px-4 py-3 text-slate-600">
                          {doc.expiresOn ? new Date(doc.expiresOn).toLocaleDateString() : 'N/A'}
                        </td>
                        <td className="px-4 py-3">
                          <StatusBadge tone={getComplianceDocStatusTone(doc.status)}>
                            {doc.status}
                          </StatusBadge>
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex items-center justify-end gap-2">
                            {doc.storageKey && (
                              <a
                                href={getFileUrl(doc.storageKey)}
                                target="popup"
                                rel="noreferrer"
                                onClick={(e) => {
                                  const url = getFileUrl(doc.storageKey)
                                  const popup = window.open(
                                    url,
                                    'popup',
                                    'width=900,height=800,scrollbars=yes,resizable=yes',
                                  )
                                  if (popup) {
                                    e.preventDefault()
                                  }
                                }}
                                className="inline-flex items-center gap-1 font-medium text-primary hover:underline mr-2"
                              >
                                View <ExternalLink className="h-3.5 w-3.5" />
                              </a>
                            )}

                            {canAction ? (
                              <>
                                <Button
                                  variant="status"
                                  status="green"
                                  className="!px-2.5 !py-1 text-xs"
                                  onClick={() => handleVerifyDoc(doc.complianceDocId)}
                                >
                                  <CheckCircle2 className="h-3.5 w-3.5" />
                                  Verify
                                </Button>
                                <Button
                                  variant="status"
                                  status="red"
                                  className="!px-2.5 !py-1 text-xs"
                                  onClick={() => handleRejectDoc(doc.complianceDocId)}
                                >
                                  <XCircle className="h-3.5 w-3.5" />
                                  Reject
                                </Button>
                              </>
                            ) : isUpdatingThis ? (
                              <span className="inline-flex items-center gap-1.5 text-xs text-slate-500">
                                <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" />
                                Updating...
                              </span>
                            ) : null}
                          </div>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex shrink-0 items-center justify-end border-t border-slate-border bg-slate-50/50 p-6">
          <Button variant="secondary" onClick={onClose}>
            Close
          </Button>
        </div>
      </div>
    </div>
  )
}
