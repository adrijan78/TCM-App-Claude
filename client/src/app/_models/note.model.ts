import { NotePriority } from './enums';

/**
 * A note about a member (SPEC sections 6.4, 6.6 and 6.8). The server returns them High
 * priority first, then newest — the client must not re-sort and lose that order.
 */
export interface Note {
  id: number;
  title: string;
  content: string;
  createdAt: string;
  priority: NotePriority;
  fromMemberId: string;
  fromMemberFullName: string;
  toMemberId: string;
  toMemberFullName: string;
  trainingId: number | null;
  trainingDescription: string | null;
}

/**
 * Creating a note. There is no author field: the server takes it from the caller's token,
 * so a note can never be attributed to someone else.
 */
export interface CreateNote {
  title: string;
  content: string;
  priority: NotePriority;
  toMemberId: string;
  trainingId: number | null;
}
