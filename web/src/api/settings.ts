import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { request, sendJson } from './client'
import type { ExtractionSettings } from './types'

const extractionSettingsKey = ['settings', 'extraction'] as const

export function useExtractionSettings() {
  return useQuery({
    queryKey: extractionSettingsKey,
    queryFn: () => request<ExtractionSettings>('/api/settings/extraction'),
  })
}

export function useSelectModel() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (choice: { provider: string; model: string }) =>
      sendJson<ExtractionSettings>('/api/settings/extraction', 'PUT', choice),
    onSuccess: (settings) => queryClient.setQueryData(extractionSettingsKey, settings),
  })
}
