import { useState } from 'react'
import { Search } from 'lucide-react'
import Input from '../Input.jsx'
import ErrorMessage from '../form/ErrorMessage.jsx'
import { useLocationSearch } from '../../hooks/useLocationSearch.js'

const MIN_SEARCH_LENGTH = 3

/**
 * Search box (Nominatim) + visible/editable address field for one location.
 * No map of its own — DualLocationPicker renders one shared map for both
 * the pickup and dropoff instances of this component, keyed off which
 * marker each result/edit is meant to move.
 *
 * @param {string} [id]
 * @param {string} [label]
 * @param {string} address
 * @param {string} [error]
 * @param {(result: { displayName: string, lat: number, lng: number }) => void} onSelectResult
 * @param {(address: string) => void} onAddressChange
 */
function LocationSearchField({ id, label, address, error, onSelectResult, onAddressChange }) {
  const [searchQuery, setSearchQuery] = useState('')
  const [isResultsOpen, setIsResultsOpen] = useState(false)
  const { results, isSearching } = useLocationSearch(searchQuery)

  function handleSelectResult(result) {
    onSelectResult(result)
    setSearchQuery('')
    setIsResultsOpen(false)
  }

  return (
    <div>
      {label && (
        <label htmlFor={id} className="mb-1.5 block text-body-md font-semibold text-on-surface">
          {label}
        </label>
      )}

      <div className="relative mb-2">
        <Search
          className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-on-surface-variant"
          strokeWidth={1.5}
        />
        <Input
          value={searchQuery}
          onChange={(event) => setSearchQuery(event.target.value)}
          onFocus={() => setIsResultsOpen(true)}
          onBlur={() => setIsResultsOpen(false)}
          placeholder="Search for a place or address"
          className="pl-9"
        />
        {isResultsOpen && searchQuery.trim().length >= MIN_SEARCH_LENGTH && (
          <div className="absolute z-[1000] mt-1 max-h-60 w-full overflow-auto rounded-md border border-slate-border bg-surface-container-lowest shadow-soft">
            {isSearching && <p className="px-3 py-2 text-body-md text-on-surface-variant">Searching…</p>}
            {!isSearching && results.length === 0 && (
              <p className="px-3 py-2 text-body-md text-on-surface-variant">No results</p>
            )}
            {results.map((result, index) => (
              <button
                key={`${result.lat}-${result.lng}-${index}`}
                type="button"
                onMouseDown={(event) => {
                  event.preventDefault()
                  handleSelectResult(result)
                }}
                className="block w-full px-3 py-2 text-left text-body-md text-on-surface hover:bg-surface-container"
              >
                {result.displayName}
              </button>
            ))}
          </div>
        )}
      </div>

      <Input
        id={id}
        value={address}
        onChange={(event) => onAddressChange(event.target.value)}
        placeholder="Address"
        error={Boolean(error)}
      />

      <ErrorMessage>{error}</ErrorMessage>
    </div>
  )
}

export default LocationSearchField
