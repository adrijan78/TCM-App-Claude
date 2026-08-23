/**
 * These mirror the C# enums in TCM.Domain.Enums. The API serialises them as integers — no
 * JsonStringEnumConverter is registered server-side — so the numeric values must match
 * exactly. Keep in step with server/TCM.Domain/Enums/.
 */

export enum TrainingType {
  Regular = 0,
  Sparring = 1,
}

export enum TrainingStatus {
  Active = 0,
  Cancelled = 1,
  Finished = 2,
}

/** Ascends with importance, so notes sort High first by sorting descending. */
export enum NotePriority {
  Low = 0,
  Medium = 1,
  High = 2,
}

export enum AttendanceStatus {
  Invited = 0,
  Present = 1,
  Absent = 2,
}

/** Age bands follow World Taekwondo: Cadets are younger than Juniors. */
export enum AgeGroup {
  Kids = 0,
  Cadets = 1,
  Juniors = 2,
  Seniors = 3,
}

export enum PaymentMethod {
  Cash = 0,
  Online = 1,
}

export const TRAINING_TYPE_LABELS: Record<TrainingType, string> = {
  [TrainingType.Regular]: 'Regular',
  [TrainingType.Sparring]: 'Sparring',
};

export const TRAINING_STATUS_LABELS: Record<TrainingStatus, string> = {
  [TrainingStatus.Active]: 'Active',
  [TrainingStatus.Cancelled]: 'Cancelled',
  [TrainingStatus.Finished]: 'Finished',
};

export const NOTE_PRIORITY_LABELS: Record<NotePriority, string> = {
  [NotePriority.Low]: 'Low',
  [NotePriority.Medium]: 'Medium',
  [NotePriority.High]: 'High',
};

export const ATTENDANCE_STATUS_LABELS: Record<AttendanceStatus, string> = {
  [AttendanceStatus.Invited]: 'Invited',
  [AttendanceStatus.Present]: 'Present',
  [AttendanceStatus.Absent]: 'Absent',
};

export const AGE_GROUP_LABELS: Record<AgeGroup, string> = {
  [AgeGroup.Kids]: 'Kids (under 12)',
  [AgeGroup.Cadets]: 'Cadets (12–14)',
  [AgeGroup.Juniors]: 'Juniors (15–17)',
  [AgeGroup.Seniors]: 'Seniors (18+)',
};
