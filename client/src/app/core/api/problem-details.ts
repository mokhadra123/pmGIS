import { HttpErrorResponse } from '@angular/common/http';

export interface ProblemDetails {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly errors?: Readonly<Record<string, readonly string[]>>;
}

export interface ApiFailure {
  readonly status: number;
  readonly message: string;
  // Field name (as the server names it, e.g. `ProjectCode`, `Activities[2].EndDate`) mapped to its messages. Empty when the failure was not field-specific.
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
}

const NETWORK_MESSAGE = 'Could not reach the server. Check your connection and try again.';

export function toApiFailure(error: unknown): ApiFailure {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, message: NETWORK_MESSAGE, fieldErrors: {} };
  }

  // status 0 is a transport failure: offline, CORS, or the API not running.
  if (error.status === 0) {
    return { status: 0, message: NETWORK_MESSAGE, fieldErrors: {} };
  }

  const problem = (error.error ?? {}) as ProblemDetails;
  const fieldErrors = problem.errors ?? {};
  const firstFieldMessage = Object.values(fieldErrors)[0]?.[0];

  return {
    status: error.status,
    message:
      problem.detail ??
      firstFieldMessage ??
      problem.title ??
      `The request failed (HTTP ${error.status}).`,
    fieldErrors,
  };
}

/// Server field names are PascalCase and use indexer syntax for collections (`Activities[2].EndDate`). The form controls are camelCase. This maps one to the other so a server-side rejection can be shown next to the field that caused it.
export function toControlPath(serverField: string): string {
  return serverField
    .split('.')
    .map((segment) => {
      const match = /^([A-Za-z]+)(\[\d+\])?$/.exec(segment);
      if (!match) return segment;
      const [, name, index = ''] = match;
      return name.charAt(0).toLowerCase() + name.slice(1) + index;
    })
    .join('.');
}
