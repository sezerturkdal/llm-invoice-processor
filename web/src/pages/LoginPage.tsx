import { useState, type FormEvent } from 'react'
import { Navigate, useNavigate, useSearchParams } from 'react-router'
import { useCurrentUser, useLogin } from '../api/auth'

// Seeded by appsettings.Development.json; shown only in the dev build.
const demoAccounts = [
  { email: 'admin@example.com', password: '1234', role: 'Admin' },
  { email: 'reviewer@example.com', password: '1234', role: 'Reviewer' },
]

const inputClass =
  'w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm placeholder:text-slate-400 focus:border-accent-500 focus:ring-2 focus:ring-accent-500/30 focus:outline-none'

export function LoginPage() {
  const [params] = useSearchParams()
  const next = safeNext(params.get('next'))
  const navigate = useNavigate()
  const { data: user } = useCurrentUser()
  const login = useLogin()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  if (user) return <Navigate to={next} replace />

  const submit = (event: FormEvent) => {
    event.preventDefault()
    login.mutate({ email, password }, { onSuccess: () => navigate(next, { replace: true }) })
  }

  return (
    <div className="grid min-h-screen place-items-center px-4">
      <div className="w-full max-w-sm">
        <div className="mb-6 flex items-center gap-2 font-semibold tracking-tight text-slate-900">
          <span className="grid size-7 place-items-center rounded-md bg-accent-600 text-sm text-white" aria-hidden>
            IP
          </span>
          Invoice Processor
        </div>

        <form onSubmit={submit} className="space-y-4 rounded-xl border border-slate-200 bg-white p-6">
          <h1 className="text-lg font-semibold tracking-tight">Sign in</h1>

          <label className="block space-y-1">
            <span className="text-sm font-medium text-slate-700">Email</span>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="username"
              required
              autoFocus
              className={inputClass}
            />
          </label>

          <label className="block space-y-1">
            <span className="text-sm font-medium text-slate-700">Password</span>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              required
              className={inputClass}
            />
          </label>

          {login.error && (
            <p role="alert" className="text-sm text-rose-700">
              {login.error.message}
            </p>
          )}

          <button
            type="submit"
            disabled={login.isPending}
            className="w-full rounded-lg bg-accent-600 px-4 py-2 text-sm font-medium text-white hover:bg-accent-700 disabled:opacity-60"
          >
            {login.isPending ? 'Signing in…' : 'Sign in'}
          </button>
        </form>

        {import.meta.env.DEV && (
          <div className="mt-4 rounded-xl border border-dashed border-slate-300 p-4 text-xs text-slate-600">
            <p className="mb-2 font-medium">Demo accounts (development only)</p>
            <ul className="space-y-1">
              {demoAccounts.map((account) => (
                <li key={account.email}>
                  <button
                    type="button"
                    onClick={() => {
                      setEmail(account.email)
                      setPassword(account.password)
                    }}
                    className="text-accent-600 hover:text-accent-700 hover:underline"
                  >
                    {account.email}
                  </button>{' '}
                  · {account.role}
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  )
}

// Only same-app paths, so a crafted link cannot bounce the user to another site after signing in.
function safeNext(next: string | null): string {
  return next && next.startsWith('/') && !next.startsWith('//') && !next.startsWith('/\\') ? next : '/'
}
