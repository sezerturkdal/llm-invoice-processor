import { Link, Outlet } from 'react-router'

export function Layout() {
  return (
    <div className="min-h-screen">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex h-14 max-w-7xl items-center gap-3 px-4 sm:px-6">
          <Link to="/" className="flex items-center gap-2 font-semibold tracking-tight text-slate-900">
            <span className="grid size-7 place-items-center rounded-md bg-accent-600 text-sm text-white" aria-hidden>
              IP
            </span>
            Invoice Processor
          </Link>
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 sm:py-8">
        <Outlet />
      </main>
    </div>
  )
}
