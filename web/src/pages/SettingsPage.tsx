import { useState, type FormEvent } from 'react'
import { useExtractionSettings, useSelectModel } from '../api/settings'
import type { ExtractionSettings, ModelChoice, ProviderChoice } from '../api/types'
import { formatRelative } from '../lib/format'

const primaryButton =
  'rounded-lg bg-accent-600 px-4 py-2 text-sm font-medium text-white hover:bg-accent-700 disabled:opacity-50'

const secondaryButton =
  'rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50'

const providerInfo: Record<string, { title: string; description: string }> = {
  Anthropic: { title: 'Claude', description: 'Anthropic API. Documents are sent to Anthropic for extraction.' },
  Ollama: { title: 'Local model', description: 'Runs through Ollama on your own machine. Documents never leave it.' },
}

/** Admins pick the extraction model among what the server configuration allows; keys and endpoints stay in configuration. */
export function SettingsPage() {
  const { data: settings, isPending, isError, error, refetch, isFetching } = useExtractionSettings()

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Settings</h1>
        <p className="mt-1 text-sm text-slate-600">
          The model that reads uploaded invoices. A change applies to the next upload; invoices already read keep the model
          they were read with.
        </p>
      </div>

      {isPending ? (
        <p className="rounded-xl border border-slate-200 bg-white px-4 py-12 text-center text-sm text-slate-500">
          Loading settings…
        </p>
      ) : isError ? (
        <p className="rounded-xl border border-slate-200 bg-white px-4 py-12 text-center text-sm text-rose-700">
          Could not load settings: {error.message}
        </p>
      ) : (
        <>
          <ModelForm
            settings={settings}
            onRecheck={() => refetch()}
            rechecking={isFetching}
          />
          <History settings={settings} />
        </>
      )}
    </div>
  )
}

function ModelForm({
  settings,
  onRecheck,
  rechecking,
}: {
  settings: ExtractionSettings
  onRecheck: () => void
  rechecking: boolean
}) {
  const select = useSelectModel()
  const { selected } = settings
  const [provider, setProvider] = useState(selected.provider)
  const [models, setModels] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      settings.providers.map((p) => [
        p.provider,
        p.provider === selected.provider ? selected.model : (p.models[0]?.id ?? ''),
      ]),
    ),
  )

  const model = models[provider] ?? ''
  const choice = settings.providers.find((p) => p.provider === provider)?.models.find((m) => m.id === model)
  const changed = provider !== selected.provider || model !== selected.model

  const submit = (event: FormEvent) => {
    event.preventDefault()
    select.mutate({ provider, model })
  }

  return (
    <form onSubmit={submit} className="rounded-xl border border-slate-200 bg-white">
      <div className="border-b border-slate-200 px-4 py-3">
        <h2 className="text-sm font-semibold text-slate-900">Extraction model</h2>
        <p className="mt-0.5 text-sm text-slate-600">
          In use: <span className="font-medium text-slate-900">{selected.model}</span> ({providerInfo[selected.provider]?.title ?? selected.provider})
          {selected.isDefault ? (
            <span className="text-slate-500"> · server default</span>
          ) : (
            selected.changedBy &&
            selected.changedAt && (
              <span className="text-slate-500">
                {' '}
                · chosen by {selected.changedBy}, {formatRelative(selected.changedAt)}
              </span>
            )
          )}
        </p>
      </div>

      <fieldset className="grid gap-3 p-4 md:grid-cols-2">
        <legend className="sr-only">Provider</legend>
        {settings.providers.map((p) => (
          <ProviderOption
            key={p.provider}
            choice={p}
            checked={provider === p.provider}
            model={models[p.provider] ?? ''}
            onSelect={() => setProvider(p.provider)}
            onModelChange={(id) => {
              setModels((current) => ({ ...current, [p.provider]: id }))
              setProvider(p.provider)
            }}
          />
        ))}
      </fieldset>

      {choice && !choice.supportsImages && (
        <p className="mx-4 mb-4 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          {choice.id} reads text only. It can extract digital PDFs, but photos and scanned PDFs will fail. Pick a model
          that supports images to handle every upload.
        </p>
      )}

      <div className="flex flex-wrap items-center gap-3 border-t border-slate-200 px-4 py-3">
        <button type="submit" disabled={!changed || !model || select.isPending} className={primaryButton}>
          {select.isPending ? 'Saving…' : 'Save'}
        </button>
        <button type="button" onClick={onRecheck} disabled={rechecking} className={secondaryButton}>
          {rechecking ? 'Checking…' : 'Check again'}
        </button>
        {select.error && <p className="text-sm text-rose-700">{select.error.message}</p>}
        {select.isSuccess && !changed && <p className="text-sm text-emerald-700">Saved. The next upload uses {selected.model}.</p>}
      </div>
    </form>
  )
}

