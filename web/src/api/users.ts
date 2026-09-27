import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { request, sendJson } from './client'
import type { Role, UserAccount } from './types'

const usersKey = ['users'] as const

export function useUsers() {
  return useQuery({ queryKey: usersKey, queryFn: () => request<UserAccount[]>('/api/users') })
}

export function useUserActions() {
  const queryClient = useQueryClient()
  const onSuccess = () => queryClient.invalidateQueries({ queryKey: usersKey })

  return {
    create: useMutation({
      mutationFn: (user: { email: string; role: Role; password: string }) => sendJson<UserAccount>('/api/users', 'POST', user),
      onSuccess,
    }),
    update: useMutation({
      mutationFn: ({ id, ...change }: { id: string; role: Role; isActive: boolean }) =>
        sendJson<UserAccount>(`/api/users/${id}`, 'PUT', change),
      onSuccess,
    }),
    resetPassword: useMutation({
      mutationFn: ({ id, password }: { id: string; password: string }) =>
        sendJson<void>(`/api/users/${id}/password`, 'POST', { password }),
    }),
  }
}
