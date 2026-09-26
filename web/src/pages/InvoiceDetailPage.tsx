import { Link, useParams } from 'react-router'
import { fileUrl, useInvoice } from '../api/invoices'
import type { Invoice } from '../api/types'
import { StatusBadge } from '../components/StatusBadge'
import { formatDate, formatMoney } from '../lib/format'

// Read-only for now; becomes the editable review screen (PDF left, fields right) in the next step.
export function InvoiceDetailPage() {
  const { id = '' } = useParams()
  const { data: invoice, isPending, isError, error } = useInvoice(id)

  return (
    <div className="space-y-4">
      <Link to="/" className="text-sm font-medium text-accent-600 hover:text-accent-700">
        ← All invoices
      </Link>

      {isPending ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : isError ? (
        <p className="text-sm text-rose-700">Could not load the invoice: {error.message}</p>
      ) : (
        <InvoiceDetail invoice={invoice} />
      )}
    </div>
  )
}

function InvoiceDetail({ invoice }: { invoice: Invoice }) {
  const flagged = new Set(invoice.validationIssues.map((i) => i.field))

  return (
    <>
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">{invoice.supplier ?? invoice.fileName}</h1>
        <StatusBadge status={invoice.status} />
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <iframe
          src={fileUrl(invoice.id)}
          title={`Document ${invoice.fileName}`}
          className="h-[75vh] w-full rounded-xl border border-slate-200 bg-white"
        />

        <div className="space-y-6">
          {invoice.status === 'Processing' && (
            <p className="rounded-lg bg-sky-50 px-4 py-3 text-sm text-sky-800">Extracting data… this usually takes a few seconds.</p>
          )}

          {invoice.validationIssues.length > 0 && (
            <ul className="space-y-2 rounded-lg bg-amber-50 px-4 py-3 text-sm text-amber-900">
              {invoice.validationIssues.map((issue, index) => (
                <li key={index}>
                  <span className="font-medium">{issue.field}</span>: {issue.message}
                </li>
              ))}
            </ul>
          )}

          <dl className="grid grid-cols-2 gap-x-6 gap-y-4 rounded-xl border border-slate-200 bg-white p-5 text-sm">
            <Field label="Supplier" value={invoice.supplier} flagged={flagged.has('supplier')} />
            <Field label="Invoice #" value={invoice.invoiceNumber} flagged={flagged.has('invoiceNumber')} />
            <Field label="Date" value={formatDate(invoice.date)} flagged={flagged.has('date')} />
            <Field label="Currency" value={invoice.currency} flagged={flagged.has('currency')} />
            <Field label="Net" value={formatMoney(invoice.net, invoice.currency)} flagged={flagged.has('net')} />
            <Field label="VAT" value={formatMoney(invoice.vat, invoice.currency)} flagged={flagged.has('vat')} />
            <Field label="Total" value={formatMoney(invoice.total, invoice.currency)} flagged={flagged.has('total')} />
            <Field label="Model" value={invoice.modelUsed} />
          </dl>

          {invoice.lines.length > 0 && (
            <table className={`w-full rounded-xl bg-white text-sm ${flagged.has('lines') ? 'ring-2 ring-amber-400' : 'ring-1 ring-slate-200'}`}>
              <thead className="text-left text-xs text-slate-500 uppercase">
                <tr>
                  <th className="px-4 py-2">Description</th>
                  <th className="px-4 py-2 text-right">Qty</th>
                  <th className="px-4 py-2 text-right">Unit price</th>
                  <th className="px-4 py-2 text-right">Line total</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {invoice.lines.map((line) => (
                  <tr key={line.id}>
                    <td className="px-4 py-2">{line.description}</td>
                    <td className="tabular px-4 py-2 text-right">{line.qty}</td>
                    <td className="tabular px-4 py-2 text-right">{formatMoney(line.unitPrice, invoice.currency)}</td>
                    <td className="tabular px-4 py-2 text-right">{formatMoney(line.lineTotal, invoice.currency)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </>
  )
}

function Field({ label, value, flagged = false }: { label: string; value: string | null; flagged?: boolean }) {
  return (
    <div className={flagged ? '-m-2 rounded-md bg-amber-50 p-2 ring-1 ring-amber-400' : ''}>
      <dt className="text-xs font-medium text-slate-500">{label}</dt>
      <dd className="mt-0.5 break-words text-slate-900">{value ?? '—'}</dd>
    </div>
  )
}
