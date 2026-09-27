import type { ValidationIssue } from '../../api/types'
import { formatMoney } from '../../lib/format'
import { emptyLine, linesSum, parseAmount, type DraftErrors, type InvoiceDraft, type LineDraft } from '../../lib/draft'

// Same allowance as the backend rule (InvoiceValidator.AmountTolerance).
const tolerance = 0.02

interface LinesEditorProps {
  draft: InvoiceDraft
  onChange: (lines: LineDraft[]) => void
  issues: ValidationIssue[]
  errors: DraftErrors
  readOnly: boolean
}

export function LinesEditor({ draft, onChange, issues, errors, readOnly }: LinesEditorProps) {
  const lines = draft.lines

  const updateLine = (index: number, changes: Partial<LineDraft>) =>
    onChange(lines.map((line, i) => (i === index ? { ...line, ...changes } : line)))

  return (
    <section className="space-y-2">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-slate-800">Line items</h2>
        {!readOnly && (
          <button
            type="button"
            onClick={() => onChange([...lines, emptyLine()])}
            className="text-sm font-medium text-accent-600 hover:text-accent-700"
          >
            + Add line
          </button>
        )}
      </div>

      {issues.length > 0 && (
        <ul className="space-y-0.5 rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-800 ring-1 ring-amber-300">
          {issues.map((issue, i) => (
            <li key={i}>{issue.message}</li>
          ))}
        </ul>
      )}

      <div className={`overflow-x-auto rounded-xl bg-white ${issues.length > 0 ? 'ring-2 ring-amber-300' : 'ring-1 ring-slate-200'}`}>
        <table className="w-full min-w-[34rem] text-sm">
          <thead className="border-b border-slate-200 text-left text-xs font-medium text-slate-500">
            <tr>
              <th className="px-3 py-2">Description</th>
              <th className="w-20 px-2 py-2 text-right">Qty</th>
              <th className="w-28 px-2 py-2 text-right">Unit price</th>
              <th className="w-28 px-2 py-2 text-right">Line total</th>
              {!readOnly && <th className="w-8" aria-label="Actions" />}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {lines.length === 0 && (
              <tr>
                <td colSpan={5} className="px-3 py-6 text-center text-slate-500">
                  No line items.
                </td>
              </tr>
            )}
            {lines.map((line, i) => (
              <tr key={line.key} className="align-top">
                <td className="px-2 py-1.5">
                  <CellInput
                    label={`Line ${i + 1} description`}
                    value={line.description}
                    onChange={(description) => updateLine(i, { description })}
                    error={errors[`lines[${i}].description`]}
                    readOnly={readOnly}
                    multiline
                  />
                </td>
                <td className="px-1 py-1.5">
                  <CellInput
                    label={`Line ${i + 1} quantity`}
                    value={line.qty}
                    onChange={(qty) => updateLine(i, { qty })}
                    error={errors[`lines[${i}].qty`]}
                    readOnly={readOnly}
                    numeric
                  />
                </td>
                <td className="px-1 py-1.5">
                  <CellInput
                    label={`Line ${i + 1} unit price`}
                    value={line.unitPrice}
                    onChange={(unitPrice) => updateLine(i, { unitPrice })}
                    error={errors[`lines[${i}].unitPrice`]}
                    readOnly={readOnly}
                    numeric
                  />
                </td>
                <td className="px-1 py-1.5">
                  <CellInput
                    label={`Line ${i + 1} total`}
                    value={line.lineTotal}
                    onChange={(lineTotal) => updateLine(i, { lineTotal })}
                    error={errors[`lines[${i}].lineTotal`]}
                    readOnly={readOnly}
                    numeric
                  />
                </td>
                {!readOnly && (
                  <td className="py-1.5 pr-2">
                    <button
                      type="button"
                      onClick={() => onChange(lines.filter((_, index) => index !== i))}
                      className="grid size-8 place-items-center rounded-md text-slate-400 hover:bg-slate-100 hover:text-rose-600"
                      aria-label={`Remove line ${i + 1}`}
                      title="Remove line"
                    >
                      ✕
                    </button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <SumHint draft={draft} />
    </section>
  )
}

// Live check while editing, mirroring the backend rule, so the reviewer sees the effect before saving.
function SumHint({ draft }: { draft: InvoiceDraft }) {
  if (draft.lines.length === 0) return null

  const sum = linesSum(draft)
  const net = parseAmount(draft.net)
  const total = parseAmount(draft.total)
  const matches = (value: number | undefined) => value !== undefined && Math.abs(sum - value) <= tolerance
  const currency = draft.currency.trim() || null

  const [tone, text] = matches(net)
    ? ['text-emerald-700', 'matches net']
    : matches(total)
      ? ['text-emerald-700', 'matches total (lines include VAT)']
      : ['text-amber-700', 'matches neither net nor total']

  return (
    <p className="text-right text-xs text-slate-600">
      Lines add up to <span className="tabular font-medium text-slate-900">{formatMoney(sum, currency)}</span>{' '}
      <span className={tone}>· {text}</span>
    </p>
  )
}

interface CellInputProps {
  label: string
  value: string
  onChange: (value: string) => void
  error?: string
  readOnly: boolean
  numeric?: boolean
  multiline?: boolean
}

function CellInput({ label, value, onChange, error, readOnly, numeric = false, multiline = false }: CellInputProps) {
  const className = `w-full rounded-md border px-2 py-1.5 text-sm focus:ring-2 focus:outline-none read-only:cursor-default read-only:border-transparent read-only:bg-transparent ${
    numeric ? 'tabular text-right' : ''
  } ${error ? 'border-rose-400 bg-rose-50/40 focus:ring-rose-500/30' : 'border-slate-200 focus:border-accent-500 focus:ring-accent-500/30'}`

  return (
    <>
      {multiline ? (
        <textarea
          aria-label={label}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          readOnly={readOnly}
          rows={Math.min(4, Math.max(1, Math.ceil(value.length / 48)))}
          className={`${className} resize-y`}
        />
      ) : (
        <input
          aria-label={label}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          readOnly={readOnly}
          inputMode="decimal"
          className={className}
        />
      )}
      {error && <p className="mt-0.5 text-xs text-rose-700">{error}</p>}
    </>
  )
}
