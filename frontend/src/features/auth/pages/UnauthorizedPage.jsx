import { Link } from 'react-router-dom'
import { ShieldAlert } from 'lucide-react'
import Button from '../../../components/Button.jsx'

// Reached via ProtectedRoute's role check (src/routes/ProtectedRoute.jsx).
// Same icon-circle/headline/body visual recipe EmptyState/ErrorState use,
// but built bespoke rather than reusing ErrorState directly — its
// `onRetry` slot renders a fixed "Retry" button, the wrong action here
// (.claude/rules/frontend-design.md #2: composition over growing a shared
// component's prop list for one screen).
function UnauthorizedPage() {
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
