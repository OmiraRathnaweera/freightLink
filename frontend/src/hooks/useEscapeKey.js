import { useEffect } from 'react'

// UI-only local-stateful-logic hook (.claude/rules/frontend-design.md #4):
// calls `onEscape` while `active` is true and the Escape key is pressed.
// Lives in src/hooks/ rather than a feature's hooks/ folder since it's
// generic enough for any dismissible overlay (drawers, modals, popovers),
// not specific to one feature.
export function useEscapeKey(active, onEscape) {
  useEffect(() => {
    if (!active) return undefined
    const onKeyDown = (event) => {
      if (event.key === 'Escape') onEscape()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [active, onEscape])
}
