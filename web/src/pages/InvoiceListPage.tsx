import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { useInvoices, useSuppliers } from '../api/invoices'
import { invoiceStatuses, type InvoiceStatus, type InvoiceSummary } from '../api/types'
import { StatusBadge } from '../components/StatusBadge'
import { UploadDropzone } from '../components/UploadDropzone'
import { formatDate, formatMoney, formatRelative } from '../lib/format'
import { statusLabels } from '../lib/status'

const pageSize = 20

export function InvoiceListPage() {
  // Filters live in the URL, so a filtered view survives a reload and can be shared.
  const [params, setParams] = useSearchParams()
  const status = parseStatus(params.get('status'))
  const supplier = params.get('supplier') ?? ''
  const page = Math.max(1, Number(params.get('page')) || 1)

  const { data, isPending, isError, error, isPlaceholderData } = useInvoices({
    status,
    supplier: supplier || undefined,
    page,
    pageSize,
  })

  // Stable, so the debounced supplier filter doesn't restart its timer on every render (e.g. while polling).
  const setFilter = useCallback(
    (key: 'status' | 'supplier' | 'page', value: string | undefined) =>
      setParams(
        (current) => {
          const next = new URLSearchParams(current)
          if (value) next.set(key, value)
          else next.delete(key)
          if (key !== 'page') next.delete('page')
          return next
        },
        { replace: true },
      ),
    [setParams],
  )
  const setSupplier = useCallback((s: string | undefined) => setFilter('supplier', s), [setFilter])

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Invoices</h1>
        <p className="mt-1 text-sm text-slate-600">
          Upload invoices; Claude extracts the data, validation flags anything that doesn't add up, and you review it.
        </p>
      </div>

      <UploadDropzone />

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <StatusTabs value={status} onChange={(s) => setFilter('status', s)} />
        <SupplierFilter value={supplier} onChange={setSupplier} />
      </div>

      <div className={`overflow-hidden rounded-xl border border-slate-200 bg-white transition-opacity ${isPlaceholderData ? 'opacity-60' : ''}`}>
        {isPending ? (
          <TableMessage>Loading invoices…</TableMessage>
        ) : isError ? (
          <TableMessage tone="error">Could not load invoices: {error.message}</TableMessage>
        ) : data.items.length === 0 ? (
          <TableMessage>{status || supplier ? 'No invoices match these filters.' : 'No invoices yet. Upload one above to get started.'}</TableMessage>
        ) : (
          <>
            <InvoiceTable items={data.items} />
            <Pagination
              page={data.page}
              pageSize={data.pageSize}
              totalCount={data.totalCount}
              onPageChange={(p) => setFilter('page', p === 1 ? undefined : String(p))}
            />
          </>
        )}
      </div>
    </div>
  )
}

