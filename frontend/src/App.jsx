import { useEffect } from 'react'
import { RouterProvider } from 'react-router-dom'
import { Loader2 } from 'lucide-react'
import router from './routes/AppRoutes.jsx'
import { useAppDispatch } from './hooks/useAppDispatch.js'
import { useAppSelector } from './hooks/useAppSelector.js'
import { bootstrapAuth } from './features/auth/store/authSlice.js'

// App.jsx's job: bootstrap the auth session from a persisted refresh token
// (if any) before rendering the router at all, so route guards never make
// a decision against a stale/unknown auth state. All route structure
// (public routes, DashboardLayout, ProtectedRoute guards) lives in
// src/routes/AppRoutes.jsx. Redux's <Provider> wraps this component in
// main.jsx, one level up.
function App() {
  const dispatch = useAppDispatch()
  const isBootstrapped = useAppSelector((state) => state.auth.isBootstrapped)

  useEffect(() => {
    dispatch(bootstrapAuth())
  }, [dispatch])

  if (!isBootstrapped) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <div className="flex flex-col items-center gap-3">
          <Loader2 className="h-6 w-6 animate-spin text-primary" strokeWidth={1.5} />
          <p className="text-body-md text-on-surface-variant">Loading FreightLink…</p>
        </div>
      </div>
    )
  }

  return <RouterProvider router={router} />
}

export default App
