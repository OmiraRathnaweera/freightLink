import { createContext, useMemo, useState } from 'react'

// This file exports both the context object and its provider component,
// which trips react-refresh's "only export components" rule; accepted here
// on purpose (the alternative — a third file just for the context object —
// isn't worth it for one const).
/* eslint-disable react-refresh/only-export-components */

// Context API is for small, localized UI state that only a handful of
// sibling/descendant components need — here, whether the dashboard's
// off-canvas sidebar is open. It must never hold auth/session data: that's
// read from unrelated places all over the app (route guards, API client,
// header) and belongs in Redux Toolkit instead (see
// src/features/auth/store/authSlice.js). (See ADR: state-management
// strategy.)
//
// The consumer hook (useSidebar) lives in src/hooks/useSidebar.js, not
// here — see that file for why.
//
// `isOpen` only matters below the `lg` breakpoint — DashboardLayout shows
// the sidebar as a fixed, always-visible rail at `lg:` and above regardless
// of this value; it's purely the mobile/tablet drawer's open/closed state.

export const SidebarContext = createContext(undefined)

export function SidebarProvider({ children }) {
  const [isOpen, setIsOpen] = useState(false)

  const toggleSidebar = () => setIsOpen((prev) => !prev)
  const closeSidebar = () => setIsOpen(false)

  // Memoize so consumers don't re-render on every parent render.
  const value = useMemo(() => ({ isOpen, toggleSidebar, closeSidebar }), [isOpen])

  return <SidebarContext.Provider value={value}>{children}</SidebarContext.Provider>
}
