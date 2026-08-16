import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Provider } from 'react-redux'
import { QueryClientProvider } from '@tanstack/react-query'
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
createRoot(document.getElementById('root')).render(
  <StrictMode>
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <App />
      </QueryClientProvider>
    </Provider>
  </StrictMode>,
)
