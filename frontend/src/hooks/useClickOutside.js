import { useEffect } from 'react'

// UI-only local-stateful-logic hook (.claude/rules/frontend-design.md #4):
// calls `onOutsideClick` while `active` is true and a pointer event lands
// outside `ref`'s element. Generic enough for any dismissible popup
// (dropdown menus, popovers), not specific to one feature — pairs with
// useEscapeKey.js for the keyboard-dismiss half of the same pattern.
export function useClickOutside(ref, active, onOutsideClick) {
  useEffect(() => {
    if (!active) return undefined
    const onPointerDown = (event) => {
      if (ref.current && !ref.current.contains(event.target)) onOutsideClick()
    }
    document.addEventListener('mousedown', onPointerDown)
    return () => document.removeEventListener('mousedown', onPointerDown)
  }, [active, ref, onOutsideClick])
}
