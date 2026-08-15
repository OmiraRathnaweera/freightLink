import { Outlet } from 'react-router-dom'
import { SidebarProvider, useSidebar } from './SidebarContext.jsx'
import Button from '../components/Button.jsx'

// SidebarProvider is mounted here (not in main.jsx) on purpose: sidebar
// collapse state is only ever read by this layout and its children, so it
// stays out of the global Redux store — a textbook case for Context
// instead of Redux Toolkit. (See ADR: state-management strategy.)
//
// This is a layout route (see src/routes/AppRoutes.jsx): react-router
// renders it for every matching nested route and swaps <Outlet /> for
// whichever child route matched, instead of this component taking an
// explicit `children` prop.
//
// Styling here is shell-only (DESIGN.md tokens applied to the chrome around
// <Outlet />) — no feature UI. DESIGN.md doesn't specify a sidebar width or
// a dedicated "ghost/icon" button variant, so the toggle reuses the
// `secondary` Button variant and the width (w-60) is an undocumented
// placeholder, not a spec'd token.

function SidebarToggleButton() {
  const { isCollapsed, toggleSidebar } = useSidebar()
  return (
    <Button variant="secondary" onClick={toggleSidebar} className="w-full">
      {isCollapsed ? 'Expand' : 'Collapse'} sidebar
    </Button>
  )
}

function DashboardLayout() {
  return (
    <SidebarProvider>
      <div className="flex min-h-screen bg-background text-on-background">
        <aside className="flex w-60 shrink-0 flex-col gap-4 border-r border-slate-border bg-primary p-4 text-on-primary">
          <SidebarToggleButton />
          {/* TODO: real sidebar nav */}
        </aside>
        <main className="flex-1 overflow-y-auto p-container-margin">
          <Outlet />
        </main>
      </div>
    </SidebarProvider>
  )
}

export default DashboardLayout
