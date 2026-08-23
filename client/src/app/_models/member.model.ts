import { AgeGroup } from './enums';

/** Mirrors TCM.Application.Dtos.Common.BeltDto. */
export interface Belt {
  id: number;
  beltName: string;
  rank: number;
}

/**
 * Mirrors MemberDto. `photoPublicId` is an opaque GUID, not a URL — photos live in the
 * database and are fetched from `GET /api/photos/{publicId}` through the authenticated
 * HTTP client, because an `img src` cannot carry a bearer token.
 */
export interface Member {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  dateOfBirth: string;
  age: number;
  startedOn: string;
  isActive: boolean;
  isCoach: boolean;
  height: number | null;
  weight: number | null;
  currentBelt: Belt | null;
  photoPublicId: string | null;
}

/** Query for GET /api/members. Coach only. */
export interface MemberFilter {
  search?: string | null;
  beltId?: number | null;
  ageGroup?: AgeGroup | null;
}

/**
 * Mirrors EditMemberDto. It deliberately has no isCoach, isActive, clubId or role: those
 * are not the member's to change, and the server has nowhere to bind them either.
 */
export interface EditMember {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  dateOfBirth: string;
  height: number | null;
  weight: number | null;
}

/** One belt exam on a member's record. */
export interface MemberBelt {
  id: number;
  memberId: string;
  belt: Belt;
  dateReceived: string;
  description: string | null;
  isCurrentBelt: boolean;
}

export interface AddMemberBelt {
  beltId: number;
  dateReceived: string;
  description: string | null;
  isCurrentBelt: boolean;
}

/** Metadata for a stored photo. The bytes come from a separate authenticated request. */
export interface Photo {
  publicId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  createdAt: string;
}
