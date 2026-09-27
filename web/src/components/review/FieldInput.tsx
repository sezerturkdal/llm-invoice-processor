import { useId } from 'react'
import type { ValidationIssue } from '../../api/types'

interface FieldInputProps {
  label: string
  value: string
  onChange: (value: string) => void
  issues?: ValidationIssue[]
  /** Input error from the form itself (e.g. not a number), shown before validation issues. */
  error?: string
  readOnly?: boolean
  type?: 'text' | 'date'
  inputMode?: 'text' | 'decimal'
  placeholder?: string
  className?: string
}

/** A labelled input that turns amber, with the rule's message, when validation flagged its field. */
export function FieldInput({
  label,
  value,
  onChange,
  issues = [],
  error,
  readOnly = false,
  type = 'text',
  inputMode,
  placeholder,
  className = '',
}: FieldInputProps) {
  const id = useId()
  const flagged = issues.length > 0
  const messages = [...(error ? [error] : []), ...issues.map((i) => i.message)]

  const tone = error
    ? 'border-rose-400 bg-rose-50/40 focus:border-rose-500 focus:ring-rose-500/30'
    : flagged
      ? 'border-amber-400 bg-amber-50/60 focus:border-amber-500 focus:ring-amber-500/30'
      : 'border-slate-300 bg-white focus:border-accent-500 focus:ring-accent-500/30'

  return (
    <div className={className}>
      <label htmlFor={id} className="mb-1 flex items-center gap-1.5 text-xs font-medium text-slate-600">
        {label}
        {flagged && !error && <span className="size-1.5 rounded-full bg-amber-500" aria-hidden />}
      </label>
      <input
        id={id}
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        readOnly={readOnly}
        inputMode={inputMode}
        placeholder={placeholder}
        aria-invalid={flagged || Boolean(error)}
        aria-describedby={messages.length > 0 ? `${id}-messages` : undefined}
        className={`w-full rounded-lg border px-3 py-2 text-sm focus:ring-2 focus:outline-none read-only:cursor-default read-only:border-slate-200 read-only:bg-slate-50 ${
          inputMode === 'decimal' ? 'tabular text-right' : ''
        } ${tone}`}
      />
      {messages.length > 0 && (
        <ul id={`${id}-messages`} className="mt-1 space-y-0.5 text-xs">
          {messages.map((message, i) => (
            <li key={i} className={error && i === 0 ? 'text-rose-700' : 'text-amber-800'}>
              {message}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
