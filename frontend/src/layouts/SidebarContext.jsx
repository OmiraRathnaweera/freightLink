import { createContext, useContext, useMemo, useState } from 'react'

// This file colocates the provider component with its custom hook, which is
// the standard React Context pattern — it trips react-refresh's
// "only export components" rule, which we accept here on purpose.
/* eslint-disable react-refresh/only-export-components */

// Context API is for small, localized UI state that only a handful of
// sibling/descendant components need — here, whether the dashboard sidebar
// is collapsed. It must never hold auth/session data: that's read from
// unrelated places all over the app (route guards, API client, header) and
// belongs in Redux Toolkit instead (see src/features/auth/store/authSlice.js).
// (See ADR: state-management strategy.)
//
// Pattern: createContext -> Provider component -> custom hook.

const SidebarContext = createContext(undefined)

export function SidebarProvider({ children }) {
  const [isCollapsed, setIsCollapsed] = useState(false)

  const toggleSidebar = () => setIsCollapsed((prev) => !prev)

  // Memoize so consumers don't re-render on every parent render.
  const value = useMemo(() => ({ isCollapsed, toggleSidebar }), [isCollapsed])

  return <SidebarContext.Provider value={value}>{children}</SidebarContext.Provider>
}

export function useSidebar() {
  const context = useContext(SidebarContext)
  if (context === undefined) {
    throw new Error('useSidebar must be used within a SidebarProvider')
  }
  return context
}
