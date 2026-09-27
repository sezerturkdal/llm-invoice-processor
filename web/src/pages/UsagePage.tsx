import type { ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import { useExtractionStats } from '../api/extractions'
import type { ExtractionStats } from '../api/types'
import { formatCount, formatDuration, formatUsd } from '../lib/format'

const periods = [
  { value: '7', label: '7 days' },
  { value: '30', label: '30 days' },
  { value: 'all', label: 'All time' },
] as const

type Period = (typeof periods)[number]['value']

const defaultPeriod: Period = '30'

/** Token, cost and latency per extraction, overall and per model. */
export function UsagePage() {
  const [params, setParams] = useSearchParams()
  const period = periods.find((p) => p.value === params.get('period'))?.value ?? defaultPeriod
  const { data, isPending, isError, error, isPlaceholderData } = useExtractionStats(period === 'all' ? undefined : Number(period))

  const setPeriod = (value: Period) =>
    setParams(value === defaultPeriod ? {} : { period: value }, { replace: true })

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Usage</h1>
          <p className="mt-1 text-sm text-slate-600">
            What extraction costs and how long it takes. Costs are estimates from token counts and the per-model prices in
            configuration.
          </p>
        </div>
        <PeriodTabs value={period} onChange={setPeriod} />
      </div>

      {isPending ? (
        <p className="text-sm text-slate-500">Loading usage…</p>
      ) : isError ? (
        <p className="text-sm text-rose-700">Could not load usage: {error.message}</p>
      ) : data.totals.extractions === 0 ? (
        <p className="rounded-xl border border-slate-200 bg-white px-4 py-12 text-center text-sm text-slate-500">
          No extractions in this period.
        </p>
      ) : (
        <div className={`space-y-6 transition-opacity ${isPlaceholderData ? 'opacity-60' : ''}`}>
          <Totals stats={data.totals} />
          <ModelTable models={data.models} />
        </div>
      )}
    </div>
  )
}

function Totals({ stats }: { stats: ExtractionStats }) {
  return (
    <dl className="grid grid-cols-2 gap-3 lg:grid-cols-4">
      <Stat label="Extractions" value={formatCount(stats.extractions)}>
        {stats.failures > 0 ? (
          <span className="text-rose-700">{formatCount(stats.failures)} failed</span>
        ) : (
          'none failed'
        )}
      </Stat>
      <Stat label="Total cost" value={formatUsd(stats.totalCost)}>
        {formatUsd(stats.averageCost)} per invoice
      </Stat>
      <Stat label="Average latency" value={formatDuration(stats.averageLatencyMs)}>
        slowest {formatDuration(stats.maxLatencyMs)}
      </Stat>
      <Stat label="Tokens" value={formatCount(stats.inputTokens + stats.outputTokens)}>
        {formatCount(stats.inputTokens)} in · {formatCount(stats.outputTokens)} out
      </Stat>
    </dl>
  )
}

function Stat({ label, value, children }: { label: string; value: string; children: ReactNode }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
      <dt className="text-xs font-medium tracking-wide text-slate-500 uppercase">{label}</dt>
      <dd className="tabular mt-1 text-2xl font-semibold tracking-tight text-slate-900">{value}</dd>
      <dd className="tabular mt-0.5 text-xs text-slate-500">{children}</dd>
    </div>
  )
}

function ModelTable({ models }: { models: { provider: string; model: string; stats: ExtractionStats }[] }) {
  return (
    <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs font-medium tracking-wide text-slate-500 uppercase">
            <tr>
              <th className="px-4 py-2.5">Model</th>
              <th className="px-4 py-2.5 text-right">Extractions</th>
              <th className="px-4 py-2.5 text-right">Failed</th>
              <th className="px-4 py-2.5 text-right">Avg latency</th>
              <th className="px-4 py-2.5 text-right">Tokens in / out</th>
              <th className="px-4 py-2.5 text-right">Per invoice</th>
              <th className="px-4 py-2.5 text-right">Total cost</th>
            </tr>
          </thead>
          <tbody className="tabular divide-y divide-slate-100">
            {models.map(({ provider, model, stats }) => (
              <tr key={`${provider}/${model}`}>
                <td className="px-4 py-3">
                  <div className="font-medium text-slate-900">{model}</div>
                  <div className="text-xs text-slate-500">{provider}</div>
                </td>
                <td className="px-4 py-3 text-right text-slate-700">{formatCount(stats.extractions)}</td>
                <td className={`px-4 py-3 text-right ${stats.failures > 0 ? 'text-rose-700' : 'text-slate-400'}`}>
                  {formatCount(stats.failures)}
                </td>
                <td className="px-4 py-3 text-right whitespace-nowrap text-slate-700">{formatDuration(stats.averageLatencyMs)}</td>
                <td className="px-4 py-3 text-right whitespace-nowrap text-slate-700">
                  {formatCount(stats.inputTokens)} / {formatCount(stats.outputTokens)}
                </td>
                <td className="px-4 py-3 text-right whitespace-nowrap text-slate-700">{formatUsd(stats.averageCost)}</td>
                <td className="px-4 py-3 text-right whitespace-nowrap font-medium text-slate-900">{formatUsd(stats.totalCost)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function PeriodTabs({ value, onChange }: { value: Period; onChange: (period: Period) => void }) {
  return (
    <div className="flex gap-1 self-start rounded-lg bg-slate-200/60 p-1" role="tablist" aria-label="Period">
      {periods.map((p) => (
        <button
          key={p.value}
          type="button"
          role="tab"
          aria-selected={p.value === value}
          onClick={() => onChange(p.value)}
          className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
            p.value === value ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-600 hover:text-slate-900'
          }`}
        >
          {p.label}
        </button>
      ))}
    </div>
  )
}
