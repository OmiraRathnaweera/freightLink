import { useEffect, useState } from 'react'
import { Search } from 'lucide-react'
import Input from '../../../components/Input.jsx'
import { LoadStatus } from '../../../lib/enums.js'
import { useDebouncedValue } from '../hooks/useDebouncedValue.js'

const STATUS_OPTIONS = Object.values(LoadStatus)

// Search/status/date-range controls for the Load Control table. LoadsPage
// owns the actual state (synced to the URL via useSearchParams) — this
// component is a plain controlled input group that calls `onChange` with a
// partial patch. Search is debounced locally before bubbling up so typing
// doesn't re-issue GET /loads on every keystroke; status/date changes
// bubble up immediately.
function LoadFilterBar({ search, status, createdFrom, createdTo, onChange }) {
  const [searchInput, setSearchInput] = useState(search ?? '')
  const debouncedSearch = useDebouncedValue(searchInput, 400)

  useEffect(() => {
    if (debouncedSearch !== (search ?? '')) {
      onChange({ search: debouncedSearch || undefined })
    }
    // Only re-run when the debounced value itself changes — `search` and
    // `onChange` change as a downstream *effect* of this same call, so
    // including them would create a feedback loop.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedSearch])

  return (
    <div className="flex flex-wrap items-end gap-3">
      <div className="relative min-w-[220px] flex-1">
        <Search
          className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-on-surface-variant"
          strokeWidth={1.5}
        />
        <Input
          value={searchInput}
          onChange={(event) => setSearchInput(event.target.value)}
          placeholder="Search by reference, cargo, or address"
          className="pl-9"
        />
      </div>

      <div>
        <label className="mb-1.5 block text-body-md font-semibold text-on-surface">Status</label>
        <select
          value={status ?? ''}
          onChange={(event) => onChange({ status: event.target.value || undefined })}
          className="w-40 rounded-md border border-slate-300 bg-white px-3 py-2 text-[15px] text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-slate-border"
        >
          <option value="">All statuses</option>
          {STATUS_OPTIONS.map((value) => (
            <option key={value} value={value}>
              {value}
            </option>
          ))}
        </select>
      </div>

      <div>
        <label className="mb-1.5 block text-body-md font-semibold text-on-surface">Created from</label>
        <Input
          type="date"
          value={createdFrom ?? ''}
          onChange={(event) => onChange({ createdFrom: event.target.value || undefined })}
          className="w-40"
        />
      </div>

      <div>
        <label className="mb-1.5 block text-body-md font-semibold text-on-surface">Created to</label>
        <Input
          type="date"
          value={createdTo ?? ''}
          onChange={(event) => onChange({ createdTo: event.target.value || undefined })}
          className="w-40"
        />
      </div>
    </div>
  )
}

export default LoadFilterBar
