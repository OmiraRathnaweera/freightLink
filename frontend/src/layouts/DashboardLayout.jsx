import { NavLink, Outlet } from 'react-router-dom'
import {
  Building2,
  CircleDollarSign,
  LogOut,
  Menu,
  Package,
  Receipt,
  Route as RouteIcon,
  Truck,
  Workflow,
  X,
} from 'lucide-react'
import { SidebarProvider } from './SidebarContext.jsx'
import { useSidebar } from '../hooks/useSidebar.js'
import { useEscapeKey } from '../hooks/useEscapeKey.js'
import { useAppDispatch } from '../hooks/useAppDispatch.js'
import { useAppSelector } from '../hooks/useAppSelector.js'
import { logout } from '../features/auth/store/authSlice.js'
import { isRouteAllowedForRole } from '../features/auth/lib/roleAccess.js'
import { cx } from '../lib/cx.js'

// This is a layout route (see src/routes/AppRoutes.jsx): react-router
// renders it for every matching nested route and swaps <Outlet /> for
// whichever child route matched, instead of this component taking an
// explicit `children` prop.
//
// SidebarProvider is mounted here (not in main.jsx) on purpose: sidebar
// open/closed state is only ever read by this layout, so it stays out of
// the global Redux store â€” a textbook case for Context instead of Redux
// Toolkit. (See ADR: state-management strategy.)
//
// Dark off-canvas sidebar, per the Loads-screens Stitch research: of the 12
// screens, only "My Loads" Empty/Loading/Error (screens 9-11) have a
// genuinely-coded dark sidebar (bg-primary) â€” the "Admin Panel" screens
// (4,6,7) look dark in their screenshots but the exported code is actually
// light (a Stitch screenshot/code mismatch), and 6 other screens use no
// sidebar at all. This adopts the one real dark-sidebar pattern globally,
// per the user's explicit instruction. None of Stitch's exports had working
// off-canvas behavior (just `hidden` below md with an inert hamburger) â€”
// the open/close drawer behavior here is built from scratch.
//
// Nav items map to this app's real 5 feature routes, not either of the
// Stitch Loads-screens' own nav lists (both reference pages that don't
// exist in this app). Visibility is filtered per role via
// isRouteAllowedForRole below, reading the same centralized
// src/features/auth/lib/roleAccess.js map ProtectedRoute uses â€” so the
// sidebar can never link to a section the signed-in role would just get
// bounced from, and there's one place (not two) to update when a role
// gains access to a route.
const NAV_ITEMS = [
  { to: '/loads', label: 'Loads', icon: Package },
  { to: '/agencies', label: 'Agencies', icon: Building2, children: [ { to: '/agencies', label: 'All Agencies', end: true }, { to: '/agencies/verification', label: 'Verification Queue' } ] },
  { to: '/trips', label: 'Trips', icon: RouteIcon },
  { to: '/billing', label: 'Billing', icon: Receipt },
  { to: '/agent-workflows', label: 'Agent Workflows', icon: Workflow },
  {
    to: '/pricing-config',
    label: 'Pricing Config',
    icon: CircleDollarSign,
    children: [
      { to: '/pricing-config/fuel-rates', label: 'Fuel Prices' },
      { to: '/pricing-config/vehicle-efficiency', label: 'Vehicle Class Efficiency' },
      { to: '/pricing-config/formula', label: 'Pricing Formula' },
    ],
  },
]

function DashboardLayout() {
  return (
    <SidebarProvider>
      <DashboardLayoutContent />
    </SidebarProvider>
  )
}

function DashboardLayoutContent() {
  const { isOpen, toggleSidebar, closeSidebar } = useSidebar()
  const dispatch = useAppDispatch()
  const { user, role } = useAppSelector((state) => state.auth)

  useEscapeKey(isOpen, closeSidebar)

  const visibleNavItems = NAV_ITEMS.filter((item) => isRouteAllowedForRole(role, item.to))

  return (
    <div className="flex min-h-screen bg-background text-on-background lg:flex-row">
      {isOpen && (
        <button
          type="button"
          aria-label="Close navigation"
          onClick={closeSidebar}
          className="fixed inset-0 z-30 bg-primary/40 lg:hidden"
        />
      )}

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
        <nav className="flex flex-1 flex-col gap-1 overflow-y-auto px-3 py-4">
          {visibleNavItems.map((item) =>
            item.children ? (
              <div key={item.to} className="flex flex-col gap-1">
                <div className="flex items-center gap-3 px-3 py-2 text-body-md text-on-primary/70">
                  <item.icon className="h-5 w-5 shrink-0" strokeWidth={1.5} />
                  {item.label}
                </div>
                <div className="flex flex-col gap-1 pl-8">
                  {item.children.map((child) => (
                    <NavLink
                      key={child.to}
                      to={child.to}
                      end={child.end}
                      onClick={closeSidebar}
                      className={({ isActive }) =>
                        cx(
                          'rounded-md px-3 py-1.5 text-body-md transition-colors',
                          isActive
                            ? 'bg-primary-container text-on-primary'
                            : 'text-on-primary/70 hover:bg-primary-container hover:text-on-primary',
                        )
                      }
                    >
                      {child.label}
                    </NavLink>
                  ))}
                </div>
              </div>
            ) : (
              <NavLink
                key={item.to}
                to={item.to}
                onClick={closeSidebar}
                className={({ isActive }) =>
                  cx(
                    'flex items-center gap-3 rounded-md px-3 py-2 text-body-md transition-colors',
                    isActive
                      ? 'bg-primary-container text-on-primary'
                      : 'text-on-primary/70 hover:bg-primary-container hover:text-on-primary',
                  )
                }
              >
                <item.icon className="h-5 w-5 shrink-0" strokeWidth={1.5} />
                {item.label}
              </NavLink>
            ),
          )}
        </nav>
        <div className="flex items-center justify-between gap-2 border-t border-primary-container px-4 py-3">
          <span className="truncate text-body-md text-on-primary/70">{user?.email}</span>
          <button
            type="button"
            onClick={() => dispatch(logout())}
            aria-label="Log out"
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded text-on-primary/70 hover:bg-primary-container hover:text-on-primary"
          >
            <LogOut className="h-4 w-4" strokeWidth={1.5} />
          </button>
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
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
        <main className="flex-1 overflow-y-auto p-container-margin">
          <Outlet />
        </main>
      </div>
    </div>
  )
}

export default DashboardLayout

