import type { ProblemDetails } from './types'

/** A non-2xx API response, with the problem details the API sent if any. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | undefined

  constructor(status: number, problem: ProblemDetails | undefined) {
    super(describe(status, problem))
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, init)

  if (!response.ok) {
    let problem: ProblemDetails | undefined
    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      // Some errors (e.g. a proxy failure) have no JSON body.
    }
    throw new ApiError(response.status, problem)
  }

  // 204 No Content, e.g. sign-out or a password reset.
  if (response.status === 204) return undefined as T

  return (await response.json()) as T
}

export function sendJson<T>(path: string, method: 'POST' | 'PUT', body?: unknown): Promise<T> {
  return request<T>(path, {
    method,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
}

// The most specific message first: a field error, then the detail, then the title.
function describe(status: number, problem: ProblemDetails | undefined): string {
  const fieldError = problem?.errors && Object.values(problem.errors).flat()[0]
  return fieldError ?? problem?.detail ?? problem?.title ?? `Request failed with status ${status}.`
}
