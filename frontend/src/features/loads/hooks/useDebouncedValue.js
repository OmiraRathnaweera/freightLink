import { useEffect, useState } from 'react'

// UI-only local-stateful-logic hook (.claude/rules/frontend-design.md #4):
// returns `value`, updated only after it's stopped changing for `delay`ms.
// Used by LoadFilterBar so the search input doesn't re-request GET /loads
// on every keystroke.
export function useDebouncedValue(value, delay = 300) {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const timeout = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(timeout)
  }, [value, delay])

  return debounced
}
