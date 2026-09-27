import { useEffect, useMemo, useState } from 'react'
import {
  AlertCircle,
  CheckCircle2,
  Clock,
  Download,
  Plus,
  RefreshCw,
} from 'lucide-react'
import { toast } from 'sonner'
import PageHeader from '../../../components/PageHeader.jsx'
import Card from '../../../components/Card.jsx'
import InvoiceFilterBar from '../components/InvoiceFilterBar.jsx'
import InvoiceListTable from '../components/InvoiceListTable.jsx'
import InvoiceDetailsDrawer from '../components/InvoiceDetailsDrawer.jsx'
import InvoiceEmptyState from '../components/InvoiceEmptyState.jsx'
import InvoiceFormModal from '../components/InvoiceFormModal.jsx'
import VoidInvoiceDialog from '../components/VoidInvoiceDialog.jsx'
import { MOCK_INVOICES } from '../data/mockInvoices.js'
import { InvoiceStatus } from '../lib/invoiceStatus.js'
import { formatCurrency } from '../lib/formatters.js'
import { cx } from '../../../lib/cx.js'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { UserRole } from '../../../lib/enums.js'
import {
  fetchInvoices,
  fetchInvoiceById,
  createInvoice,
  updateInvoice,
  issueInvoice,
  voidInvoice,
  uploadPaymentProof,
  confirmPayment,
} from '../api/invoiceApi.js'

const PAGE_SIZE = 8

