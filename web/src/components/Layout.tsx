import { Link, NavLink, Outlet, useLocation } from 'react-router'

export function Layout() {
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
            <NavItem to="/usage">Usage</NavItem>
          </nav>
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
