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
