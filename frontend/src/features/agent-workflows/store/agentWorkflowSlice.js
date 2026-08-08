import { createSlice } from '@reduxjs/toolkit'

// Agent-workflow monitoring lives in Redux Toolkit alongside auth: workflow
// status needs to be visible from multiple pages/routes at once (e.g. a
// dashboard summary and a detail page tracking the same run), which is the
// same "shared, cross-cutting" shape as auth. (See ADR: state-management
// strategy.)

const initialState = {
  workflows: [], // tracked workflow runs, shape TBD (e.g. { id, status, ... })
  activeWorkflowId: null,
  status: 'idle', // 'idle' | 'loading' | 'succeeded' | 'failed'
  error: null,
}

const agentWorkflowSlice = createSlice({
  name: 'agentWorkflow',
  initialState,
  reducers: {
    setActiveWorkflow(state, action) {
      state.activeWorkflowId = action.payload
    },
    clearAgentWorkflowError(state) {
      state.error = null
    },
  },
  // TODO: add async thunks (fetchWorkflows, startWorkflow, pollWorkflowStatus)
  // once the agent-workflows API contract is defined.
})

export const { setActiveWorkflow, clearAgentWorkflowError } = agentWorkflowSlice.actions
export default agentWorkflowSlice.reducer
