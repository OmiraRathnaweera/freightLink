import { configureStore } from '@reduxjs/toolkit'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { Provider } from 'react-redux'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import authReducer from '../features/auth/store/authSlice.js'

// Shared render helper for Load Management tests — wires up the same
// Provider/QueryClientProvider/Router nesting main.jsx uses (see
// src/main.jsx, src/store/index.js), scoped down to just the `auth` slice
// since that's the only piece of Redux state any Loads component reads.

/**
 * @param {object} [overrides] - merged into authSlice's initialState.
 */
export function createTestStore(overrides = {}) {
  return configureStore({
    reducer: { auth: authReducer },
    preloadedState: {
      auth: {
        user: null,
        accessToken: null,
        refreshToken: null,
        role: null,
        isAuthenticated: false,
        status: 'idle',
        error: null,
        isBootstrapped: true,
        ...overrides,
      },
    },
  })
}

export function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  })
}

/**
 * Renders `ui` under Redux + TanStack Query + a MemoryRouter.
 *
 * @param {import('react').ReactElement} ui
 * @param {object} [options]
 * @param {object} [options.authState] - overrides for the seeded auth slice (role, isAuthenticated, ...).
 * @param {string} [options.route] - the route path `ui` is mounted at, e.g. '/loads/:loadId/edit'.
 * @param {string[]} [options.initialEntries] - MemoryRouter's initial history, e.g. ['/loads/abc/edit'].
 * @param {import('@tanstack/react-query').QueryClient} [options.queryClient]
 * @param {import('@reduxjs/toolkit').EnhancedStore} [options.store]
 */
export function renderWithProviders(
  ui,
  { authState = {}, route = '/', initialEntries = [route], queryClient = createTestQueryClient(), store = createTestStore(authState) } = {},
) {
  return {
    ...render(
      <QueryClientProvider client={queryClient}>
        <Provider store={store}>
          <MemoryRouter initialEntries={initialEntries}>
            <Routes>
              <Route path={route} element={ui} />
            </Routes>
          </MemoryRouter>
        </Provider>
      </QueryClientProvider>,
    ),
    store,
    queryClient,
  }
}