function ProviderOption({
  choice,
  checked,
  model,
  onSelect,
  onModelChange,
}: {
  choice: ProviderChoice
  checked: boolean
  model: string
  onSelect: () => void
  onModelChange: (id: string) => void
}) {
  const info = providerInfo[choice.provider] ?? { title: choice.provider, description: '' }
  const disabled = !choice.isSelectable
  const id = `provider-${choice.provider}`

  return (
    <div
      className={`rounded-lg border p-4 transition-colors ${
        checked && !disabled ? 'border-accent-500 bg-accent-50/40 ring-1 ring-accent-500' : 'border-slate-200'
      } ${disabled ? 'bg-slate-50' : ''}`}
    >
      <label htmlFor={id} className={`flex items-start gap-3 ${disabled ? 'cursor-not-allowed' : 'cursor-pointer'}`}>
        <input
          id={id}
          type="radio"
          name="provider"
          checked={checked}
          disabled={disabled}
          onChange={onSelect}
          className="mt-1 accent-accent-600"
        />
        <span>
          <span className={`block text-sm font-semibold ${disabled ? 'text-slate-500' : 'text-slate-900'}`}>{info.title}</span>
          <span className="block text-sm text-slate-600">{info.description}</span>
        </span>
      </label>

      <div className="mt-3 pl-7">
        {!choice.allowed ? (
          <p className="text-sm text-slate-500">Turned off in the server configuration.</p>
        ) : choice.unavailable ? (
          <p className="text-sm text-amber-700">{choice.unavailable}</p>
        ) : (
          <select
            value={model}
            onChange={(e) => onModelChange(e.target.value)}
            aria-label={`${info.title} model`}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm focus:border-accent-500 focus:ring-2 focus:ring-accent-500/30 focus:outline-none"
          >
            {choice.models.map((m) => (
              <option key={m.id} value={m.id}>
                {describe(m)}
              </option>
            ))}
          </select>
        )}
      </div>
    </div>
  )
}

/** "claude-sonnet-5 · $2 / $10 per M tokens", "qwen3-vl:8b · 8.8B", "gpt-oss:20b · 20.9B · text PDFs only". */
function describe(model: ModelChoice): string {
  const parts = [model.id]
  if (model.inputPerMillion !== null && model.outputPerMillion !== null) {
    parts.push(`$${model.inputPerMillion} / $${model.outputPerMillion} per M tokens`)
  }
  if (model.size) parts.push(model.size)
  if (!model.supportsImages) parts.push('text PDFs only')
  return parts.join(' · ')
}

function History({ settings }: { settings: ExtractionSettings }) {
  if (settings.history.length === 0) return null

  return (
    <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
      <h2 className="border-b border-slate-200 px-4 py-3 text-sm font-semibold text-slate-900">Recent changes</h2>
      <table className="w-full text-left text-sm">
        <thead className="border-b border-slate-200 bg-slate-50 text-xs font-medium tracking-wide text-slate-500 uppercase">
          <tr>
            <th className="px-4 py-2.5">Model</th>
            <th className="px-4 py-2.5">Changed by</th>
            <th className="px-4 py-2.5">When</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {settings.history.map((change, i) => (
            <tr key={`${change.changedAt}-${i}`}>
              <td className="px-4 py-2.5">
                <span className="text-slate-900">{change.model}</span>{' '}
                <span className="text-slate-500">({providerInfo[change.provider]?.title ?? change.provider})</span>
              </td>
              <td className="px-4 py-2.5 text-slate-700">{change.changedBy}</td>
              <td className="px-4 py-2.5 text-slate-500" title={new Date(change.changedAt).toLocaleString()}>
                {formatRelative(change.changedAt)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