function BillingPage() {
  const { role } = useAppSelector((state) => state.auth)
  const isAgent = role === UserRole.AGENCY_STAFF || role === 'Agent' || role === 'AgencyStaff'
  const isAdmin = role === UserRole.ADMIN
  const isShipper = role === UserRole.SHIPPER
  const [invoices, setInvoices] = useState(MOCK_INVOICES)
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const [searchQuery, setSearchQuery] = useState('')
  const [statusFilter, setStatusFilter] = useState('ALL')
  const [dateRange, setDateRange] = useState('30d')
  const [currentPage, setCurrentPage] = useState(1)

  const [selectedInvoice, setSelectedInvoice] = useState(null)
  const [isDrawerOpen, setIsDrawerOpen] = useState(false)

  const [isFormModalOpen, setIsFormModalOpen] = useState(false)
  const [editingInvoice, setEditingInvoice] = useState(null)

  const [isVoidDialogOpen, setIsVoidDialogOpen] = useState(false)
  const [voidingInvoice, setVoidingInvoice] = useState(null)

  const [isExporting, setIsExporting] = useState(false)

  // Fetches invoices from the API (with mock fallback). Used by the manual Refresh button below,
  // via an ordinary event handler, so setting isLoading(true) synchronously up front is fine here
  // (unlike the mount effect, which inlines its own copy of this fetch below rather than calling
  // this function, so its setState calls stay nested in .then()/.catch()/.finally() callbacks â€”
  // see react-hooks/set-state-in-effect).
  const fetchAndSetInvoices = async () => {
    try {
      const response = await fetchInvoices()
      if (response && response.items && response.items.length > 0) {
        setInvoices(response.items)
      } else {
        // Keep initial mock if API returned empty
        setInvoices(MOCK_INVOICES)
      }
    } catch {
      // Backend not yet reachable or offline -> keep mock invoices
      setInvoices(MOCK_INVOICES)
    } finally {
      setIsLoading(false)
    }
  }

  // Manual reload (e.g. the Refresh button) â€” sets the loading flag itself before re-fetching,
  // since it's called from an event handler, not an effect.
  const loadInvoices = () => {
    setIsLoading(true)
    fetchAndSetInvoices()
  }

  useEffect(() => {
    let isMounted = true
    fetchInvoices()
      .then((response) => {
        if (!isMounted) return
        if (response && response.items && response.items.length > 0) {
          setInvoices(response.items)
        } else {
          setInvoices(MOCK_INVOICES)
        }
      })
      .catch(() => {
        if (isMounted) setInvoices(MOCK_INVOICES)
      })
      .finally(() => {
        if (isMounted) setIsLoading(false)
      })
    return () => {
      isMounted = false
    }
  }, [])

  // Filter invoices based on search, status, and date range
  const filteredInvoices = useMemo(() => {
    return invoices.filter((inv) => {
      // 0. Shipper restriction: Never expose Draft invoices to Shippers
      if (isShipper && (inv.status === InvoiceStatus.DRAFT || inv.status === 'Draft')) {
        return false
      }

      // 1. Status Filter
      if (statusFilter !== 'ALL') {
        const invStatus = String(inv.status || '').toLowerCase()
        const targetStatus = String(statusFilter).toLowerCase()
        if (invStatus !== targetStatus) return false
      }

      // 2. Search Query (matches Invoice ID, Recipient/Shipper Name, Linked Entity/Load Ref)
      if (searchQuery.trim()) {
        const query = searchQuery.toLowerCase().trim()
        const matchesId = (inv.invoiceNumber || '').toLowerCase().includes(query)
        const recipientName = inv.recipientName || inv.shipperName || ''
        const matchesRecipient = recipientName.toLowerCase().includes(query)
        const linked = inv.tripId || inv.linkedEntityId || inv.loadRef || ''
        const matchesLinked = String(linked).toLowerCase().includes(query)
        if (!matchesId && !matchesRecipient && !matchesLinked) {
          return false
        }
      }

      // 3. Date Range Filter
      const dateVal = inv.issuedAt || inv.issueDate || inv.createdAt
      if (dateRange !== 'all' && dateVal) {
        const invDate = new Date(dateVal)
        const now = new Date()
        const diffDays = Math.floor((now - invDate) / (1000 * 60 * 60 * 24))
        if (dateRange === '7d' && diffDays > 7) return false
        if (dateRange === '30d' && diffDays > 30) return false
        if (dateRange === '90d' && diffDays > 90) return false
      }

      return true
    })
  }, [invoices, searchQuery, statusFilter, dateRange, isShipper])

  // Pagination calculation
  const totalEntries = filteredInvoices.length
  const totalPages = Math.max(1, Math.ceil(totalEntries / PAGE_SIZE))
  const paginatedInvoices = useMemo(() => {
    const start = (currentPage - 1) * PAGE_SIZE
    return filteredInvoices.slice(start, start + PAGE_SIZE)
  }, [filteredInvoices, currentPage])

  // Filter handlers
  const handleSearchChange = (val) => {
    setSearchQuery(val)
    setCurrentPage(1)
  }

  const handleStatusChange = (val) => {
    setStatusFilter(val)
    setCurrentPage(1)
  }

  const handleDateRangeChange = (val) => {
    setDateRange(val)
    setCurrentPage(1)
  }

  const handleResetFilters = () => {
    setSearchQuery('')
    setStatusFilter('ALL')
    setDateRange('30d')
    setCurrentPage(1)
  }

  const hasActiveFilters = searchQuery !== '' || statusFilter !== 'ALL' || dateRange !== '30d'

  // Summary Metrics
  const metrics = useMemo(() => {
    const totalAmount = invoices.reduce((sum, inv) => sum + Number(inv.totalAmount || inv.amount || 0), 0)
    const paidAmount = invoices
      .filter((inv) => inv.status === InvoiceStatus.PAID || inv.status === 'Paid')
      .reduce((sum, inv) => sum + Number(inv.totalAmount || inv.amount || 0), 0)
    const pendingAmount = invoices
      .filter(
        (inv) =>
          inv.status === InvoiceStatus.PAYMENT_PENDING ||
          inv.status === InvoiceStatus.ISSUED ||
          inv.status === 'Issued' ||
          inv.status === 'Payment Pending',
      )
      .reduce((sum, inv) => sum + Number(inv.totalAmount || inv.amount || 0), 0)
    const overdueCount = invoices.filter(
      (inv) =>
        inv.status === InvoiceStatus.OVERDUE ||
        inv.status === InvoiceStatus.FAILED ||
        inv.status === 'Failed' ||
        inv.status === 'Overdue',
    ).length

    return { totalAmount, paidAmount, pendingAmount, overdueCount }
  }, [invoices])

  const handleSelectInvoice = async (invoice) => {
    try {
      // Fetch fresh details with full line items if ID available
      const id = invoice.id || invoice.invoiceId
      const fullDetails = await fetchInvoiceById(id)
      setSelectedInvoice(fullDetails || invoice)
    } catch {
      setSelectedInvoice(invoice)
    }
    setIsDrawerOpen(true)
  }

  // Open Create Modal
  const handleOpenCreateModal = () => {
    setEditingInvoice(null)
    setIsFormModalOpen(true)
  }

  // Open Edit Modal
  const handleOpenEditModal = (invoice) => {
    setEditingInvoice(invoice)
    setIsFormModalOpen(true)
    if (isDrawerOpen) setIsDrawerOpen(false)
  }

  // Handle Form Submission (Create or Edit)
  const handleFormSubmit = async (payload, invoiceId) => {
    setIsSubmitting(true)
    try {
      if (invoiceId) {
        // Edit existing draft invoice
        const updated = await updateInvoice(invoiceId, payload)
        toast.success(`Invoice ${updated.invoiceNumber || 'Draft'} updated successfully.`)
        setInvoices((prev) =>
          prev.map((i) => ((i.id || i.invoiceId) === invoiceId ? { ...i, ...updated } : i)),
        )
      } else {
        // Create new invoice
        const created = await createInvoice(payload)
        const isIssued = payload.issueImmediately
        toast.success(
          isIssued
            ? `Invoice ${created.invoiceNumber} created and issued successfully.`
            : `Draft invoice ${created.invoiceNumber} saved.`,
        )
        setInvoices((prev) => [created, ...prev])
      }
      setIsFormModalOpen(false)
      setEditingInvoice(null)
    } catch (err) {
      toast.error('Failed to save invoice', {
        description: err.response?.data?.error?.message || err.message || 'An unexpected error occurred.',
      })
    } finally {
      setIsSubmitting(false)
    }
  }

  // Issue Draft Invoice
  const handleIssueInvoice = async (invoice) => {
    const id = invoice.id || invoice.invoiceId
    try {
      setIsSubmitting(true)
      const issued = await issueInvoice(id)
      toast.success(`Invoice ${issued.invoiceNumber || invoice.invoiceNumber} has been issued!`, {
        description: 'Totals are locked and ready for payment presentation.',
      })
      setInvoices((prev) =>
        prev.map((i) => ((i.id || i.invoiceId) === id ? { ...i, ...issued, status: 'Issued' } : i)),
      )
      if (selectedInvoice && (selectedInvoice.id || selectedInvoice.invoiceId) === id) {
        setSelectedInvoice((prev) => ({ ...prev, ...issued, status: 'Issued' }))
      }
    } catch (err) {
      toast.error('Could not issue invoice', {
        description: err.response?.data?.error?.message || err.message,
      })
    } finally {
      setIsSubmitting(false)
    }
  }

  // Open Void Dialog
  const handleOpenVoidDialog = (invoice) => {
    setVoidingInvoice(invoice)
    setIsVoidDialogOpen(true)
  }

  // Confirm Void
  const handleConfirmVoid = async (id, reason) => {
    try {
      setIsSubmitting(true)
      const voided = await voidInvoice(id, reason)
      toast.success(`Invoice ${voided.invoiceNumber || 'record'} has been voided.`)
      setInvoices((prev) =>
        prev.map((i) =>
          (i.id || i.invoiceId) === id
            ? { ...i, ...voided, status: 'Voided', voidReason: reason }
            : i,
        ),
      )
      if (selectedInvoice && (selectedInvoice.id || selectedInvoice.invoiceId) === id) {
        setSelectedInvoice((prev) => ({ ...prev, ...voided, status: 'Voided', voidReason: reason }))
      }
      setIsVoidDialogOpen(false)
      setVoidingInvoice(null)
    } catch (err) {
      toast.error('Could not void invoice', {
        description: err.response?.data?.error?.message || err.message,
      })
    } finally {
      setIsSubmitting(false)
    }
  }

  // Submit Payment Receipt (Strictly Shipper capability)
  const handleUploadPaymentProof = async (invoice, publicId) => {
    const id = invoice.id || invoice.invoiceId
    try {
      const updated = await uploadPaymentProof(id, publicId)
      toast.success(`Payment receipt submitted for Invoice ${invoice.invoiceNumber}!`, {
        description: 'The Agency will review it and close the invoice once confirmed.',
      })
      setInvoices((prev) =>
        prev.map((i) => ((i.id || i.invoiceId) === id ? { ...i, ...updated } : i)),
      )
      if (selectedInvoice && (selectedInvoice.id || selectedInvoice.invoiceId) === id) {
        setSelectedInvoice((prev) => ({ ...prev, ...updated }))
      }
      return updated
    } catch (err) {
      toast.error('Could not submit payment receipt', {
        description: err.response?.data?.error?.message || err.message,
      })
      throw err
    }
  }

  // Confirm Payment & Close Invoice (Strictly Agency capability)
  const handleConfirmPayment = async (invoice) => {
    const id = invoice.id || invoice.invoiceId
    try {
      const updated = await confirmPayment(id)
      toast.success(`Invoice ${invoice.invoiceNumber} settled successfully!`)
      setInvoices((prev) =>
        prev.map((i) => ((i.id || i.invoiceId) === id ? { ...i, ...updated } : i)),
      )
      if (selectedInvoice && (selectedInvoice.id || selectedInvoice.invoiceId) === id) {
        setSelectedInvoice((prev) => ({ ...prev, ...updated }))
      }
      return updated
    } catch (err) {
      toast.error('Could not confirm payment', {
        description: err.response?.data?.error?.message || err.message,
      })
      throw err
    }
  }

  // CSV Export
  const handleExportCSV = () => {
    setIsExporting(true)
    try {
      const headers = [
        'Invoice ID',
        'Recipient',
        'Linked Entity',
        'Amount (LKR)',
        'Status',
        'Issued Date',
        'Due Date',
      ]
      const rows = filteredInvoices.map((inv) => [
        `"${inv.invoiceNumber}"`,
        `"${inv.recipientName || inv.shipperName || 'Direct'}"`,
        `"${inv.tripId || inv.linkedEntityId || inv.loadRef || 'Standalone'}"`,
        inv.totalAmount || inv.amount,
        `"${inv.status}"`,
        `"${inv.issuedAt || inv.issueDate || ''}"`,
        `"${inv.dueDate || ''}"`,
      ])

      const csvContent =
        'data:text/csv;charset=utf-8,' + [headers.join(','), ...rows.map((e) => e.join(','))].join('\n')
      const encodedUri = encodeURI(csvContent)
      const link = document.createElement('a')
      link.setAttribute('href', encodedUri)
      link.setAttribute('download', `freightlink_invoices_${new Date().toISOString().split('T')[0]}.csv`)
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
    } finally {
      setTimeout(() => setIsExporting(false), 500)
    }
  }

  const startEntry = totalEntries === 0 ? 0 : (currentPage - 1) * PAGE_SIZE + 1
  const endEntry = Math.min(currentPage * PAGE_SIZE, totalEntries)

  return (
    <div className="space-y-6 max-w-7xl mx-auto">
      {/* Page Header */}
      <PageHeader
        eyebrow="Financial Operations"
        title="Invoice Ledger"
        description={
          isAgent
            ? 'Manual invoice drafting, granular line-item billing, and payment tracking.'
            : isAdmin
            ? 'System-wide financial ledger audit and read-only invoice inspection.'
            : 'Review your issued freight invoices and complete secure online settlements.'
        }
        actions={
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={loadInvoices}
              disabled={isLoading}
              title="Refresh ledger"
              className="inline-flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 transition-colors"
            >
              <RefreshCw className={cx('h-3.5 w-3.5', isLoading && 'animate-spin')} />
              <span>Refresh</span>
            </button>

            <button
              type="button"
              onClick={handleExportCSV}
              disabled={isExporting || totalEntries === 0}
              className="inline-flex items-center gap-2 rounded-lg border border-slate-300 bg-white px-3.5 py-2 text-xs font-medium text-slate-700 shadow-xs hover:bg-slate-50 disabled:opacity-50 transition-colors cursor-pointer"
            >
              <Download className="h-4 w-4 text-slate-500" />
              <span>{isExporting ? 'Exporting…' : 'Export CSV'}</span>
            </button>

            {/* Create Invoice: Strictly for Agent role */}
            {isAgent && (
              <button
                type="button"
                onClick={handleOpenCreateModal}
                className="inline-flex items-center gap-2 rounded-lg bg-primary px-3.5 py-2 text-xs font-semibold text-on-primary shadow-xs hover:bg-primary/90 transition-colors cursor-pointer"
              >
                <Plus className="h-4 w-4" />
                <span>Create Invoice</span>
              </button>
            )}
          </div>
        }
      />

      {/* Summary KPI Strip */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
          <p className="text-xs font-bold uppercase tracking-wider text-slate-500">Total Invoiced</p>
          <p className="mt-1 font-mono text-lg md:text-xl font-bold text-slate-900">
            LKR {formatCurrency(metrics.totalAmount)}
          </p>
          <p className="mt-1 text-xs text-slate-400">{invoices.length} invoices on record</p>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
          <div className="flex items-center justify-between">
            <p className="text-xs font-bold uppercase tracking-wider text-emerald-700">Settled (Paid)</p>
            <CheckCircle2 className="h-3.5 w-3.5 text-emerald-600" />
          </div>
          <p className="mt-1 font-mono text-lg md:text-xl font-bold text-emerald-700">
            LKR {formatCurrency(metrics.paidAmount)}
          </p>
          <p className="mt-1 text-xs text-slate-400">
            {invoices.filter((i) => i.status === InvoiceStatus.PAID || i.status === 'Paid').length} invoices cleared
          </p>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
          <div className="flex items-center justify-between">
            <p className="text-xs font-bold uppercase tracking-wider text-amber-700">Outstanding</p>
            <Clock className="h-3.5 w-3.5 text-amber-600" />
          </div>
          <p className="mt-1 font-mono text-lg md:text-xl font-bold text-amber-800">
            LKR {formatCurrency(metrics.pendingAmount)}
          </p>
          <p className="mt-1 text-xs text-slate-400">
            {
              invoices.filter(
                (i) =>
                  i.status === InvoiceStatus.PAYMENT_PENDING ||
                  i.status === InvoiceStatus.ISSUED ||
                  i.status === 'Issued' ||
                  i.status === 'Payment Pending',
              ).length
            }{' '}
            awaiting payment
          </p>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
          <div className="flex items-center justify-between">
            <p className="text-xs font-bold uppercase tracking-wider text-rose-700">Attention</p>
            <AlertCircle className="h-3.5 w-3.5 text-rose-600" />
          </div>
          <p className="mt-1 font-mono text-lg md:text-xl font-bold text-rose-700">
            {metrics.overdueCount} Invoices
          </p>
          <p className="mt-1 text-xs text-slate-400">Overdue or voided records</p>
        </div>
      </div>

      {/* Filter Bar */}
      <InvoiceFilterBar
        searchQuery={searchQuery}
        onSearchChange={handleSearchChange}
        statusFilter={statusFilter}
        onStatusChange={handleStatusChange}
        dateRange={dateRange}
        onDateRangeChange={handleDateRangeChange}
        onResetFilters={handleResetFilters}
        hasActiveFilters={hasActiveFilters}
        totalCount={invoices.length}
        filteredCount={totalEntries}
        isShipper={isShipper}
      />

      {/* Main Ledger Card */}
      <Card className="p-0 overflow-hidden min-h-[420px] flex flex-col justify-between shadow-xs border-slate-200 bg-white">
        {totalEntries > 0 ? (
          <div>
            <InvoiceListTable
              invoices={paginatedInvoices}
              onSelectInvoice={handleSelectInvoice}
              onEditInvoice={handleOpenEditModal}
              onIssueInvoice={handleIssueInvoice}
              onVoidInvoice={handleOpenVoidDialog}
              isAgent={isAgent}
            />
          </div>
        ) : (
          <InvoiceEmptyState onClearFilters={handleResetFilters} isFiltered={hasActiveFilters} />
        )}

        {/* Table Footer with Pagination */}
        <div className="border-t border-slate-200 bg-slate-50/50 px-4 py-3 flex flex-wrap items-center justify-between gap-3 text-xs text-slate-600">
          <div>
            Showing <span className="font-semibold text-slate-900">{startEntry}</span> to{' '}
            <span className="font-semibold text-slate-900">{endEntry}</span> of{' '}
            <span className="font-semibold text-slate-900">{totalEntries}</span> entries
          </div>

          {totalPages > 1 && (
            <div className="flex items-center gap-1.5">
              <button
                type="button"
                onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                disabled={currentPage <= 1}
                aria-label="Previous page"
                className="flex h-7 w-7 items-center justify-center rounded border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 disabled:opacity-40 disabled:pointer-events-none transition-colors cursor-pointer"
              >
                ‹
              </button>

              {Array.from({ length: totalPages }, (_, i) => i + 1).map((pageNum) => (
                <button
                  key={pageNum}
                  type="button"
                  onClick={() => setCurrentPage(pageNum)}
                  className={cx(
                    'flex h-7 min-w-7 items-center justify-center rounded px-2 text-xs font-medium transition-colors cursor-pointer',
                    currentPage === pageNum
                      ? 'bg-primary text-on-primary font-bold'
                      : 'border border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                  )}
                >
                  {pageNum}
                </button>
              ))}

              <button
                type="button"
                onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                disabled={currentPage >= totalPages}
                aria-label="Next page"
                className="flex h-7 w-7 items-center justify-center rounded border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 disabled:opacity-40 disabled:pointer-events-none transition-colors cursor-pointer"
              >
                ›
              </button>
            </div>
          )}
        </div>
      </Card>

      {/* Invoice Details Slide-over Drawer */}
      <InvoiceDetailsDrawer
        invoice={selectedInvoice}
        isOpen={isDrawerOpen}
        onClose={() => setIsDrawerOpen(false)}
        onEdit={handleOpenEditModal}
        onIssue={handleIssueInvoice}
        onVoid={handleOpenVoidDialog}
        onUploadPaymentProof={handleUploadPaymentProof}
        onConfirmPayment={handleConfirmPayment}
        role={role}
        isAgent={isAgent}
        isAdmin={isAdmin}
        isShipper={isShipper}
      />

      {/* Manual Invoice Form Modal (Create / Edit: Agent only) */}
      {isAgent && (
        <InvoiceFormModal
          isOpen={isFormModalOpen}
          onClose={() => {
            setIsFormModalOpen(false)
            setEditingInvoice(null)
          }}
          onSubmit={handleFormSubmit}
          initialInvoice={editingInvoice}
          isSubmitting={isSubmitting}
        />
      )}

      {/* Void Invoice Confirmation Modal: Agent only */}
      {isAgent && (
        <VoidInvoiceDialog
          isOpen={isVoidDialogOpen}
          onClose={() => {
            setIsVoidDialogOpen(false)
            setVoidingInvoice(null)
          }}
          onConfirm={handleConfirmVoid}
          invoice={voidingInvoice}
          isSubmitting={isSubmitting}
        />
      )}
    </div>
  )
}

export default BillingPage
