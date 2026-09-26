import { useRef, useState } from 'react'
import { AlertCircle, CheckCircle2, FileText, Image, UploadCloud, X } from 'lucide-react'
import { cx } from '../lib/cx.js'
import Button from './Button.jsx'

/**
 * Reusable drag-and-drop file upload Dropzone component.
 *
 * @param {File|null} file - currently selected file
 * @param {(file: File|null) => void} onFileChange - change handler
 * @param {string} [accept='.pdf,.png,.jpg,.jpeg'] - comma-separated extensions or MIME types
 * @param {number} [maxSizeMB=10] - max file size in MB
 * @param {boolean} [disabled=false]
 * @param {string} [error]
 * @param {string} [className]
 */
function Dropzone({
  file = null,
  onFileChange,
  accept = '.pdf,.png,.jpg,.jpeg',
  maxSizeMB = 10,
  disabled = false,
  error,
  className,
}) {
  const [isDragActive, setIsDragActive] = useState(false)
  const [localError, setLocalError] = useState(null)
  const inputRef = useRef(null)

  function validateAndSelect(selectedFile) {
    if (!selectedFile) return
    setLocalError(null)

    // Check size
    if (selectedFile.size > maxSizeMB * 1024 * 1024) {
      setLocalError(`File exceeds maximum size of ${maxSizeMB}MB.`)
      return
    }

    // Check type extension
    const allowedExtensions = accept
      .split(',')
      .map((ext) => ext.trim().toLowerCase().replace(/^\./, ''))
    const fileExt = selectedFile.name.split('.').pop()?.toLowerCase()
    const mimeMatch = selectedFile.type && accept.includes(selectedFile.type)

    if (allowedExtensions.length > 0 && !allowedExtensions.includes(fileExt) && !mimeMatch) {
      setLocalError(`Unsupported file format. Please upload ${accept}.`)
      return
    }

    onFileChange(selectedFile)
  }

  function handleDragOver(e) {
    e.preventDefault()
    e.stopPropagation()
    if (!disabled) setIsDragActive(true)
  }

  function handleDragLeave(e) {
    e.preventDefault()
    e.stopPropagation()
    setIsDragActive(false)
  }

  function handleDrop(e) {
    e.preventDefault()
    e.stopPropagation()
    setIsDragActive(false)
    if (disabled) return

    const droppedFile = e.dataTransfer.files?.[0]
    if (droppedFile) {
      validateAndSelect(droppedFile)
    }
  }

  function handleRemove(e) {
    e.stopPropagation()
    setLocalError(null)
    onFileChange(null)
    if (inputRef.current) inputRef.current.value = ''
  }

  function formatFileSize(bytes) {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  }

  const activeError = error || localError
  const isImage = file?.type?.startsWith('image/')
  const isPdf = file?.type === 'application/pdf' || file?.name?.endsWith('.pdf')

  return (
    <div className={className}>
      <input
        ref={inputRef}
        type="file"
        accept={accept}
        disabled={disabled}
        onChange={(e) => {
          const selected = e.target.files?.[0]
          if (selected) validateAndSelect(selected)
        }}
        className="hidden"
      />

      {!file ? (
        <div
          onDragOver={handleDragOver}
          onDragEnter={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          onClick={() => !disabled && inputRef.current?.click()}
          role="button"
          tabIndex={0}
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault()
              inputRef.current?.click()
            }
          }}
          className={cx(
            'group relative flex cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed p-6 text-center transition-all',
            isDragActive
              ? 'border-primary bg-primary/5 ring-2 ring-primary/20'
              : 'border-slate-300 bg-slate-50/50 hover:border-primary/60 hover:bg-slate-50',
            disabled && 'cursor-not-allowed opacity-60',
            activeError && 'border-status-red-text bg-status-red-bg/20',
          )}
        >
          <div className="flex h-12 w-12 items-center justify-center rounded-full bg-slate-100 text-primary transition-transform group-hover:scale-105">
            <UploadCloud className="h-6 w-6" />
          </div>
          <p className="mt-3 text-body-md font-medium text-slate-800">
            <span className="text-status-blue-text underline decoration-status-blue-text/40 underline-offset-2">
              Click to browse
            </span>{' '}
            or drag and drop your file here
          </p>
          <p className="mt-1 text-body-sm text-on-surface-variant">
            Supported formats: PDF, JPG, PNG (up to {maxSizeMB}MB)
          </p>
        </div>
      ) : (
        <div className="flex items-center justify-between rounded-lg border border-slate-200 bg-white p-4 shadow-xs">
          <div className="flex items-center space-x-3 truncate">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-secondary-container text-on-secondary-container">
              {isImage ? (
                <Image className="h-5 w-5" />
              ) : isPdf ? (
                <FileText className="h-5 w-5" />
              ) : (
                <FileText className="h-5 w-5" />
              )}
            </div>
            <div className="truncate">
              <p className="truncate text-body-md font-medium text-slate-900">{file.name}</p>
              <div className="flex items-center space-x-2 text-body-xs text-on-surface-variant">
                <span>{formatFileSize(file.size)}</span>
                <span>•</span>
                <span className="flex items-center text-status-green-text">
                  <CheckCircle2 className="mr-1 h-3.5 w-3.5" /> Ready for upload
                </span>
              </div>
            </div>
          </div>

          <div className="flex items-center space-x-2 pl-3">
            <Button
              type="button"
              variant="secondary"
              disabled={disabled}
              onClick={() => inputRef.current?.click()}
              className="text-xs"
            >
              Change
            </Button>
            <button
              type="button"
              disabled={disabled}
              onClick={handleRemove}
              className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600 disabled:cursor-not-allowed"
              aria-label="Remove file"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>
      )}

      {activeError && (
        <div className="mt-2 flex items-center gap-1.5 text-body-sm text-status-red-text">
          <AlertCircle className="h-4 w-4 shrink-0" />
          <span>{activeError}</span>
        </div>
      )}
    </div>
  )
}

export default Dropzone
