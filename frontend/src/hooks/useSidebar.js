import { useContext } from 'react'
import { SidebarContext } from '../layouts/SidebarContext.jsx'

// Consumer hook for SidebarContext (src/layouts/SidebarContext.jsx). Lives
// here rather than in the layouts folder per .claude/rules/frontend-design.md
// #4 — hook-shaped code (state/behavior) belongs in src/hooks/ even when
// it's tightly coupled to one Context, matching the useEscapeKey.js
// convention.
export function useSidebar() {
  const context = useContext(SidebarContext)
  if (context === undefined) {
    throw new Error('useSidebar must be used within a SidebarProvider')
  }
  return context
}
