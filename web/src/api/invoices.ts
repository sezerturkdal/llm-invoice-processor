import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { request } from './client'
import type { Invoice, InvoiceListFilters, InvoiceListResponse } from './types'

// Extraction runs in the background, so anything still Processing is polled until it settles.
const processingPollMs = 2000

export const invoiceKeys = {
  all: ['invoices'] as const,
  list: (filters: InvoiceListFilters) => [...invoiceKeys.all, 'list', filters] as const,
  detail: (id: string) => [...invoiceKeys.all, 'detail', id] as const,
  suppliers: () => [...invoiceKeys.all, 'suppliers'] as const,
}

export function fileUrl(id: string): string {
  return `/api/invoices/${id}/file`
}

export function useInvoices(filters: InvoiceListFilters) {
  return useQuery({
    queryKey: invoiceKeys.list(filters),
    queryFn: () => request<InvoiceListResponse>(`/api/invoices?${toSearchParams(filters)}`),
    // Keep showing the current page while the next one loads, instead of flashing empty.
    placeholderData: keepPreviousData,
    refetchInterval: (query) =>
      query.state.data?.items.some((i) => i.status === 'Processing') ? processingPollMs : false,
  })
}

export function useInvoice(id: string) {
  return useQuery({
    queryKey: invoiceKeys.detail(id),
    queryFn: () => request<Invoice>(`/api/invoices/${id}`),
    refetchInterval: (query) => (query.state.data?.status === 'Processing' ? processingPollMs : false),
  })
}

export function useSuppliers() {
  return useQuery({
    queryKey: invoiceKeys.suppliers(),
    queryFn: () => request<string[]>('/api/invoices/suppliers'),
    staleTime: 30_000,
  })
}

export function useUploadInvoice() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData()
      form.append('file', file)
      return request<Invoice>('/api/invoices', { method: 'POST', body: form })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: invoiceKeys.all }),
  })
}

function toSearchParams(filters: InvoiceListFilters): string {
  const params = new URLSearchParams({ page: String(filters.page), pageSize: String(filters.pageSize) })
  if (filters.status) params.set('status', filters.status)
  if (filters.supplier) params.set('supplier', filters.supplier)
  return params.toString()
}
