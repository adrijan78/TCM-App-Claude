/** POST /api/account/login */
export interface LoginRequest {
  email: string;
  password: string;
}

/** What a successful login returns. The only response that carries a token. */
export interface MemberToken {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  isCoach: boolean;
  roles: string[];
  token: string;
  expiresAt: string;
  photoUrl: string | null;
}

/** POST /api/account/register — coach only (SPEC section 6.1). */
export interface MemberRegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  height: number | null;
  weight: number | null;
  dateOfBirth: string;
  beltId: number;
  role: string;
}

/**
 * What registration returns. Deliberately carries no token: registration authenticates the
 * coach, not the member being created, so the server does not hand back a credential for
 * someone else. The new member signs in themselves.
 */
export interface RegisteredMember {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  isCoach: boolean;
  roles: string[];
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
  confirmPassword: string;
}

export interface Role {
  id: string;
  name: string;
}
