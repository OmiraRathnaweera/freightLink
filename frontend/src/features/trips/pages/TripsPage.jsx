import { useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Plus, RefreshCw, Truck } from "lucide-react";
import PageHeader from "../../../components/PageHeader.jsx";
import Button from "../../../components/Button.jsx";
import Card from "../../../components/Card.jsx";
import EmptyState from "../../../components/EmptyState.jsx";
import ErrorState from "../../../components/ErrorState.jsx";
import Skeleton from "../../../components/Skeleton.jsx";
import { useAppSelector } from "../../../hooks/useAppSelector.js";
import { UserRole } from "../../../lib/enums.js";
import { useTripsQuery } from "../api/tripsApi.js";
import { getTripErrorMessage } from "../lib/errorMessages.js";
import TripsTable from "../components/TripsTable.jsx";
import TripFilterBar from "../components/TripFilterBar.jsx";
import Pagination from "../components/Pagination.jsx";
import CreateTripDialog from "../components/CreateTripDialog.jsx";

const DEFAULT_PAGE_SIZE = 20;

function TripsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const role = useAppSelector((state) => state.auth.role);

  const page = Number(searchParams.get("page") ?? "1");
  const pageSize = Number(searchParams.get("pageSize") ?? String(DEFAULT_PAGE_SIZE));
  const sortBy = searchParams.get("sortBy") ?? "createdAt";
  const sortDir = searchParams.get("sortDir") ?? "desc";
  const status = searchParams.get("status") ?? undefined;

  const apiParams = useMemo(
    () => ({
      page,
      pageSize,
      sortBy,
      sortDir,
      status: status || undefined,
    }),
    [page, pageSize, sortBy, sortDir, status]
  );

  const tripsQuery = useTripsQuery(apiParams, { refetchInterval: 10000 });

  function updateParams(patch, { resetPage = true } = {}) {
    const next = new URLSearchParams(searchParams);
    Object.entries(patch).forEach(([key, value]) => {
      if (value === undefined || value === null || value === "") {
        next.delete(key);
      } else {
        next.set(key, String(value));
      }
    });
    if (resetPage) next.delete("page");
    setSearchParams(next);
  }

  function handleSortChange(column) {
    updateParams(
      { sortBy: column, sortDir: sortBy === column && sortDir === "asc" ? "desc" : "asc" },
      { resetPage: false }
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="TRIPS"
        title="Active Trips"
        description="Monitor active and in-progress trips, track statuses, and view proof evidence."
        actions={
          <div className="flex items-center gap-2">
            <Button
              variant="secondary"
              onClick={() => tripsQuery.refetch()}
              disabled={tripsQuery.isFetching}
              className="inline-flex items-center gap-1.5"
              title="Refresh active trips"
            >
              <RefreshCw className={`h-4 w-4 ${tripsQuery.isFetching ? "animate-spin" : ""}`} />
              Refresh
            </Button>
            {(role === UserRole.AGENCY_STAFF || role === UserRole.ADMIN) && (
              <Button
                variant="primary"
                onClick={() => setIsCreateDialogOpen(true)}
                className="inline-flex items-center gap-1.5"
              >
                <Plus className="h-4 w-4" />
                Dispatch Trip
              </Button>
            )}
          </div>
        }
      />

      <TripFilterBar status={status} onChange={updateParams} />

      <Card className="min-h-[60vh] p-0 flex flex-col justify-between">
        {tripsQuery.isError ? (
          <ErrorState
            description={getTripErrorMessage(tripsQuery.error)}
            onRetry={tripsQuery.refetch}
          />
        ) : !tripsQuery.data ? (
          <div className="space-y-2 p-4">
            {Array.from({ length: 6 }).map((_, index) => (
              <Skeleton key={index} className="h-10 w-full" />
            ))}
          </div>
        ) : tripsQuery.data.items.length > 0 ? (
          <div>
            <TripsTable
              trips={tripsQuery.data.items}
              sortBy={sortBy}
              sortDir={sortDir}
              onSortChange={handleSortChange}
            />
          </div>
        ) : (
          <EmptyState
            icon={Truck}
            title={status ? "No matching trips" : "No active trips"}
            description={
              status
                ? `There are no trips currently in '${status}' status.`
                : "Trips will appear here once an assignment is accepted and dispatched."
            }
          />
        )}

        {tripsQuery.data && tripsQuery.data.totalItems > 0 && (
          <Pagination
            page={tripsQuery.data.page}
            pageSize={tripsQuery.data.pageSize}
            totalItems={tripsQuery.data.totalItems}
            totalPages={tripsQuery.data.totalPages}
            onPageChange={(nextPage) => updateParams({ page: nextPage }, { resetPage: false })}
            onPageSizeChange={(nextSize) => updateParams({ pageSize: nextSize })}
          />
        )}
      </Card>

      {isCreateDialogOpen && (
        <CreateTripDialog
          onClose={() => setIsCreateDialogOpen(false)}
          onCreated={() => tripsQuery.refetch()}
        />
      )}
    </div>
  );
}

export default TripsPage;
