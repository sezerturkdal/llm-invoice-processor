import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router'
import { useCurrentUser, useLogout } from '../api/auth'

export function Layout() {
  const { data: user } = useCurrentUser()
  const logout = useLogout()
  const navigate = useNavigate()
  const isAdmin = user?.role === 'Admin'

  return (
    <div className="min-h-screen">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex h-14 max-w-7xl items-center gap-6 px-4 sm:px-6">
          <Link to="/" className="flex items-center gap-2 font-semibold tracking-tight text-slate-900">
            <span className="grid size-7 place-items-center rounded-md bg-accent-600 text-sm text-white" aria-hidden>
              IP
            </span>
            <span className="hidden sm:inline">Invoice Processor</span>
          </Link>
          <nav className="flex gap-1 text-sm font-medium">
            {/* The review screen belongs to the invoices section too. */}
            <NavItem to="/" matches={(path) => path === '/' || path.startsWith('/invoices')}>
              Invoices
            </NavItem>
            {isAdmin && <NavItem to="/usage">Usage</NavItem>}
            {isAdmin && <NavItem to="/users">Users</NavItem>}
          </nav>

          {user && (
            <div className="ml-auto flex items-center gap-3 text-sm">
              <span className="hidden text-right leading-tight md:block">
                <span className="block text-slate-900">{user.email}</span>
                <span className="block text-xs text-slate-500">{user.role}</span>
              </span>
              <button
                type="button"
                onClick={() => logout.mutate(undefined, { onSettled: () => navigate('/login', { replace: true }) })}
                className="rounded-md px-3 py-1.5 font-medium text-slate-600 hover:bg-slate-100 hover:text-slate-900"
              >
                Sign out
              </button>
            </div>
          )}
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 sm:py-8">
        <Outlet />
      </main>
    </div>
  )
}

function NavItem({ to, matches, children }: { to: string; matches?: (path: string) => boolean; children: string }) {
  const { pathname } = useLocation()

  return (
    <NavLink
      to={to}
      className={({ isActive }) => {
        const active = matches ? matches(pathname) : isActive
        return `rounded-md px-3 py-1.5 transition-colors ${
          active ? 'bg-accent-50 text-accent-700' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
        }`
      }}
    >
      {children}
    </NavLink>
  )
}
