import { useMemo, useState } from 'react'
import {
  AlertCircle,
  CheckCircle2,
  Clock,
  Download,
  Plus,
  Wallet,
} from 'lucide-react'
import PageHeader from '../../../components/PageHeader.jsx'
import Card from '../../../components/Card.jsx'
import InvoiceFilterBar from '../components/InvoiceFilterBar.jsx'
import InvoiceListTable from '../components/InvoiceListTable.jsx'
import InvoiceDetailsDrawer from '../components/InvoiceDetailsDrawer.jsx'
import InvoiceEmptyState from '../components/InvoiceEmptyState.jsx'
import { MOCK_INVOICES } from '../data/mockInvoices.js'
import { InvoiceStatus } from '../lib/invoiceStatus.js'
import { formatCurrency } from '../lib/formatters.js'
import { cx } from '../../../lib/cx.js'

const PAGE_SIZE = 8

function BillingPage() {
  const [invoices] = useState(MOCK_INVOICES)
  const [searchQuery, setSearchQuery] = useState('')
  const [statusFilter, setStatusFilter] = useState('ALL')
  const [dateRange, setDateRange] = useState('30d')
  const [currentPage, setCurrentPage] = useState(1)
  const [selectedInvoice, setSelectedInvoice] = useState(null)
  const [isDrawerOpen, setIsDrawerOpen] = useState(false)
  const [isExporting, setIsExporting] = useState(false)
  const [createNoticeOpen, setCreateNoticeOpen] = useState(false)

  // Filter invoices based on search, status, and date range
  const filteredInvoices = useMemo(() => {
    return invoices.filter((inv) => {
      // 1. Status Filter
      if (statusFilter !== 'ALL' && inv.status !== statusFilter) {
        return false
      }

      // 2. Search Query (matches Invoice ID, Shipper Name, Load Ref, Gateway TXN)
      if (searchQuery.trim()) {
        const query = searchQuery.toLowerCase().trim()
        const matchesId = inv.invoiceNumber.toLowerCase().includes(query)
        const matchesShipper = inv.shipperName.toLowerCase().includes(query)
        const matchesLoad = inv.loadRef.toLowerCase().includes(query)
        const matchesTxn = inv.gatewayTxn ? inv.gatewayTxn.toLowerCase().includes(query) : false
        if (!matchesId && !matchesShipper && !matchesLoad && !matchesTxn) {
          return false
        }
      }

      // 3. Date Range Filter
      if (dateRange !== 'all' && inv.issueDate) {
        const invDate = new Date(inv.issueDate)
        const now = new Date()
        const diffDays = Math.floor((now - invDate) / (1000 * 60 * 60 * 24))
        if (dateRange === '7d' && diffDays > 7) return false
        if (dateRange === '30d' && diffDays > 30) return false
        if (dateRange === '90d' && diffDays > 90) return false
      }

      return true
    })
  }, [invoices, searchQuery, statusFilter, dateRange])

  // Pagination calculation
  const totalEntries = filteredInvoices.length
  const totalPages = Math.max(1, Math.ceil(totalEntries / PAGE_SIZE))
  const paginatedInvoices = useMemo(() => {
    const start = (currentPage - 1) * PAGE_SIZE
    return filteredInvoices.slice(start, start + PAGE_SIZE)
  }, [filteredInvoices, currentPage])

  // Reset page when filters change
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
    const totalAmount = invoices.reduce((sum, inv) => sum + inv.amount, 0)
    const paidAmount = invoices.filter((inv) => inv.status === InvoiceStatus.PAID).reduce((sum, inv) => sum + inv.amount, 0)
    const pendingAmount = invoices
      .filter((inv) => inv.status === InvoiceStatus.PAYMENT_PENDING || inv.status === InvoiceStatus.ISSUED)
      .reduce((sum, inv) => sum + inv.amount, 0)
    const overdueCount = invoices.filter((inv) => inv.status === InvoiceStatus.OVERDUE || inv.status === InvoiceStatus.FAILED).length

    return { totalAmount, paidAmount, pendingAmount, overdueCount }
  }, [invoices])

  const handleSelectInvoice = (invoice) => {
    setSelectedInvoice(invoice)
    setIsDrawerOpen(true)
  }

  // Functional CSV Export
  const handleExportCSV = () => {
    setIsExporting(true)
    try {
      const headers = ['Invoice ID', 'Shipper Entity', 'Load Ref', 'Amount (LKR)', 'Gateway TXN', 'Status', 'Issued Date', 'Due Date']
      const rows = filteredInvoices.map((inv) => [
        `"${inv.invoiceNumber}"`,
        `"${inv.shipperName}"`,
        `"${inv.loadRef}"`,
        inv.amount,
        `"${inv.gatewayTxn || '--'}"`,
        `"${inv.status}"`,
        `"${inv.issueDate}"`,
        `"${inv.dueDate}"`,
      ])

      const csvContent = 'data:text/csv;charset=utf-8,' + [headers.join(','), ...rows.map((e) => e.join(','))].join('\n')
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
      {/* Page Header matching Image 4 */}
      <PageHeader
        eyebrow="Financial Operations"
        title="Invoice Ledger"
        description="Manage and track all issued freight invoices and gateway transactions."
        actions={
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={handleExportCSV}
              disabled={isExporting || totalEntries === 0}
              className="inline-flex items-center gap-2 rounded-md border border-slate-300 bg-white px-3.5 py-2 text-body-md font-medium text-slate-700 shadow-xs hover:bg-slate-50 disabled:opacity-50 transition-colors cursor-pointer"
            >
              <Download className="h-4 w-4 text-slate-500" />
              <span>{isExporting ? 'Exporting…' : 'Export CSV'}</span>
            </button>

            <button
              type="button"
              onClick={() => setCreateNoticeOpen(true)}
              className="inline-flex items-center gap-2 rounded-md bg-primary px-3.5 py-2 text-body-md font-medium text-on-primary shadow-xs hover:bg-primary-container transition-colors cursor-pointer"
            >
              <Plus className="h-4 w-4" />
              <span>Create Invoice</span>
            </button>
          </div>
        }
      />

      {/* Sprint 5 Notice Modal / Banner when clicking Create Invoice */}
      {createNoticeOpen && (
        <div className="rounded-lg border border-blue-200 bg-blue-50/70 p-4 text-body-md text-blue-900 flex items-start justify-between gap-3">
          <div className="flex items-start gap-2.5">
            <Wallet className="h-5 w-5 text-blue-600 mt-0.5 shrink-0" />
            <div>
              <p className="font-semibold text-blue-950">Automated Invoice Creation</p>
              <p className="mt-0.5 text-xs text-blue-800">
                Invoices are automatically issued upon Trip completion and POD approval. Direct manual invoice creation and full PayHere gateway callback hooks are arriving in Sprint 5.
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={() => setCreateNoticeOpen(false)}
            className="text-blue-500 hover:text-blue-800 text-xs font-semibold px-2 py-1 rounded"
          >
            Dismiss
          </button>
        </div>
      )}

      {/* Summary KPI Strip */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div className="rounded-lg border border-slate-border bg-surface-container-lowest p-4 shadow-soft">
          <p className="text-xs font-bold uppercase tracking-wider text-slate-500">Total Billed</p>
          <p className="mt-1 font-mono text-lg md:text-xl font-bold text-on-surface">
            LKR {formatCurrency(metrics.totalAmount)}
          </p>
          <p className="mt-1 text-xs text-slate-400">{invoices.length} total invoices issued</p>
        </div>

        <div className="rounded-lg border border-slate-border bg-surface-container-lowest p-4 shadow-soft">
          <div className="flex items-center justify-between">
            <p className="text-xs font-bold uppercase tracking-wider text-emerald-700">Settled (Paid)</p>
            <CheckCircle2 className="h-3.5 w-3.5 text-emerald-600" />
          </div>
          <p className="mt-1 font-mono text-lg md:text-xl font-bold text-emerald-700">
            LKR {formatCurrency(metrics.paidAmount)}
          </p>
          <p className="mt-1 text-xs text-slate-400">
            {invoices.filter((i) => i.status === InvoiceStatus.PAID).length} invoices cleared
          </p>
        </div>

        <div className="rounded-lg border border-slate-border bg-surface-container-lowest p-4 shadow-soft">
          <div className="flex items-center justify-between">
            <p className="text-xs font-bold uppercase tracking-wider text-amber-700">Pending</p>
            <Clock className="h-3.5 w-3.5 text-amber-600" />
          </div>
          <p className="mt-1 font-mono text-lg md:text-xl font-bold text-amber-800">
            LKR {formatCurrency(metrics.pendingAmount)}
          </p>
          <p className="mt-1 text-xs text-slate-400">
            {invoices.filter((i) => i.status === InvoiceStatus.PAYMENT_PENDING || i.status === InvoiceStatus.ISSUED).length} awaiting payment
          </p>
        </div>

        <div className="rounded-lg border border-slate-border bg-surface-container-lowest p-4 shadow-soft">
          <div className="flex items-center justify-between">
            <p className="text-xs font-bold uppercase tracking-wider text-rose-700">Attention Needed</p>
            <AlertCircle className="h-3.5 w-3.5 text-rose-600" />
          </div>
          <p className="mt-1 font-mono text-lg md:text-xl font-bold text-rose-700">
            {metrics.overdueCount} Invoices
          </p>
          <p className="mt-1 text-xs text-slate-400">Overdue or failed gateway txns</p>
        </div>
      </div>

      {/* Filter Bar matching Image 4 */}
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
      />

      {/* Main Ledger Card */}
      <Card className="p-0 overflow-hidden min-h-[420px] flex flex-col justify-between shadow-soft border-slate-border bg-surface-container-lowest">
        {totalEntries > 0 ? (
          <div>
            <InvoiceListTable invoices={paginatedInvoices} onSelectInvoice={handleSelectInvoice} />
          </div>
        ) : (
          <InvoiceEmptyState onClearFilters={handleResetFilters} isFiltered={hasActiveFilters} />
        )}

        {/* Table Footer with Pagination matching Image 4 */}
        <div className="border-t border-slate-border bg-surface-container-lowest px-4 py-3 flex flex-wrap items-center justify-between gap-3 text-body-md text-slate-600">
          <div>
            Showing <span className="font-semibold text-on-surface">{startEntry}</span> to{' '}
            <span className="font-semibold text-on-surface">{endEntry}</span> of{' '}
            <span className="font-semibold text-on-surface">{totalEntries}</span> entries
          </div>

          {totalPages > 1 && (
            <div className="flex items-center gap-1.5">
              {/* Prev Button */}
              <button
                type="button"
                onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                disabled={currentPage <= 1}
                aria-label="Previous page"
                className="flex h-8 w-8 items-center justify-center rounded border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 disabled:opacity-40 disabled:pointer-events-none transition-colors cursor-pointer"
              >
                ‹
              </button>

              {/* Page numbers */}
              {Array.from({ length: totalPages }, (_, i) => i + 1).map((pageNum) => (
                <button
                  key={pageNum}
                  type="button"
                  onClick={() => setCurrentPage(pageNum)}
                  className={cx(
                    'flex h-8 min-w-8 items-center justify-center rounded px-2 text-body-md font-medium transition-colors cursor-pointer',
                    currentPage === pageNum
                      ? 'bg-primary text-on-primary font-bold'
                      : 'border border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                  )}
                >
                  {pageNum}
                </button>
              ))}

              {/* Next Button */}
              <button
                type="button"
                onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                disabled={currentPage >= totalPages}
                aria-label="Next page"
                className="flex h-8 w-8 items-center justify-center rounded border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 disabled:opacity-40 disabled:pointer-events-none transition-colors cursor-pointer"
              >
                ›
              </button>
            </div>
          )}
        </div>
      </Card>

      {/* Invoice Details Slide-over Drawer matching Image 5 */}
      <InvoiceDetailsDrawer
        invoice={selectedInvoice}
        isOpen={isDrawerOpen}
        onClose={() => setIsDrawerOpen(false)}
      />
    </div>
  )
}

export default BillingPage
