import { useSelector } from 'react-redux'

// Plain-JS stand-in for the typed useSelector you'd get in a TypeScript
// project (pre-bound to RootState). Import this instead of react-redux's
// useSelector directly so every component shares one place to add typing
// later without touching call sites.
export const useAppSelector = (selector) => useSelector(selector)
