import { QueryClient } from '@tanstack/react-query'

/**
 * App-wide TanStack Query client. TanStack Query owns server state (lists,
 * details, mutations); Redux Toolkit keeps owning client/session state
 * (auth) — see src/store/index.js's own comment on that split.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000, // dashboard data doesn't need refetching on every render
      refetchOnWindowFocus: true, // ops dashboard — refresh load/assignment state when the tab regains focus
      retry: (failureCount, error) =>
        error?.status >= 400 && error.status < 500 ? false : failureCount < 2, // don't retry deterministic 4xx
    },
    mutations: {
      retry: false, // never silently re-send a POST/PUT/PATCH that may have side effects
    },
  },
})
