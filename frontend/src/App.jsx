import { RouterProvider } from 'react-router-dom'
import router from './routes/AppRoutes.jsx'

// App.jsx's only job is to mount the router. All route structure (public
// routes, DashboardLayout, ProtectedRoute guards) lives in
// src/routes/AppRoutes.jsx so this file doesn't grow as routes are added.
// Redux's <Provider> wraps this component in main.jsx, one level up.
function App() {
  return <RouterProvider router={router} />
}

export default App
