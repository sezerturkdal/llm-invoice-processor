import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { request } from './client'
import type { ExtractionStatsResponse } from './types'

/** Extraction usage for the last `days` days, or all time when undefined. */
export function useExtractionStats(days: number | undefined) {
  return useQuery({
    queryKey: ['extractions', 'stats', days ?? 'all'] as const,
    queryFn: () => request<ExtractionStatsResponse>(`/api/extractions/stats${days ? `?days=${days}` : ''}`),
    placeholderData: keepPreviousData,
  })
}
