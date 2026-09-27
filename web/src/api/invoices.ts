import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { request, sendJson } from './client'
import type { Invoice, InvoiceListFilters, InvoiceListResponse, InvoiceUpdate } from './types'

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

/**
 * Review actions. Each returns the updated invoice, which replaces the cached detail directly
 * (so validation flags update without a refetch); lists are refetched.
 */
export function useReviewActions(id: string) {
  const queryClient = useQueryClient()

  const onSuccess = (invoice: Invoice) => {
    queryClient.setQueryData(invoiceKeys.detail(id), invoice)
    return queryClient.invalidateQueries({ queryKey: invoiceKeys.all, predicate: (q) => q.queryKey[1] !== 'detail' })
  }

  return {
    save: useMutation({
      mutationFn: (update: InvoiceUpdate) => sendJson<Invoice>(`/api/invoices/${id}`, 'PUT', update),
      onSuccess,
    }),
    approve: useMutation({ mutationFn: () => sendJson<Invoice>(`/api/invoices/${id}/approve`, 'POST'), onSuccess }),
    reject: useMutation({ mutationFn: () => sendJson<Invoice>(`/api/invoices/${id}/reject`, 'POST'), onSuccess }),
    reextract: useMutation({ mutationFn: () => sendJson<Invoice>(`/api/invoices/${id}/extract`, 'POST'), onSuccess }),
  }
}

/** The most recently uploaded other invoice still waiting for review, to move on to after finishing one. */
export async function findNextPending(currentId: string): Promise<string | undefined> {
  const list = await request<InvoiceListResponse>('/api/invoices?status=PendingReview&page=1&pageSize=2')
  return list.items.find((i) => i.id !== currentId)?.id
}

function toSearchParams(filters: InvoiceListFilters): string {
  const params = new URLSearchParams({ page: String(filters.page), pageSize: String(filters.pageSize) })
  if (filters.status) params.set('status', filters.status)
  if (filters.supplier) params.set('supplier', filters.supplier)
  return params.toString()
}
