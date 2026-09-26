import { useState } from 'react'
import { AlertCircle, FileCheck, Loader2, UploadCloud, X } from 'lucide-react'
import { toast } from 'sonner'
import Button from '../../../components/Button.jsx'
import Dropzone from '../../../components/Dropzone.jsx'
import Input from '../../../components/Input.jsx'
import { useEscapeKey } from '../../../hooks/useEscapeKey.js'
import { useUploadFileMutation } from '../../loads/api/loadsApi.js'
import { useAddComplianceDocMutation } from '../api/agencyApi.js'

/**
 * Modal dialog for uploading/replacing compliance documents using Dropzone.
 *
 * @param {boolean} isOpen
 * @param {() => void} onClose
 * @param {string} agencyId
 * @param {{ id: string, title: string, description: string }} docTypeInfo
 * @param {object|null} existingDoc
 */
export default function UploadComplianceDocModal({
  isOpen,
  onClose,
  agencyId,
  docTypeInfo,
  existingDoc = null,
}) {
  const [docNumber, setDocNumber] = useState(existingDoc?.docNumber || '')
  const [selectedFile, setSelectedFile] = useState(null)
  const [errorMessage, setErrorMessage] = useState(null)

  const uploadFile = useUploadFileMutation()
  const addDoc = useAddComplianceDocMutation()

  useEscapeKey(isOpen, handleClose)

  if (!isOpen || !docTypeInfo) return null

  function handleClose() {
    setSelectedFile(null)
    setErrorMessage(null)
    onClose()
  }

  const isSubmitting = uploadFile.isPending || addDoc.isPending

  async function handleSubmit(e) {
    e.preventDefault()
    setErrorMessage(null)

    const trimmedDocNumber = docNumber.trim()
    if (!trimmedDocNumber) {
      setErrorMessage('Please enter a valid document or certificate number.')
      return
    }

    if (!selectedFile) {
      setErrorMessage('Please select or drop a document file to upload.')
      return
    }

    try {
      const uploadRes = await uploadFile.mutateAsync(selectedFile)
      const publicId = uploadRes.publicId || uploadRes.data?.publicId

      if (!publicId) {
        throw new Error('File upload completed, but no storage identifier was returned.')
      }

      await addDoc.mutateAsync({
        agencyId,
        doc: {
          publicId,
          docType: docTypeInfo.id,
          docNumber: trimmedDocNumber,
          issuedOn: new Date().toISOString().split('T')[0],
        },
      })

      toast.success(`${docTypeInfo.title} uploaded successfully!`)
      handleClose()
    } catch (err) {
      setErrorMessage(
        err?.response?.data?.message ||
          err?.message ||
          'Failed to upload compliance document. Please check the file and try again.',
      )
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      {/* Backdrop */}
      <div
        onClick={handleClose}
        className="fixed inset-0 bg-primary/40 backdrop-blur-xs transition-opacity"
        aria-hidden="true"
      />

      {/* Modal Dialog Card */}
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="upload-doc-title"
        className="relative z-50 w-full max-w-lg rounded-xl border border-slate-border bg-surface-container-lowest p-6 shadow-2xl"
      >
        {/* Header */}
        <div className="flex items-start justify-between border-b border-slate-border pb-4">
          <div className="flex items-center space-x-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 text-primary">
              <FileCheck className="h-5 w-5" />
            </div>
            <div>
              <h2 id="upload-doc-title" className="text-headline-sm font-bold text-on-surface">
                {existingDoc ? 'Update' : 'Upload'} {docTypeInfo.title}
              </h2>
              <p className="mt-0.5 text-body-xs text-on-surface-variant">
                {docTypeInfo.description}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={handleClose}
            aria-label="Close upload dialog"
            className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Error Banner */}
        {errorMessage && (
          <div className="mt-4 flex items-start gap-2.5 rounded-lg border border-status-red-text/30 bg-status-red-bg p-3 text-body-sm text-status-red-text">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
            <div className="flex-1">{errorMessage}</div>
          </div>
        )}

        {/* Form */}
        <form onSubmit={handleSubmit} className="mt-4 space-y-4">
          <div>
            <label className="mb-1.5 block text-body-xs font-semibold text-slate-700">
              Document / Certificate Number <span className="text-status-red-text">*</span>
            </label>
            <Input
              required
              value={docNumber}
              onChange={(e) => setDocNumber(e.target.value)}
              placeholder="e.g. BR-2024-9901 / POL-88172"
              className="text-body-sm"
              disabled={isSubmitting}
            />
          </div>

          <div>
            <label className="mb-1.5 block text-body-xs font-semibold text-slate-700">
              Document File <span className="text-status-red-text">*</span>
            </label>
            <Dropzone
              file={selectedFile}
              onFileChange={setSelectedFile}
              accept=".pdf,.png,.jpg,.jpeg"
              maxSizeMB={10}
              disabled={isSubmitting}
            />
          </div>

          {/* Action Buttons */}
          <div className="flex items-center justify-end gap-3 border-t border-slate-border pt-4">
            <Button
              type="button"
              variant="secondary"
              onClick={handleClose}
              disabled={isSubmitting}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              disabled={isSubmitting || !selectedFile || !docNumber.trim()}
              className="inline-flex items-center gap-2"
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />
                  <span>Uploading…</span>
                </>
              ) : (
                <>
                  <UploadCloud className="h-4 w-4" />
                  <span>{existingDoc ? 'Save & Replace' : 'Upload Document'}</span>
                </>
              )}
            </Button>
          </div>
        </form>
      </div>
    </div>
  )
}