function InvoiceTable({ items }: { items: InvoiceSummary[] }) {
  const navigate = useNavigate()

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead className="border-b border-slate-200 bg-slate-50 text-xs font-medium text-slate-500 uppercase tracking-wide">
          <tr>
            <th className="px-4 py-2.5">Supplier</th>
            <th className="px-4 py-2.5">Invoice #</th>
            <th className="px-4 py-2.5">Date</th>
            <th className="px-4 py-2.5 text-right">Total</th>
            <th className="px-4 py-2.5">Status</th>
            <th className="px-4 py-2.5">Uploaded</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {items.map((invoice) => (
            <tr
              key={invoice.id}
              onClick={() => navigate(`/invoices/${invoice.id}`)}
              className="cursor-pointer hover:bg-slate-50"
            >
              <td className="max-w-72 px-4 py-3">
                {/* Until a supplier is known (processing, failed), the file name is all there is to go on. */}
                {invoice.supplier ? (
                  <>
                    <div className="truncate font-medium text-slate-900" title={invoice.supplier}>
                      {invoice.supplier}
                    </div>
                    <div className="truncate text-xs text-slate-500" title={invoice.fileName}>
                      {invoice.fileName}
                    </div>
                  </>
                ) : (
                  <div className="truncate text-slate-600" title={invoice.fileName}>
                    {invoice.fileName}
                  </div>
                )}
              </td>
              <td className="px-4 py-3 text-slate-700">
                {invoice.invoiceNumber ? <span className="font-mono text-xs">{invoice.invoiceNumber}</span> : '—'}
              </td>
              <td className="px-4 py-3 whitespace-nowrap text-slate-700">{formatDate(invoice.date)}</td>
              <td className="tabular px-4 py-3 text-right whitespace-nowrap text-slate-900">
                {formatMoney(invoice.total, invoice.currency)}
              </td>
              <td className="px-4 py-3">
                <div className="flex items-center gap-2">
                  <StatusBadge status={invoice.status} />
                  {invoice.issueCount > 0 && invoice.status === 'PendingReview' && (
                    <span className="text-xs font-medium text-amber-700" title="Validation issues to review">
                      {invoice.issueCount} {invoice.issueCount === 1 ? 'issue' : 'issues'}
                    </span>
                  )}
                </div>
              </td>
              <td className="px-4 py-3 whitespace-nowrap text-slate-500" title={new Date(invoice.createdAt).toLocaleString()}>
                {formatRelative(invoice.createdAt)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function StatusTabs({ value, onChange }: { value: InvoiceStatus | undefined; onChange: (status: InvoiceStatus | undefined) => void }) {
  const options: (InvoiceStatus | undefined)[] = [undefined, ...invoiceStatuses]

  return (
    <div className="flex flex-wrap gap-1 rounded-lg bg-slate-200/60 p-1" role="tablist" aria-label="Filter by status">
      {options.map((option) => {
        const selected = option === value
        return (
          <button
            key={option ?? 'all'}
            type="button"
            role="tab"
            aria-selected={selected}
            onClick={() => onChange(option)}
            className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
              selected ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            {option ? statusLabels[option] : 'All'}
          </button>
        )
      })}
    </div>
  )
}

// Typing is debounced so the list isn't refetched on every keystroke.
function SupplierFilter({ value, onChange }: { value: string; onChange: (supplier: string | undefined) => void }) {
  const [text, setText] = useState(value)
  const [syncedValue, setSyncedValue] = useState(value)
  const { data: suppliers } = useSuppliers()

  // The URL changed from outside (back button, a link): show that value instead of the typed text.
  if (value !== syncedValue) {
    setSyncedValue(value)
    setText(value)
  }

  useEffect(() => {
    if (text.trim() === value) return
    const timer = setTimeout(() => onChange(text.trim() || undefined), 300)
    return () => clearTimeout(timer)
  }, [text, value, onChange])

  return (
    <div className="relative sm:w-72">
      <input
        type="search"
        value={text}
        onChange={(e) => setText(e.target.value)}
        placeholder="Filter by supplier"
        list="supplier-options"
        aria-label="Filter by supplier"
        className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm placeholder:text-slate-400 focus:border-accent-500 focus:ring-2 focus:ring-accent-500/30 focus:outline-none"
      />
      <datalist id="supplier-options">
        {suppliers?.map((s) => <option key={s} value={s} />)}
      </datalist>
    </div>
  )
}

function Pagination({
  page,
  pageSize,
  totalCount,
  onPageChange,
}: {
  page: number
  pageSize: number
  totalCount: number
  onPageChange: (page: number) => void
}) {
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize))
  const first = (page - 1) * pageSize + 1
  const last = Math.min(page * pageSize, totalCount)

  return (
    <div className="flex items-center justify-between border-t border-slate-200 px-4 py-3 text-sm text-slate-600">
      <span className="tabular">
        {first}–{last} of {totalCount}
      </span>
      {pageCount > 1 && (
        <div className="flex gap-2">
          <PageButton disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
            Previous
          </PageButton>
          <PageButton disabled={page >= pageCount} onClick={() => onPageChange(page + 1)}>
            Next
          </PageButton>
        </div>
      )}
    </div>
  )
}

function PageButton({ disabled, onClick, children }: { disabled: boolean; onClick: () => void; children: string }) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className="rounded-md border border-slate-300 bg-white px-3 py-1 font-medium text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
    >
      {children}
    </button>
  )
}

function TableMessage({ children, tone }: { children: ReactNode; tone?: 'error' }) {
  return <p className={`px-4 py-12 text-center text-sm ${tone === 'error' ? 'text-rose-700' : 'text-slate-500'}`}>{children}</p>
}

function parseStatus(value: string | null): InvoiceStatus | undefined {
  return invoiceStatuses.find((s) => s === value)
}
