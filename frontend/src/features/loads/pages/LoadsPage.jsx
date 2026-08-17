import { useMemo } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { PackageOpen } from 'lucide-react'
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
import LoadFilterBar from '../components/LoadFilterBar.jsx'
import LoadsTable from '../components/LoadsTable.jsx'
import Pagination from '../components/Pagination.jsx'

const DEFAULT_PAGE_SIZE = 20

// Load Control dashboard — GET /api/v1/loads is the source of truth
// (docs/load-management-api.md Section 3.3); every filter/sort/page
// control is synced to the URL via useSearchParams so the view is
// bookmarkable/shareable and survives a refresh.
function LoadsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const role = useAppSelector((state) => state.auth.role)

  const page = Number(searchParams.get('page') ?? '1')
  const pageSize = Number(searchParams.get('pageSize') ?? String(DEFAULT_PAGE_SIZE))
  const sortBy = searchParams.get('sortBy') ?? 'createdAt'
  const sortDir = searchParams.get('sortDir') ?? 'desc'
  const search = searchParams.get('search') ?? undefined
  const status = searchParams.get('status') ?? undefined
  const createdFrom = searchParams.get('createdFrom') ?? undefined
  const createdTo = searchParams.get('createdTo') ?? undefined

  // createdFrom/createdTo live in the URL as bare dates (from <input
  // type="date">) — expanded to inclusive-bound ISO datetimes only at the
  // API-call boundary, per Section 3.3's "inclusive lower/upper bound".
  const apiParams = useMemo(
    () => ({
      page,
      pageSize,
      sortBy,
      sortDir,
      search,
      status,
      createdFrom: createdFrom ? `${createdFrom}T00:00:00Z` : undefined,
      createdTo: createdTo ? `${createdTo}T23:59:59Z` : undefined,
    }),
    [page, pageSize, sortBy, sortDir, search, status, createdFrom, createdTo],
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

  function handleSortChange(column) {
    updateParams({ sortBy: column, sortDir: sortBy === column && sortDir === 'asc' ? 'desc' : 'asc' }, { resetPage: false })
  }

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="LOADS"
        title="My Loads"
        description="Track every load you've posted, from draft to delivery."
        actions={
          <Button as={Link} to="/loads/new" variant="primary">
            Post a Load
          </Button>
        }
      />

      <LoadFilterBar search={search} status={status} createdFrom={createdFrom} createdTo={createdTo} onChange={updateParams} />

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
              showShipperColumn={role === UserRole.ADMIN}
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
            title="No loads found"
            description="Try different filters, or post a new load."
            action={
              <Button as={Link} to="/loads/new" variant="primary">
                Post a Load
              </Button>
            }
          />
        )}
      </Card>
    </div>
  )
}

export default LoadsPage
