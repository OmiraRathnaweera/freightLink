import { useEffect, useState } from 'react'

// UI-only local-stateful-logic hook (.claude/rules/frontend-design.md #4):
// returns `value`, updated only after it's stopped changing for `delay`ms.
// Shared version of src/features/loads/hooks/useDebouncedValue.js, promoted
// to src/hooks/ because it's used by src/hooks/useLocationSearch.js, which
// backs the shared LocationPicker component rather than one feature.
export function useDebouncedValue(value, delay = 300) {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const timeout = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(timeout)
  }, [value, delay])

  return debounced
}
