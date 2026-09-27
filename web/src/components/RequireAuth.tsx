import type { ReactNode } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router'
import { useCurrentUser } from '../api/auth'
import { ApiError } from '../api/client'
import type { Role } from '../api/types'

/** Sends signed-out users to the login page, remembering where they were going. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { data: user, isPending, isError, error } = useCurrentUser()
  const location = useLocation()

  if (isPending) return null
  if (isError) {
    return (
      <div className="grid min-h-screen place-items-center px-4 text-center text-sm">
        <div>
          <p className="font-medium text-slate-900">Waiting for the API…</p>
          <p className="mt-1 text-slate-600">
            {error instanceof ApiError && error.status === 502
              ? 'It is not running. Start it with: dotnet run --project src/InvoiceProcessor.Api --launch-profile http'
              : error.message}
          </p>
          <p className="mt-1 text-xs text-slate-500">This page retries every few seconds.</p>
        </div>
      </div>
    )
  }

  if (!user) {
    const next = location.pathname + location.search
    return <Navigate to={next === '/' ? '/login' : `/login?next=${encodeURIComponent(next)}`} replace />
  }

  return children
}

/**
 * Hides a section from users without the role. The API enforces the same rule;
 * this only spares them a page of 403 errors.
 */
export function RequireRole({ role }: { role: Role }) {
  const { data: user } = useCurrentUser()

  if (user?.role !== role) {
    return (
      <div className="rounded-xl border border-slate-200 bg-white px-4 py-12 text-center text-sm text-slate-600">
        This page is for {role.toLowerCase()}s only.
      </div>
    )
  }

  return <Outlet />
}
