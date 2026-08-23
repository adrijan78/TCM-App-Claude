import { HttpErrorResponse } from '@angular/common/http';
import { ApiResponse } from '../_models/api-response.model';

/**
 * Peels the `ApiResponse<T>` envelope so components deal in plain data.
 *
 * A response with `success: false` that still arrived as HTTP 200 is turned into a thrown
 * error, so it lands in the same `error` branch a 4xx would — one failure path for callers
 * rather than two.
 */
export function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success || response.data === null) {
    throw new Error(response.message ?? 'The request could not be completed.');
  }

  return response.data;
}

/** Joins an envelope's message and field errors into one line for a snackbar. */
export function describeFailure(response: ApiResponse<unknown>): string {
  const message = response.message ?? 'The request could not be completed.';
  return response.errors?.length ? `${message} ${response.errors.join(' ')}` : message;
}

const UNREACHABLE = 'Cannot reach the server. Check your connection and try again.';
const GENERIC = 'Something went wrong. Please try again.';

/** A failed request, split into the one line to lead with and the rules behind it. */
export interface ApiFailure {
  readonly message: string;
  readonly details: readonly string[];
}

/**
 * Turns anything a failed request can hand a subscriber into something a person can read:
 * an `HttpErrorResponse` carrying an `ApiResponse` body, a network failure, or the plain
 * `Error` that `unwrap` throws for a `success: false` 200.
 *
 * The message and the field errors stay apart, because a form renders them apart — the
 * summary above the fields, the rules as a list beneath it.
 *
 * Never surfaces a raw exception string. The API is built not to send one, and this is the
 * second guard on that.
 */
export function apiErrorParts(error: unknown, fallback = GENERIC): ApiFailure {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return { message: UNREACHABLE, details: [] };

    const body = error.error as Partial<ApiResponse<unknown>> | null;
    if (body && typeof body === 'object' && typeof body.message === 'string') {
      return {
        message: body.message,
        details: Array.isArray(body.errors) ? body.errors : [],
      };
    }

    return { message: fallback, details: [] };
  }

  if (error instanceof Error && error.message) {
    return { message: error.message, details: [] };
  }

  return { message: fallback, details: [] };
}

/** The same thing on one line, for a snackbar that has nowhere to put a list. */
export function apiErrorMessage(error: unknown, fallback = GENERIC): string {
  const { message, details } = apiErrorParts(error, fallback);
  return details.length ? `${message} ${details.join(' ')}` : message;
}
