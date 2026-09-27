import { useState, type FormEvent } from 'react'
import { useCurrentUser } from '../api/auth'
import { roles, type Role, type UserAccount } from '../api/types'
import { useUserActions, useUsers } from '../api/users'

const inputClass =
  'rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm placeholder:text-slate-400 focus:border-accent-500 focus:ring-2 focus:ring-accent-500/30 focus:outline-none'

const primaryButton =
  'rounded-lg bg-accent-600 px-4 py-2 text-sm font-medium text-white hover:bg-accent-700 disabled:opacity-60'

const secondaryButton =
  'rounded-md border border-slate-300 bg-white px-3 py-1 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-40'

/** Admins create accounts and manage roles; there is no self-registration. */
export function UsersPage() {
  const { data: users, isPending, isError, error } = useUsers()

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Users</h1>
        <p className="mt-1 text-sm text-slate-600">
          Reviewers upload and review invoices. Admins also see usage and costs and manage users.
        </p>
      </div>

      <CreateUserForm />

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
        {isPending ? (
          <p className="px-4 py-12 text-center text-sm text-slate-500">Loading users…</p>
        ) : isError ? (
          <p className="px-4 py-12 text-center text-sm text-rose-700">Could not load users: {error.message}</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-slate-200 bg-slate-50 text-xs font-medium tracking-wide text-slate-500 uppercase">
                <tr>
                  <th className="px-4 py-2.5">Email</th>
                  <th className="px-4 py-2.5">Role</th>
                  <th className="px-4 py-2.5">Status</th>
                  <th className="px-4 py-2.5 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {users.map((user) => (
                  <UserRow key={user.id} user={user} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}

function CreateUserForm() {
  const { create } = useUserActions()
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<Role>('Reviewer')
  const [password, setPassword] = useState('')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    create.mutate(
      { email, role, password },
      {
        onSuccess: () => {
          setEmail('')
          setPassword('')
        },
      },
    )
  }

  return (
    <form onSubmit={submit} className="rounded-xl border border-slate-200 bg-white p-4">
      <h2 className="mb-3 text-sm font-semibold text-slate-900">Add a user</h2>
      <div className="flex flex-col gap-2 sm:flex-row">
        <input
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="Email"
          aria-label="Email"
          required
          className={`${inputClass} sm:flex-1`}
        />
        <select value={role} onChange={(e) => setRole(e.target.value as Role)} aria-label="Role" className={inputClass}>
          {roles.map((r) => (
            <option key={r}>{r}</option>
          ))}
        </select>
        <input
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="Password"
          aria-label="Password"
          autoComplete="new-password"
          required
          className={`${inputClass} sm:w-60`}
        />
        <button type="submit" disabled={create.isPending} className={primaryButton}>
          Add
        </button>
      </div>
      {create.error && <p className="mt-2 text-sm text-rose-700">{create.error.message}</p>}
    </form>
  )
}

function UserRow({ user }: { user: UserAccount }) {
  const { data: currentUser } = useCurrentUser()
  const { update, resetPassword } = useUserActions()
  const [settingPassword, setSettingPassword] = useState(false)
  const [password, setPassword] = useState('')

  // The API refuses these for your own account too; disabling the controls says so up front.
  const isSelf = currentUser?.email === user.email
  const error = update.error ?? resetPassword.error

  const submitPassword = (event: FormEvent) => {
    event.preventDefault()
    resetPassword.mutate(
      { id: user.id, password },
      {
        onSuccess: () => {
          setPassword('')
          setSettingPassword(false)
        },
      },
    )
  }

  return (
    <tr className={user.isActive ? '' : 'bg-slate-50/60'}>
      <td className="px-4 py-3">
        <span className={user.isActive ? 'text-slate-900' : 'text-slate-400'}>{user.email}</span>
        {isSelf && <span className="ml-2 text-xs text-slate-500">(you)</span>}
        {error && <p className="mt-1 text-xs text-rose-700">{error.message}</p>}
        {resetPassword.isSuccess && !settingPassword && <p className="mt-1 text-xs text-emerald-700">Password changed.</p>}
      </td>
      <td className="px-4 py-3">
        <select
          value={user.role ?? ''}
          disabled={isSelf || update.isPending}
          onChange={(e) => update.mutate({ id: user.id, role: e.target.value as Role, isActive: user.isActive })}
          aria-label={`Role of ${user.email}`}
          className="rounded-md border border-slate-300 bg-white px-2 py-1 text-sm disabled:opacity-60"
        >
          {user.role === null && <option value="">No role</option>}
          {roles.map((r) => (
            <option key={r}>{r}</option>
          ))}
        </select>
      </td>
      <td className="px-4 py-3">
        {user.isActive ? (
          <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700">Active</span>
        ) : (
          <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-600">Disabled</span>
        )}
      </td>
      <td className="px-4 py-3">
        {settingPassword ? (
          <form onSubmit={submitPassword} className="flex justify-end gap-2">
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="New password"
              aria-label={`New password for ${user.email}`}
              autoComplete="new-password"
              required
              autoFocus
              className="w-44 rounded-md border border-slate-300 px-2 py-1 text-sm"
            />
            <button type="submit" disabled={resetPassword.isPending} className={secondaryButton}>
              Save
            </button>
            <button type="button" onClick={() => setSettingPassword(false)} className={secondaryButton}>
              Cancel
            </button>
          </form>
        ) : (
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setSettingPassword(true)} className={secondaryButton}>
              Set password
            </button>
            <button
              type="button"
              disabled={isSelf || update.isPending || user.role === null}
              onClick={() => user.role && update.mutate({ id: user.id, role: user.role, isActive: !user.isActive })}
              className={secondaryButton}
            >
              {user.isActive ? 'Disable' : 'Enable'}
            </button>
          </div>
        )}
      </td>
    </tr>
  )
}
