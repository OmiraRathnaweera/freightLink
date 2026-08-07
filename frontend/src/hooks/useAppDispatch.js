import { useDispatch } from 'react-redux'

// Plain-JS stand-in for the typed useDispatch you'd get in a TypeScript
// project (pre-bound to AppDispatch). Import this instead of react-redux's
// useDispatch directly so every component shares one place to add typing
// later without touching call sites.
export const useAppDispatch = () => useDispatch()
