import { Link } from 'react-router-dom'
import { LogOut, Smartphone, ShieldAlert } from 'lucide-react'
import Button from '../../../components/Button.jsx'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { useAppDispatch } from '../../../hooks/useAppDispatch.js'
import { logout } from '../store/authSlice.js'
import { UserRole } from '../../../lib/enums.js'

function UnauthorizedPage() {
  const role = useAppSelector((state) => state.auth.role)
  const dispatch = useAppDispatch()
  const isDriver = role === UserRole.DRIVER

  if (isDriver) {
    return (
      <div className="flex flex-col items-center gap-4 px-6 py-16 text-center max-w-md mx-auto">
        <div className="flex h-16 w-16 items-center justify-center rounded-full bg-primary/10 text-primary">
          <Smartphone className="h-8 w-8" strokeWidth={1.5} />
        </div>
        <h3 className="text-headline-md text-on-surface">Mobile Access Required</h3>
        <p className="text-body-md text-on-surface-variant">
          Driver accounts do not have access to the web portal. Drivers must use the <strong>FreightLink Mobile App</strong> to view assignments, navigate routes, and upload delivery proofs.
        </p>
        <div className="rounded-lg border border-slate-200 bg-surface-container-low p-4 text-xs text-on-surface-variant w-full text-left space-y-1">
          <p className="font-semibold text-on-surface">Available Platforms:</p>
          <p>• Android (APK / Play Store)</p>
          <p>• iOS (TestFlight / App Store)</p>
        </div>
        <Button
          variant="secondary"
          onClick={() => dispatch(logout())}
          className="mt-2 inline-flex items-center gap-2"
        >
          <LogOut className="h-4 w-4" />
          Log Out
        </Button>
      </div>
    )
  }

  return (
    <div className="flex flex-col items-center gap-3 px-6 py-16 text-center">
      <div className="flex h-12 w-12 items-center justify-center rounded-full bg-status-red-bg">
        <ShieldAlert className="h-6 w-6 text-status-red-text" strokeWidth={1.5} />
      </div>
      <h3 className="text-headline-md text-on-surface">Access Denied</h3>
      <p className="max-w-sm text-body-md text-on-surface-variant">
        Your account doesn't have permission to view this page.
      </p>
      <Button as={Link} to="/loads" variant="secondary" className="mt-2">
        Back to Dashboard
      </Button>
    </div>
  )
}

export default UnauthorizedPage
