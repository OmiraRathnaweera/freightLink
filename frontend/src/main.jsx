import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Provider } from 'react-redux'
import { QueryClientProvider } from '@tanstack/react-query'
import { Toaster } from 'sonner'
import './index.css'
import App from './App.jsx'
import { store } from './store/index.js'
import { queryClient } from './lib/api/queryClient.js'

// Redux is provided at the root because auth/agent-workflow state is
// global. Localized UI state (e.g. SidebarContext) is intentionally NOT
// provided here — it's mounted only around the subtree that needs it, see
// src/layouts/DashboardLayout.jsx.
//
// QueryClientProvider sits alongside Redux, not inside a feature — server
// state (TanStack Query) and client/session state (Redux) are two
// independent root-level providers, per src/lib/api/queryClient.js's comment.
//
// <Toaster/> is mounted once here (not per-feature) so any mutation
// anywhere in the app can call sonner's `toast()` — it renders into its
// own portal, styled with the existing DESIGN.md tokens instead of
// sonner's defaults.
createRoot(document.getElementById('root')).render(
  <StrictMode>
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <App />
        <Toaster
          position="top-right"
          toastOptions={{
            classNames: {
              toast: 'rounded-md! border! border-slate-border! bg-surface-container-lowest! shadow-soft!',
              title: 'text-body-md! text-on-surface!',
              success: 'border-status-green-text!',
              error: 'border-status-red-text!',
            },
          }}
        />
      </QueryClientProvider>
    </Provider>
  </StrictMode>,
)
