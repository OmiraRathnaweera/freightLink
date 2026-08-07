import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Provider } from 'react-redux'
import './index.css'
import App from './App.jsx'
import { store } from './store/index.js'

// Redux is provided at the root because auth/agent-workflow state is
// global. Localized UI state (e.g. SidebarContext) is intentionally NOT
// provided here — it's mounted only around the subtree that needs it, see
// src/layouts/DashboardLayout.jsx.
createRoot(document.getElementById('root')).render(
  <StrictMode>
    <Provider store={store}>
      <App />
    </Provider>
  </StrictMode>,
)
