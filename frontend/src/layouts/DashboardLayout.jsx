import { useEffect } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { Truck, Menu, X, Package, Building2, Route as RouteIcon, Receipt, Workflow } from 'lucide-react'
import { SidebarProvider, useSidebar } from './SidebarContext.jsx'
import { cx } from '../lib/cx.js'

// SidebarProvider is mounted here (not in main.jsx) on purpose: sidebar
// open/closed state is only ever read by this layout and its children, so
// it stays out of the global Redux store — a textbook case for Context
// instead of Redux Toolkit. (See ADR: state-management strategy.)
//
// This is a layout route (see src/routes/AppRoutes.jsx): react-router
// renders it for every matching nested route and swaps <Outlet /> for
// whichever child route matched, instead of this component taking an
// explicit `children` prop.
//
// Dark off-canvas sidebar, per the Loads-screens Stitch research: of the 12
// screens, only "My Loads" Empty/Loading/Error (screens 9-11) have a
// genuinely-coded dark sidebar (bg-primary) — the "Admin Panel" screens
// (4,6,7) look dark in their screenshots but the exported code is actually
// light (a Stitch screenshot/code mismatch), and 6 other screens use no
// sidebar at all. This adopts the one real dark-sidebar pattern globally,
// per the user's explicit instruction. None of Stitch's exports had working
// off-canvas behavior (just `hidden` below md with an inert hamburger) —
// the open/close drawer behavior below is built from scratch.
//
// Nav items map to this app's real 5 feature routes, not either of Stitch's
// own nav lists (both reference pages that don't exist in this app).
const NAV_ITEMS = [
  { to: '/loads', label: 'Loads', icon: Package },
  { to: '/agencies', label: 'Agencies', icon: Building2 },
  { to: '/trips', label: 'Trips', icon: RouteIcon },
  { to: '/billing', label: 'Billing', icon: Receipt },
  { to: '/agent-workflows', label: 'Agent Workflows', icon: Workflow },
]

function SidebarNav({ onNavigate }) {
  return (
    <nav className="flex flex-1 flex-col gap-1 overflow-y-auto px-3 py-4">
      {NAV_ITEMS.map(({ to, label, icon: Icon }) => (
        <NavLink
          key={to}
          to={to}
          onClick={onNavigate}
          className={({ isActive }) =>
            cx(
              'flex items-center gap-3 rounded-md px-3 py-2 text-body-md transition-colors',
              isActive ? 'bg-primary-container text-on-primary' : 'text-on-primary/70 hover:bg-primary-container hover:text-on-primary',
            )
          }
        >
          <Icon className="h-5 w-5 shrink-0" strokeWidth={1.5} />
          {label}
        </NavLink>
      ))}
    </nav>
  )
}

function Sidebar() {
  const { isOpen, closeSidebar } = useSidebar()

  // Off-canvas below `lg`: fixed drawer, translated fully off-screen unless
  // isOpen. At `lg:` and above it's a normal in-flow, always-visible rail —
  // isOpen never applies there, so the explicit close button is hidden then.
  return (
    <aside
      className={cx(
        'fixed inset-y-0 left-0 z-40 flex w-64 shrink-0 flex-col bg-primary transition-transform duration-200 ease-out',
        'lg:static lg:translate-x-0',
        isOpen ? 'translate-x-0' : '-translate-x-full',
      )}
    >
      <div className="flex h-16 items-center justify-between gap-2 border-b border-primary-container px-4">
        <div className="flex items-center gap-2">
          <Truck className="h-6 w-6 text-on-primary" strokeWidth={1.5} />
          <span className="text-headline-md font-bold text-on-primary">FreightLink</span>
        </div>
        <button
          type="button"
          onClick={closeSidebar}
          aria-label="Close navigation"
          className="flex h-11 w-11 items-center justify-center text-on-primary lg:hidden"
        >
          <X className="h-6 w-6" strokeWidth={1.5} />
        </button>
      </div>
      <SidebarNav onNavigate={closeSidebar} />
    </aside>
  )
}

function MobileTopBar() {
  const { toggleSidebar } = useSidebar()
  return (
    <div className="flex h-16 shrink-0 items-center gap-3 border-b border-slate-border bg-surface-container-lowest px-container-margin lg:hidden">
      <button
        type="button"
        onClick={toggleSidebar}
        aria-label="Open navigation"
        className="flex h-11 w-11 items-center justify-center text-primary"
      >
        <Menu className="h-6 w-6" strokeWidth={1.5} />
      </button>
      <span className="text-headline-md font-bold text-primary">FreightLink</span>
    </div>
  )
}

function SidebarBackdrop() {
  const { isOpen, closeSidebar } = useSidebar()

  // Navy-tinted scrim, reusing the same convention DESIGN.md specifies for
  // the AI-approval modal overlay ("Human-in-the-Loop Overlay").
  if (!isOpen) return null
  return (
    <button
      type="button"
      aria-label="Close navigation"
      onClick={closeSidebar}
      className="fixed inset-0 z-30 bg-primary/40 lg:hidden"
    />
  )
}

function DashboardShell() {
  const { isOpen, closeSidebar } = useSidebar()

  useEffect(() => {
    if (!isOpen) return undefined
    const onKeyDown = (event) => {
      if (event.key === 'Escape') closeSidebar()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [isOpen, closeSidebar])

  return (
    <div className="flex min-h-screen bg-background text-on-background lg:flex-row">
      <SidebarBackdrop />
      <Sidebar />
      <div className="flex min-w-0 flex-1 flex-col">
        <MobileTopBar />
        <main className="flex-1 overflow-y-auto p-container-margin">
          <Outlet />
        </main>
      </div>
    </div>
  )
}

function DashboardLayout() {
  return (
    <SidebarProvider>
      <DashboardShell />
    </SidebarProvider>
  )
}

export default DashboardLayout
