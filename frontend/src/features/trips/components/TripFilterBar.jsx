import { RotateCcw } from "lucide-react";
import Button from "../../../components/Button.jsx";
import { TripStatus } from "../../../lib/enums.js";

const STATUS_OPTIONS = [
  { value: "", label: "All Statuses" },
  { value: TripStatus.ASSIGNED, label: "Assigned" },
  { value: TripStatus.PICKED_UP, label: "Picked Up" },
  { value: TripStatus.IN_TRANSIT, label: "In Transit" },
  { value: TripStatus.DELIVERED, label: "Delivered" },
  { value: TripStatus.CANCELLED, label: "Cancelled" },
];

function TripFilterBar({ status, onChange }) {
  const hasActiveFilters = Boolean(status);

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 bg-white p-3 rounded-lg border border-slate-200">
      <div className="flex flex-wrap items-center gap-3">
        <label className="text-sm font-medium text-on-surface flex items-center gap-2">
          <span>Status:</span>
          <select
            value={status ?? ""}
            onChange={(e) => onChange({ status: e.target.value || undefined })}
            className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-slate-border"
          >
            {STATUS_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>
      </div>

      {hasActiveFilters && (
        <Button
          variant="secondary"
          size="sm"
          onClick={() => onChange({ status: undefined })}
          className="inline-flex items-center gap-1.5 text-xs text-on-surface-variant hover:text-on-surface"
        >
          <RotateCcw className="h-3.5 w-3.5" />
          Reset Filters
        </Button>
      )}
    </div>
  );
}

export default TripFilterBar;
