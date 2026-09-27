import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, request, sendJson } from './client'
import type { CurrentUser } from './types'

export const currentUserKey = ['auth', 'me'] as const

/** The signed-in user, or null when nobody is. The session itself is an HttpOnly cookie the app never sees. */
export function useCurrentUser() {
  return useQuery({
    queryKey: currentUserKey,
    queryFn: async () => {
      try {
        return await request<CurrentUser>('/api/auth/me')
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null
        throw error
      }
    },
    staleTime: 5 * 60_000,
    // The API may still be starting (or restarting in development); keep trying so the page recovers by itself.
    refetchInterval: (query) => (query.state.status === 'error' ? 3000 : false),
  })
}

export function useLogin() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (credentials: { email: string; password: string }) =>
      sendJson<CurrentUser>('/api/auth/login', 'POST', credentials),
    onSuccess: (user) => {
      // Nothing cached from a previous session (another user's invoices) survives a sign-in.
      queryClient.clear()
      queryClient.setQueryData(currentUserKey, user)
    },
  })
}

export function useLogout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => request<void>('/api/auth/logout', { method: 'POST' }),
    onSettled: () => {
      queryClient.clear()
      queryClient.setQueryData(currentUserKey, null)
    },
  })
}
