import { useState } from 'react'
import { toast } from 'sonner'
import { FileText, Trash2, X } from 'lucide-react'
import Card from '../../../components/Card.jsx'
import { useDetachLoadFileMutation, useLoadFilesQuery } from '../api/loadsApi.js'
import { getLoadErrorMessage } from '../lib/errorMessages.js'
import { formatFileSize } from '../lib/format.js'
import { canManageLoadFiles } from '../lib/loadPermissions.js'
import { useEscapeKey } from '../../../hooks/useEscapeKey.js'

// Attached-documents list (docs/load-management-api.md Section 4.2/4.3) —
// upload/attach is not offered from the detail page; files here are only
// viewed (via the embedded FilePreviewPanel, not a new browser tab/window)
// or detached, and detaching (like every Load file mutation) is Shipper
// (owner) only — an Admin viewing someone else's load can preview files
// but never sees the detach control (canManageLoadFiles — loadPermissions.js).
// GET /loads/{loadId}/files and DELETE /loads/{loadId}/files/{fileId} only —
// the two-step upload+attach mutations still live in loadsApi.js for reuse
// elsewhere, just unused here.
function LoadFilesSection({ loadId, role }) {
  const [previewFile, setPreviewFile] = useState(null)
  const filesQuery = useLoadFilesQuery(loadId)
  const detachMutation = useDetachLoadFileMutation(loadId)
  const canManage = canManageLoadFiles(role)

  async function handleDetach(fileId) {
    try {
      await detachMutation.mutateAsync(fileId)
      toast.success('File detached')
      setPreviewFile((current) => (current?.fileId === fileId ? null : current))
    } catch (error) {
      toast.error(getLoadErrorMessage(error))
    }
  }

  return (
    <Card>
      <h3 className="mb-4 text-headline-md text-primary">Attached Documents</h3>

      {filesQuery.isLoading ? (
        <p className="text-body-md text-on-surface-variant">Loading files…</p>
      ) : filesQuery.isError ? (
        <p className="text-body-md text-status-red-text">{getLoadErrorMessage(filesQuery.error)}</p>
      ) : filesQuery.data.length > 0 ? (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4">
          {filesQuery.data.map((file) => (
            <FileCard
              key={file.fileId}
              file={file}
              onOpen={() => setPreviewFile(file)}
              onDetach={canManage ? () => handleDetach(file.fileId) : undefined}
              isDetaching={detachMutation.isPending}
            />
          ))}
        </div>
      ) : (
        <p className="text-body-md text-on-surface-variant">No documents attached yet.</p>
      )}

      {previewFile && <FilePreviewPanel file={previewFile} onClose={() => setPreviewFile(null)} />}
    </Card>
  )
}

// Small thumbnail card for one attachment — images render as a cover
// thumbnail, other file types show a generic document icon. The whole card
// opens FilePreviewPanel; detach is a hover-revealed corner button so it
// doesn't compete with the click-to-preview target.
function FileCard({ file, onOpen, onDetach, isDetaching }) {
  const isImage = file.contentType?.startsWith('image/')

  return (
    <div className="group relative overflow-hidden rounded-md border border-slate-border bg-white">
      <button type="button" onClick={onOpen} className="flex w-full flex-col text-left">
        <div className="flex h-24 w-full items-center justify-center overflow-hidden bg-surface-container-low">
          {isImage ? (
            <img
              src={file.secureUrl}
              alt={file.originalFileName ?? 'Attached document'}
              className="h-full w-full object-cover"
            />
          ) : (
            <FileText className="h-8 w-8 text-on-surface-variant" strokeWidth={1.5} />
          )}
        </div>
        <div className="px-2 py-2">
          <p className="truncate text-body-md text-on-surface" title={file.originalFileName ?? file.publicId}>
            {file.originalFileName ?? file.publicId}
          </p>
          <p className="text-label-caps text-on-surface-variant">{file.fileType}</p>
        </div>
      </button>
      {onDetach && (
        <button
          type="button"
          onClick={onDetach}
          disabled={isDetaching}
          aria-label={`Detach ${file.originalFileName ?? file.publicId}`}
          className="absolute right-1.5 top-1.5 flex h-7 w-7 items-center justify-center rounded-full bg-surface-container-lowest text-on-surface-variant opacity-0 shadow-soft transition-opacity hover:text-status-red-text focus-visible:opacity-100 group-hover:opacity-100"
        >
          <Trash2 className="h-3.5 w-3.5" strokeWidth={1.5} />
        </button>
      )}
    </div>
  )
}

// Embedded slide-in panel anchored to the right edge of the viewport —
// replaces the previous target="_blank" new-tab open. Images render
// inline; other file types (manifests, invoices) fall back to an in-panel
// "open in a new tab" link, since no in-app document viewer is installed.
// Overlay/backdrop pattern mirrors DashboardLayout's mobile nav drawer.
function FilePreviewPanel({ file, onClose }) {
  useEscapeKey(true, onClose)
  const isImage = file.contentType?.startsWith('image/')

  return (
    <>
      <button type="button" aria-label="Close preview" onClick={onClose} className="fixed inset-0 z-40 bg-primary/40" />
      <aside className="fixed inset-y-0 right-0 z-50 flex w-full max-w-md flex-col border-l border-slate-border bg-surface-container-lowest shadow-soft">
        <div className="flex items-center justify-between gap-2 border-b border-slate-border px-4 py-3">
          <h4 className="truncate text-headline-md text-primary">{file.originalFileName ?? file.publicId}</h4>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close preview"
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded text-on-surface-variant hover:bg-slate-100 hover:text-on-surface"
          >
            <X className="h-5 w-5" strokeWidth={1.5} />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-4">
          {isImage ? (
            <img
              src={file.secureUrl}
              alt={file.originalFileName ?? 'Attached document preview'}
              className="w-full rounded-md border border-slate-border"
            />
          ) : (
            <div className="flex flex-col items-center gap-3 py-16 text-center">
              <FileText className="h-10 w-10 text-on-surface-variant" strokeWidth={1.5} />
              <p className="text-body-md text-on-surface-variant">Preview isn't available for this file type.</p>
              <a href={file.secureUrl} target="_blank" rel="noreferrer" className="text-body-md text-primary hover:underline">
                Open in a new tab
              </a>
            </div>
          )}
        </div>

        <div className="flex items-center justify-between border-t border-slate-border px-4 py-3 text-body-md text-on-surface-variant">
          <span>{file.fileType}</span>
          <span>{formatFileSize(file.bytes)}</span>
        </div>
      </aside>
    </>
  )
}

export default LoadFilesSection
