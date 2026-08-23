import { AttendanceStatus, TrainingStatus, TrainingType } from './enums';

/** One row of the trainings table, and one entry in the calendar (SPEC section 6.5). */
export interface Training {
  id: number;
  date: string;
  description: string;
  trainingType: TrainingType;
  status: TrainingStatus;
  invitedCount: number;
  presentCount: number;
}

export interface EditTraining {
  description: string;
  date: string;
  trainingType: TrainingType;
  status: TrainingStatus;
  memberIds: string[];
}

/**
 * The training details screen (SPEC section 6.6). For a member, every attendee other than
 * themselves comes back with `performance` and `absenceReason` nulled by the server —
 * SPEC section 5 gives them "views own only".
 */
export interface TrainingDetails {
  id: number;
  date: string;
  description: string;
  trainingType: TrainingType;
  status: TrainingStatus;
  attendees: TrainingAttendee[];
}

export interface TrainingAttendee {
  memberId: string;
  firstName: string;
  lastName: string;
  status: AttendanceStatus;
  absenceReason: string | null;
  performance: number | null;
}

/** One line of the "trainings held" list on a member's profile (SPEC section 6.4). */
export interface MemberTraining {
  trainingId: number;
  date: string;
  description: string;
  trainingType: TrainingType;
  trainingStatus: TrainingStatus;
  attendanceStatus: AttendanceStatus;
  absenceReason: string | null;
  performance: number | null;
}

/** Everything behind the attendance and performance charts of SPEC section 6.4. */
export interface MemberAttendanceSummary {
  memberId: string;
  year: number | null;
  invitedCount: number;
  presentCount: number;
  absentCount: number;
  attendancePercentage: number;
  perMonth: MonthlyAttendance[];
  trainings: MemberTraining[];
}

export interface MonthlyAttendance {
  year: number;
  month: number;
  invited: number;
  present: number;
  absent: number;
}

/** `memberId` omitted means "me" — the only value a member is allowed to send. */
export interface ReportAttendance {
  memberId?: string | null;
  status: AttendanceStatus;
  absenceReason: string | null;
}

export interface SetPerformance {
  performance: number;
}

export interface TrainingInvitee {
  memberId: string;
  firstName: string;
  lastName: string;
  email: string | null;
}
