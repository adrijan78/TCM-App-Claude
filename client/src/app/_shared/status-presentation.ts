import {
  AttendanceStatus,
  NotePriority,
  PaymentMethod,
  TrainingStatus,
  TrainingType,
} from '../_models/enums';
import { Tone } from './components/status-chip';

/** What a `<app-status-chip>` needs to render one state. */
export interface StatusPresentation {
  readonly label: string;
  readonly tone: Tone;
  readonly icon: string;
}

/**
 * The one mapping from a domain enum to its colour and glyph.
 *
 * Without this, phase 9's five modules each decide for themselves what an absent member
 * looks like, and the member profile disagrees with the training details page about the same
 * member on the same day. Adding a state means adding it here, once.
 *
 * The tones come from `_tokens.scss`; see that file for what each ramp means.
 */
export const TRAINING_STATUS_PRESENTATION: Record<TrainingStatus, StatusPresentation> = {
  // SPEC 6.5 names these two colours explicitly: green finished, yellow active.
  [TrainingStatus.Finished]: { label: 'Finished', tone: 'positive', icon: 'task_alt' },
  [TrainingStatus.Active]: { label: 'Active', tone: 'caution', icon: 'schedule' },
  // Cancelled is quiet rather than red: it is a fact about the calendar, not a failure.
  [TrainingStatus.Cancelled]: { label: 'Cancelled', tone: 'quiet', icon: 'event_busy' },
};

export const ATTENDANCE_STATUS_PRESENTATION: Record<AttendanceStatus, StatusPresentation> = {
  [AttendanceStatus.Present]: { label: 'Present', tone: 'positive', icon: 'check_circle' },
  [AttendanceStatus.Absent]: { label: 'Absent', tone: 'critical', icon: 'cancel' },
  [AttendanceStatus.Invited]: { label: 'Invited', tone: 'info', icon: 'mail_outline' },
};

export const NOTE_PRIORITY_PRESENTATION: Record<NotePriority, StatusPresentation> = {
  // High sorts first (SPEC 6.8), so it has to read first too.
  [NotePriority.High]: { label: 'High', tone: 'critical', icon: 'priority_high' },
  [NotePriority.Medium]: { label: 'Medium', tone: 'caution', icon: 'drag_handle' },
  [NotePriority.Low]: { label: 'Low', tone: 'info', icon: 'low_priority' },
};

export const TRAINING_TYPE_PRESENTATION: Record<TrainingType, StatusPresentation> = {
  [TrainingType.Regular]: { label: 'Regular', tone: 'quiet', icon: 'fitness_center' },
  [TrainingType.Sparring]: { label: 'Sparring', tone: 'info', icon: 'sports_mma' },
};

export const PAYMENT_METHOD_PRESENTATION: Record<PaymentMethod, StatusPresentation> = {
  [PaymentMethod.Cash]: { label: 'Cash', tone: 'quiet', icon: 'payments' },
  [PaymentMethod.Online]: { label: 'Online', tone: 'info', icon: 'credit_card' },
};

/** Membership standing is derived, not stored, so it is keyed by name rather than an enum. */
export const MEMBERSHIP_PRESENTATION: Record<'paid' | 'due' | 'overdue', StatusPresentation> = {
  paid: { label: 'Paid', tone: 'positive', icon: 'verified' },
  due: { label: 'Due soon', tone: 'caution', icon: 'hourglass_bottom' },
  overdue: { label: 'Overdue', tone: 'critical', icon: 'error_outline' },
};
