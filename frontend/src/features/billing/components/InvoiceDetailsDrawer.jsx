import { useEffect, useState } from 'react'
import {
  Building2,
  CheckCircle2,
  Clock,
  Edit3,
  ExternalLink,
  FileCheck,
  FileText,
  Loader2,
  MapPin,
  Printer,
  Send,
  Ban,
  X,
  ShieldCheck,
} from 'lucide-react'
import { toast } from 'sonner'
import InvoiceStatusBadge from './InvoiceStatusBadge.jsx'
import { formatCurrency, formatInvoiceDate } from '../lib/formatters.js'
import { InvoiceStatus } from '../lib/invoiceStatus.js'
import Dropzone from '../../../components/Dropzone.jsx'
import Button from '../../../components/Button.jsx'
import { useUploadFileMutation } from '../../loads/api/loadsApi.js'

function InvoiceDetailsDrawer({
  invoice,
  isOpen,
  onClose,
  onEdit,
  onIssue,
  onVoid,
  onUploadPaymentProof,
  onConfirmPayment,
  isAgent = true,
  isAdmin = false,
  isShipper = false,
}) {
  const [receiptFile, setReceiptFile] = useState(null)
  const [isSubmittingReceipt, setIsSubmittingReceipt] = useState(false)
  const [isConfirmingPayment, setIsConfirmingPayment] = useState(false)
  const uploadFile = useUploadFileMutation()

  const isDraft = invoice?.status === InvoiceStatus.DRAFT || invoice?.status === 'Draft'
  const isIssued = invoice?.status === InvoiceStatus.ISSUED || invoice?.status === 'Issued'
  const isPaymentPending =
    invoice?.status === InvoiceStatus.PAYMENT_PENDING || invoice?.status === 'PaymentPending'
  const isPaid = invoice?.status === InvoiceStatus.PAID || invoice?.status === 'Paid'
  const isVoided =
    invoice?.status === InvoiceStatus.VOIDED ||
    invoice?.status === InvoiceStatus.VOID ||
    invoice?.status === 'Voided' ||
    invoice?.status === 'Void'
  const isUnpaid = isIssued || isPaymentPending

  // Strict RBAC Capabilities
  const canEdit = isAgent && isDraft
  const canIssue = isAgent && isDraft
  const canVoid =
    isAgent &&
    (isDraft ||
      isIssued ||
      isPaymentPending ||
      invoice?.status === InvoiceStatus.FAILED ||
      invoice?.status === 'Draft' ||
      invoice?.status === 'Issued')
  const canSubmitReceipt = isShipper && isUnpaid && !isVoided
  const canConfirmPayment = isAgent && isPaymentPending && Boolean(invoice?.paymentProofUrl)

  const handleSubmitReceipt = async () => {
    if (!receiptFile || !onUploadPaymentProof) return
    try {
      setIsSubmittingReceipt(true)
      const uploaded = await uploadFile.mutateAsync(receiptFile)
      const publicId = uploaded?.publicId || uploaded?.data?.publicId
      if (!publicId) {
        throw new Error('Upload completed, but no storage identifier was returned.')
      }
      await onUploadPaymentProof(invoice, publicId)
      setReceiptFile(null)
    } catch (err) {
      toast.error('Could not submit payment receipt', {
        description: err?.response?.data?.error?.message || err?.message || 'Please try again.',
      })
    } finally {
      setIsSubmittingReceipt(false)
    }
  }

  const handleConfirmPayment = async () => {
    if (!onConfirmPayment) return
    try {
      setIsConfirmingPayment(true)
      await onConfirmPayment(invoice)
    } finally {
      setIsConfirmingPayment(false)
    }
  }

  // Close drawer on escape key
  useEffect(() => {
    function handleKeyDown(e) {
      if (e.key === 'Escape') onClose()
    }
    if (isOpen) {
      document.addEventListener('keydown', handleKeyDown)
      document.body.style.overflow = 'hidden'
    }
    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      document.body.style.overflow = 'unset'
    }
  }, [isOpen, onClose])

  if (!isOpen || !invoice) return null

  const currency = invoice.currency || 'LKR'
  const recipientName = invoice.recipientName || invoice.shipperName || 'Direct Customer'
  const linkedTrip = invoice.tripId || invoice.linkedEntityId || invoice.loadRef
  const audit = invoice.auditTrail || {}

  return (
    <div className="fixed inset-0 z-50 overflow-hidden">
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-primary/40 backdrop-blur-xs transition-opacity duration-200"
        onClick={onClose}
        aria-hidden="true"
      />

      <div className="fixed inset-y-0 right-0 flex max-w-full pl-10">
        <div className="w-screen max-w-md md:max-w-xl bg-white shadow-2xl flex flex-col h-full border-l border-slate-200">
          {/* Header */}
          <div className="flex items-center justify-between border-b border-slate-200 px-6 py-4 bg-slate-50/70">
            <div className="flex items-center gap-3">
              <span className="font-heading text-xl font-bold tracking-tight text-slate-900">
                {invoice.invoiceNumber}
              </span>
              <InvoiceStatusBadge status={invoice.status} />
            </div>
            <button
              type="button"
              onClick={onClose}
              className="rounded-md p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700 transition-colors"
              aria-label="Close drawer"
            >
              <X className="h-5 w-5" />
            </button>
          </div>

          {/* Drawer Body - Scrollable */}
          <div className="flex-1 overflow-y-auto px-6 py-5 space-y-6">
            {/* Admin Inspector Mode Banner */}
            {isAdmin && (
              <div className="rounded-xl border border-blue-200 bg-blue-50/70 p-3.5 flex items-center gap-2.5 text-xs text-blue-900">
                <ShieldCheck className="h-4 w-4 text-blue-600 shrink-0" />
                <div>
                  <span className="font-semibold uppercase tracking-wider text-[11px] block">
                    Admin Inspector Mode
                  </span>
                  <span>Read-only financial audit mode. Mutation actions and payment settlements are restricted.</span>
                </div>
              </div>
            )}

            {/* Quick Summary Grid */}
            <div className="grid grid-cols-2 gap-4 rounded-xl border border-slate-200 bg-slate-50/60 p-4">
              <div>
                <p className="text-xs uppercase tracking-wider text-slate-500 font-semibold">Linked Trip / Entity</p>
                <div className="mt-1 flex items-center gap-1.5">
                  <span className="rounded bg-white px-2 py-0.5 font-mono text-xs font-semibold text-slate-800 border border-slate-200">
                    {linkedTrip ? (typeof linkedTrip === 'string' && linkedTrip.length > 12 ? linkedTrip.slice(0, 8) : linkedTrip) : 'Standalone'}
                  </span>
                </div>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wider text-slate-500 font-semibold">Date Issued</p>
                <p className="mt-1 font-mono text-sm text-slate-900">
                  {formatInvoiceDate(invoice.issuedAt || invoice.issueDate || invoice.createdAt)}
                </p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wider text-slate-500 font-semibold">Payment Due</p>
                <p className="mt-1 font-mono text-sm text-slate-900">
                  {invoice.dueDate ? formatInvoiceDate(invoice.dueDate) : 'On Presentation'}
                </p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wider text-slate-500 font-semibold">Total Amount</p>
                <p className="mt-1 font-mono text-base font-bold text-slate-900">
                  {formatCurrency(invoice.totalAmount || invoice.amount, currency)}
                </p>
              </div>
            </div>

            {/* Recipient Details */}
            <div>
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-2 flex items-center gap-1.5">
                <Building2 className="h-3.5 w-3.5 text-primary" /> Recipient Details
              </h3>
              <div className="rounded-xl border border-slate-200 p-4 bg-white space-y-1.5">
                <p className="font-semibold text-sm text-slate-900">{recipientName}</p>
                {invoice.recipientRole && (
                  <p className="text-xs text-slate-500 capitalize">Role: {invoice.recipientRole}</p>
                )}
                {invoice.shipperEmail && (
                  <p className="text-xs text-slate-600">{invoice.shipperEmail}</p>
                )}
                {invoice.shipperAddress && (
                  <p className="text-xs text-slate-500 mt-2 flex items-start gap-1">
                    <MapPin className="h-3.5 w-3.5 shrink-0 text-slate-400 mt-0.5" />
                    {invoice.shipperAddress}
                  </p>
                )}
              </div>
            </div>

            {/* Line Items Table */}
            <div>
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-2 flex items-center gap-1.5">
                <FileText className="h-3.5 w-3.5 text-primary" /> Invoice Line Items
              </h3>
              <div className="rounded-xl border border-slate-200 overflow-hidden bg-white">
                <table className="w-full text-left text-xs border-collapse">
                  <thead>
                    <tr className="border-b border-slate-200 bg-slate-50 text-slate-500 uppercase tracking-wider">
                      <th className="py-2.5 px-3 font-semibold">Description</th>
                      <th className="py-2.5 px-2 text-right font-semibold">Qty</th>
                      <th className="py-2.5 px-2 text-right font-semibold">Rate</th>
                      <th className="py-2.5 px-2 text-right font-semibold">Tax</th>
                      <th className="py-2.5 px-3 text-right font-semibold">Amount</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {invoice.lineItems && invoice.lineItems.length > 0 ? (
                      invoice.lineItems.map((item, idx) => (
                        <tr key={idx} className="hover:bg-slate-50/50">
                          <td className="py-2.5 px-3 text-slate-800 font-medium">
                            {item.description}
                          </td>
                          <td className="py-2.5 px-2 text-right font-mono text-slate-600">
                            {item.quantity}
                          </td>
                          <td className="py-2.5 px-2 text-right font-mono text-slate-600">
                            {formatCurrency(item.unitPrice, currency)}
                          </td>
                          <td className="py-2.5 px-2 text-right font-mono text-slate-500">
                            {item.taxRate > 0 ? `${item.taxRate}%` : '0%'}
                          </td>
                          <td className="py-2.5 px-3 text-right font-mono font-semibold text-slate-900">
                            {formatCurrency(item.amount, currency)}
                          </td>
                        </tr>
                      ))
                    ) : (
                      <tr>
                        <td className="py-2.5 px-3 text-slate-800 font-medium">
                          Freight Logistics Service
                        </td>
                        <td className="py-2.5 px-2 text-right font-mono text-slate-600">1</td>
                        <td className="py-2.5 px-2 text-right font-mono text-slate-600">
                          {formatCurrency(invoice.totalAmount || invoice.amount, currency)}
                        </td>
                        <td className="py-2.5 px-2 text-right font-mono text-slate-500">0%</td>
                        <td className="py-2.5 px-3 text-right font-mono font-semibold text-slate-900">
                          {formatCurrency(invoice.totalAmount || invoice.amount, currency)}
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>

                {/* Subtotals & Totals summary in table footer */}
                <div className="p-3 bg-slate-50/80 border-t border-slate-200 space-y-1 text-xs">
                  <div className="flex justify-between text-slate-600">
                    <span>Subtotal</span>
                    <span className="font-mono text-slate-900 font-medium">
                      {formatCurrency(invoice.subtotal || invoice.totalAmount || invoice.amount, currency)}
                    </span>
                  </div>
                  {invoice.taxTotal > 0 && (
                    <div className="flex justify-between text-slate-600">
                      <span>Tax Total</span>
                      <span className="font-mono text-slate-900 font-medium">
                        {formatCurrency(invoice.taxTotal, currency)}
                      </span>
                    </div>
                  )}
                  {invoice.discountTotal > 0 && (
                    <div className="flex justify-between text-emerald-600">
                      <span>Discount Total</span>
                      <span className="font-mono font-medium">
                        -{formatCurrency(invoice.discountTotal, currency)}
                      </span>
                    </div>
                  )}
                  <div className="pt-1.5 border-t border-slate-200 flex justify-between font-bold text-sm text-slate-900">
                    <span>Final Amount</span>
                    <span className="font-mono text-primary font-bold">
                      {formatCurrency(invoice.totalAmount || invoice.amount, currency)}
                    </span>
                  </div>
                </div>
              </div>
            </div>

            {/* Void Notice (If Voided) */}
            {isVoided && (
              <div className="rounded-xl border border-rose-200 bg-rose-50/80 p-4 space-y-1.5">
                <div className="flex items-center gap-2 text-rose-800 font-semibold text-xs uppercase tracking-wide">
                  <Ban className="h-4 w-4 text-rose-600" /> This invoice is Voided
                </div>
                <p className="text-xs text-rose-900">
                  <span className="font-medium">Reason:</span> {invoice.voidReason || audit.voidReason || 'No reason specified'}
                </p>
                {(invoice.voidedAt || audit.voidedAt) && (
                  <p className="text-[11px] text-rose-700 font-mono">
                    Voided on: {formatInvoiceDate(invoice.voidedAt || audit.voidedAt)}
                  </p>
                )}
              </div>
            )}

            {/* Paid Settlement Banner (If Paid) */}
            {isPaid && (
              <div className="rounded-xl border border-emerald-200 bg-emerald-50/80 p-4 space-y-1.5">
                <div className="flex items-center gap-2 text-emerald-800 font-semibold text-xs uppercase tracking-wide">
                  <CheckCircle2 className="h-4 w-4 text-emerald-600" /> Payment Settled & Cleared
                </div>
                <div className="text-xs text-emerald-950 flex flex-col gap-0.5 mt-1 font-mono">
                  {invoice.paymentReference && (
                    <p>
                      <span className="font-sans text-emerald-800 font-medium">Reference: </span>
                      {invoice.paymentReference}
                    </p>
                  )}
                  {(invoice.paidAt || invoice.updatedAt) && (
                    <p>
                      <span className="font-sans text-emerald-800 font-medium">Settled At: </span>
                      {formatInvoiceDate(invoice.paidAt || invoice.updatedAt)}
                    </p>
                  )}
                </div>
              </div>
            )}

            {/* Shipper: Submit Payment Receipt (Issued / previously-submitted invoices) */}
            {canSubmitReceipt && (
              <div className="rounded-xl border-2 border-emerald-300 bg-emerald-50/70 p-4 space-y-3 shadow-xs">
                <div className="flex items-center gap-2">
                  <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-600 text-white">
                    <FileCheck className="h-4 w-4" />
                  </div>
                  <div>
                    <h4 className="font-heading font-bold text-sm text-emerald-950">
                      {invoice.paymentProofUrl ? 'Replace Payment Receipt' : 'Submit Payment Receipt'}
                    </h4>
                    <p className="text-[11px] text-emerald-700">
                      Settle invoice balance ({formatCurrency(invoice.totalAmount || invoice.amount, currency)}) by uploading proof of payment. The Agency will review it and close the invoice.
                    </p>
                  </div>
                </div>

                {invoice.paymentProofUrl && (
                  <a
                    href={invoice.paymentProofUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="flex items-center gap-1.5 rounded-lg border border-emerald-200 bg-white px-3 py-2 text-xs font-medium text-emerald-800 hover:bg-emerald-50"
                  >
                    <ExternalLink className="h-3.5 w-3.5" />
                    View submitted receipt{invoice.paymentProofFileName ? `: ${invoice.paymentProofFileName}` : ''}
                  </a>
                )}

                <Dropzone file={receiptFile} onFileChange={setReceiptFile} disabled={isSubmittingReceipt} />

                <Button
                  variant="status"
                  status="green"
                  onClick={handleSubmitReceipt}
                  disabled={!receiptFile || isSubmittingReceipt}
                  className="w-full py-2 font-bold text-xs bg-emerald-600 hover:bg-emerald-700 text-white shadow-xs disabled:opacity-50"
                >
                  {isSubmittingReceipt ? (
                    <>
                      <Loader2 className="h-4 w-4 animate-spin mr-1.5" />
                      Submitting...
                    </>
                  ) : (
                    <>
                      <FileCheck className="h-4 w-4 mr-1.5" />
                      Submit Receipt
                    </>
                  )}
                </Button>
              </div>
            )}

            {/* Agency: Review Submitted Receipt & Confirm Payment (PaymentPending invoices) */}
            {isAgent && isPaymentPending && (
              <div className="rounded-xl border-2 border-blue-300 bg-blue-50/70 p-4 space-y-3 shadow-xs">
                <div className="flex items-center gap-2">
                  <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-blue-600 text-white">
                    <FileCheck className="h-4 w-4" />
                  </div>
                  <div>
                    <h4 className="font-heading font-bold text-sm text-blue-950">Payment Receipt Awaiting Review</h4>
                    <p className="text-[11px] text-blue-700">
                      {invoice.paymentProofUploadedAt
                        ? `Submitted ${formatInvoiceDate(invoice.paymentProofUploadedAt)}${invoice.paymentProofUploadedByName ? ` by ${invoice.paymentProofUploadedByName}` : ''}.`
                        : 'The Shipper has not submitted a receipt yet.'}
                    </p>
                  </div>
                </div>

                {invoice.paymentProofUrl && (
                  <a
                    href={invoice.paymentProofUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="flex items-center gap-1.5 rounded-lg border border-blue-200 bg-white px-3 py-2 text-xs font-medium text-blue-800 hover:bg-blue-50"
                  >
                    <ExternalLink className="h-3.5 w-3.5" />
                    View receipt{invoice.paymentProofFileName ? `: ${invoice.paymentProofFileName}` : ''}
                  </a>
                )}

                <Button
                  variant="status"
                  status="green"
                  onClick={handleConfirmPayment}
                  disabled={!canConfirmPayment || isConfirmingPayment}
                  className="w-full py-2 font-bold text-xs bg-blue-600 hover:bg-blue-700 text-white shadow-xs disabled:opacity-50"
                >
                  {isConfirmingPayment ? (
                    <>
                      <Loader2 className="h-4 w-4 animate-spin mr-1.5" />
                      Confirming...
                    </>
                  ) : (
                    <>
                      <CheckCircle2 className="h-4 w-4 mr-1.5" />
                      Confirm Payment & Close Invoice
                    </>
                  )}
                </Button>
              </div>
            )}

            {/* Internal Notes */}
            {invoice.notes && (
              <div>
                <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-1.5">Invoice Notes</h3>
                <div className="rounded-xl border border-slate-200 bg-slate-50 p-3 text-xs text-slate-700">
                  {invoice.notes}
                </div>
              </div>
            )}

            {/* Audit Trail Section */}
            <div>
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-2 flex items-center gap-1.5">
                <Clock className="h-3.5 w-3.5 text-primary" /> Audit Trail & History
              </h3>
              <div className="rounded-xl border border-slate-200 p-3 bg-white space-y-2 text-xs text-slate-600">
                <div className="flex justify-between">
                  <span>Created At:</span>
                  <span className="font-mono text-slate-900">
                    {formatInvoiceDate(invoice.createdAt)}
                  </span>
                </div>
                {(audit.createdByName || invoice.createdByName) && (
                  <div className="flex justify-between">
                    <span>Created By (Agent):</span>
                    <span className="text-slate-900 font-medium">
                      {audit.createdByName || invoice.createdByName}
                    </span>
                  </div>
                )}
                {invoice.updatedAt && (
                  <div className="flex justify-between">
                    <span>Last Updated:</span>
                    <span className="font-mono text-slate-900">
                      {formatInvoiceDate(invoice.updatedAt)}
                    </span>
                  </div>
                )}
                {(audit.updatedByName || invoice.updatedByName) && (
                  <div className="flex justify-between">
                    <span>Updated By:</span>
                    <span className="text-slate-900 font-medium">
                      {audit.updatedByName || invoice.updatedByName}
                    </span>
                  </div>
                )}
                {(invoice.paidAt || audit.paidAt) && (
                  <div className="flex justify-between text-emerald-700">
                    <span>Paid At:</span>
                    <span className="font-mono font-medium">
                      {formatInvoiceDate(invoice.paidAt || audit.paidAt)}
                    </span>
                  </div>
                )}
                {invoice.paymentReference && (
                  <div className="flex justify-between text-emerald-700">
                    <span>Payment Ref:</span>
                    <span className="font-mono font-medium">{invoice.paymentReference}</span>
                  </div>
                )}
                {audit.voidedByName && (
                  <div className="flex justify-between text-rose-700">
                    <span>Voided By:</span>
                    <span className="font-medium">{audit.voidedByName}</span>
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* Footer Actions */}
          <div className="border-t border-slate-200 px-6 py-4 bg-slate-50/70 flex items-center justify-between gap-3">
            <button
              type="button"
              onClick={() => window.print()}
              className="inline-flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3.5 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 transition-colors cursor-pointer"
            >
              <Printer className="h-4 w-4" />
              Print
            </button>

            <div className="flex items-center gap-2">
              {canEdit && onEdit && (
                <button
                  type="button"
                  onClick={() => onEdit(invoice)}
                  className="inline-flex items-center gap-1.5 rounded-lg border border-blue-300 bg-blue-50 px-3.5 py-2 text-xs font-medium text-blue-700 hover:bg-blue-100"
                >
                  <Edit3 className="h-3.5 w-3.5" />
                  Edit Draft
                </button>
              )}

              {canIssue && onIssue && (
                <button
                  type="button"
                  onClick={() => onIssue(invoice)}
                  className="inline-flex items-center gap-1.5 rounded-lg bg-emerald-600 px-3.5 py-2 text-xs font-medium text-white hover:bg-emerald-700 shadow-xs"
                >
                  <Send className="h-3.5 w-3.5" />
                  Issue Invoice
                </button>
              )}

              {canVoid && onVoid && (
                <button
                  type="button"
                  onClick={() => onVoid(invoice)}
                  className="inline-flex items-center gap-1.5 rounded-lg border border-rose-300 bg-rose-50 px-3.5 py-2 text-xs font-medium text-rose-700 hover:bg-rose-100"
                >
                  <Ban className="h-3.5 w-3.5" />
                  Void
                </button>
              )}

              <Button variant="secondary" onClick={onClose}>
                Close
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

export default InvoiceDetailsDrawer
