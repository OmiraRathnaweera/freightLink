import { configureStore } from '@reduxjs/toolkit'
import authReducer from '../features/auth/store/authSlice.js'
import agentWorkflowReducer from '../features/agent-workflows/store/agentWorkflowSlice.js'

// Redux Toolkit owns state that is shared across features or must survive
// route changes (auth session, agent-workflow monitoring). State scoped to a
// single small component tree belongs in Context instead — see
// src/context/SidebarContext.jsx. (See ADR: state-management strategy.)
export const store = configureStore({
  reducer: {
    auth: authReducer,
    agentWorkflow: agentWorkflowReducer,
  },
})

export default store
