import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Building2, PackageOpen, ShoppingBag, Truck } from 'lucide-react'
import PageHeader from '../../../components/PageHeader.jsx'
import Button from '../../../components/Button.jsx'
import Card from '../../../components/Card.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import ErrorState from '../../../components/ErrorState.jsx'
import Skeleton from '../../../components/Skeleton.jsx'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { UserRole } from '../../../lib/enums.js'
import { useLoadsQuery } from '../api/loadsApi.js'
import { getLoadErrorMessage } from '../lib/errorMessages.js'
import { canCreateLoad } from '../lib/loadPermissions.js'
import LoadFilterBar from '../components/LoadFilterBar.jsx'
import LoadsTable from '../components/LoadsTable.jsx'
import Pagination from '../components/Pagination.jsx'
import AcceptShipmentDialog from '../components/AcceptShipmentDialog.jsx'
import { cx } from '../../../lib/cx.js'

const DEFAULT_PAGE_SIZE = 20

function LoadsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const role = useAppSelector((state) => state.auth.role)
  const isAgencyStaff = role === UserRole.AGENCY_STAFF

  const [selectedAcceptLoad, setSelectedAcceptLoad] = useState(null)

  const activeTab = searchParams.get('tab') ?? (isAgencyStaff ? 'marketplace' : 'all')

  const page = Number(searchParams.get('page') ?? '1')
  const pageSize = Number(searchParams.get('pageSize') ?? String(DEFAULT_PAGE_SIZE))
  const sortBy = searchParams.get('sortBy') ?? 'createdAt'
  const sortDir = searchParams.get('sortDir') ?? 'desc'
  const search = searchParams.get('search') ?? undefined
  const statusParam = searchParams.get('status') ?? undefined
  const createdFrom = searchParams.get('createdFrom') ?? undefined
  const createdTo = searchParams.get('createdTo') ?? undefined

  // For AgencyStaff in Marketplace tab, force marketplace = true and status = Posted
  const isMarketplaceMode = isAgencyStaff && activeTab === 'marketplace'
  const effectiveStatus = isMarketplaceMode ? 'Posted' : statusParam

  const apiParams = useMemo(
    () => ({
      page,
      pageSize,
      sortBy,
      sortDir,
      search,
      status: effectiveStatus,
      marketplace: isMarketplaceMode ? true : undefined,
      createdFrom: createdFrom ? `${createdFrom}T00:00:00.000Z` : undefined,
      createdTo: createdTo ? `${createdTo}T23:59:59.999Z` : undefined,
    }),
    [page, pageSize, sortBy, sortDir, search, effectiveStatus, isMarketplaceMode, createdFrom, createdTo],
  )

  const loadsQuery = useLoadsQuery(apiParams)

  function updateParams(patch, { resetPage = true } = {}) {
    const next = new URLSearchParams(searchParams)
    Object.entries(patch).forEach(([key, value]) => {
      if (value === undefined || value === null || value === '') {
        next.delete(key)
      } else {
        next.set(key, String(value))
      }
    })
    if (resetPage) next.delete('page')
    setSearchParams(next)
  }

  function handleTabChange(tab) {
    updateParams({ tab, page: 1, status: undefined })
  }

  function handleSortChange(column) {
    updateParams({ sortBy: column, sortDir: sortBy === column && sortDir === 'asc' ? 'desc' : 'asc' }, { resetPage: false })
  }

  const pageTitle = isAgencyStaff
    ? activeTab === 'marketplace'
      ? 'Freight Marketplace'
      : 'Assigned Shipments'
    : role === UserRole.ADMIN
      ? 'All Platform Loads'
      : 'My Loads'

  const pageDescription = isAgencyStaff
    ? activeTab === 'marketplace'
      ? 'Browse open transport jobs posted by shippers. Accept loads to claim them exclusively for your agency.'
      : 'Transport jobs claimed by your agency. Assign your registered drivers and vehicles to dispatch trips.'
    : role === UserRole.ADMIN
      ? 'Global oversight of all transport loads across the FreightLink platform.'
      : "Track every load you've posted, from draft to delivery."

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow={isAgencyStaff ? 'AGENCY PORTAL' : 'LOADS'}
        title={pageTitle}
        description={pageDescription}
        actions={
          canCreateLoad(role) && (
            <Button as={Link} to="/loads/new" variant="primary">
              Post a Load
            </Button>
          )
        }
      />

      {/* Agency Mode Tab Bar */}
      {isAgencyStaff && (
        <div className="flex items-center gap-2 border-b border-slate-200">
          <button
            type="button"
            onClick={() => handleTabChange('marketplace')}
            className={cx(
              'inline-flex items-center gap-2 border-b-2 px-4 py-2.5 text-body-md font-medium transition-colors',
              activeTab === 'marketplace'
                ? 'border-primary text-primary font-semibold'
                : 'border-transparent text-on-surface-variant hover:text-on-surface hover:border-slate-300',
            )}
          >
            <ShoppingBag className="h-4 w-4" />
            Marketplace (Available Loads)
          </button>
          <button
            type="button"
            onClick={() => handleTabChange('assigned')}
            className={cx(
              'inline-flex items-center gap-2 border-b-2 px-4 py-2.5 text-body-md font-medium transition-colors',
              activeTab === 'assigned'
                ? 'border-primary text-primary font-semibold'
                : 'border-transparent text-on-surface-variant hover:text-on-surface hover:border-slate-300',
            )}
          >
            <Truck className="h-4 w-4" />
            My Agency Shipments
          </button>
        </div>
      )}

      {!isMarketplaceMode && (
        <LoadFilterBar search={search} status={statusParam} createdFrom={createdFrom} createdTo={createdTo} onChange={updateParams} />
      )}

      <Card className="min-h-[60vh] p-0">
        {loadsQuery.isLoading ? (
          <div className="space-y-2 p-4">
            {Array.from({ length: 6 }).map((_, index) => (
              <Skeleton key={index} className="h-10 w-full" />
            ))}
          </div>
        ) : loadsQuery.isError ? (
          <ErrorState description={getLoadErrorMessage(loadsQuery.error)} onRetry={loadsQuery.refetch} />
        ) : loadsQuery.data.items.length > 0 ? (
          <>
            <LoadsTable
              loads={loadsQuery.data.items}
              sortBy={sortBy}
              sortDir={sortDir}
              onSortChange={handleSortChange}
              showShipperColumn={role === UserRole.ADMIN || isAgencyStaff}
              role={role}
              onAcceptLoad={isMarketplaceMode ? (load) => setSelectedAcceptLoad(load) : undefined}
              onDispatchLoad={isAgencyStaff && activeTab === 'assigned' ? (load) => load : undefined}
            />
            <Pagination
              page={loadsQuery.data.page}
              pageSize={loadsQuery.data.pageSize}
              totalItems={loadsQuery.data.totalItems}
              totalPages={loadsQuery.data.totalPages}
              onPageChange={(nextPage) => updateParams({ page: nextPage }, { resetPage: false })}
              onPageSizeChange={(nextPageSize) => updateParams({ pageSize: nextPageSize })}
            />
          </>
        ) : (
          <EmptyState
            icon={PackageOpen}
            title={
              isMarketplaceMode
                ? 'No available marketplace loads'
                : isAgencyStaff && activeTab === 'assigned'
                  ? 'No assigned shipments'
                  : 'No loads found'
            }
            description={
              isMarketplaceMode
                ? 'All posted shipments have been accepted. Check back shortly for new loads.'
                : isAgencyStaff && activeTab === 'assigned'
                  ? 'Your agency has not claimed any shipments yet. Visit the Marketplace tab to accept open loads.'
                  : 'Try different filters, or post a new load.'
            }
            action={
              canCreateLoad(role) ? (
                <Button as={Link} to="/loads/new" variant="primary">
                  Post a Load
                </Button>
              ) : isMarketplaceMode ? (
                <Button variant="secondary" onClick={() => loadsQuery.refetch()}>
                  Refresh Marketplace
                </Button>
              ) : undefined
            }
          />
        )}
      </Card>

      {/* Accept Shipment Modal for Agency */}
      {selectedAcceptLoad && (
        <AcceptShipmentDialog
          load={selectedAcceptLoad}
          onClose={() => setSelectedAcceptLoad(null)}
          onAccepted={() => {
            loadsQuery.refetch()
            handleTabChange('assigned')
          }}
        />
      )}
    </div>
  )
}

export default LoadsPage
