// Placeholder — public route, no auth required. Real form + login() thunk
// dispatch comes later; see src/features/auth/store/authSlice.js.
function LoginPage() {
  return (
    <div>
      <h1>Login</h1>
      <p>Placeholder page — form not implemented yet.</p>
      {/* TEMPORARY — Tailwind smoke test, safe to remove */}
      <p className="text-sm font-bold text-red-500">Tailwind test: this text should render bold and red.</p>
    </div>
  )
}

export default LoginPage
