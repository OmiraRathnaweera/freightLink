import { Outlet } from 'react-router-dom'
import { SidebarProvider, useSidebar } from './SidebarContext.jsx'

// SidebarProvider is mounted here (not in main.jsx) on purpose: sidebar
// collapse state is only ever read by this layout and its children, so it
// stays out of the global Redux store — a textbook case for Context
// instead of Redux Toolkit. (See ADR: state-management strategy.)
//
// This is a layout route (see src/routes/AppRoutes.jsx): react-router
// renders it for every matching nested route and swaps <Outlet /> for
// whichever child route matched, instead of this component taking an
// explicit `children` prop.

function SidebarToggleButton() {
  const { isCollapsed, toggleSidebar } = useSidebar()
  return (
    <button type="button" onClick={toggleSidebar}>
      {isCollapsed ? 'Expand' : 'Collapse'} sidebar
    </button>
  )
}

function DashboardLayout() {
  return (
    <SidebarProvider>
      <div className="dashboard-layout">
        <aside className="dashboard-sidebar">
          <SidebarToggleButton />
          {/* TODO: real sidebar nav */}
        </aside>
        <main className="dashboard-content">
          <Outlet />
        </main>
      </div>
    </SidebarProvider>
  )
}

export default DashboardLayout
