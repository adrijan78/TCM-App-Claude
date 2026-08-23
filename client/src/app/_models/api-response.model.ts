/**
 * The envelope every endpoint returns (SPEC section 3.1). Services unwrap it in one place
 * so components see plain data or an error, never the wrapper.
 */
export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  message: string | null;
  errors: string[];
}
