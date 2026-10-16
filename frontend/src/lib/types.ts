// Mirrors the backend contracts (Kitsuneo.Application). Enums travel as strings and dates as ISO 8601.

export type Language = "English" | "Japanese";
export type ReviewRating = "Again" | "Hard" | "Good" | "Easy";
export type ReviewState = "Learning" | "Review" | "Relearning";
export type DictationTokenStatus = "Correct" | "Missing" | "Wrong" | "Extra";

export interface User {
  id: string;
  email: string;
  createdAt: string;
}

export interface AuthResponse {
  user: User;
  accessToken: string;
  accessTokenExpiresAt: string;
}

export interface Phrase {
  id: string;
  language: Language;
  text: string;
  spokenForm: string;
  reading: string | null;
  translation: string | null;
  isArchived: boolean;
  createdAt: string;
}

export interface PhraseInput {
  language: Language;
  text: string;
  spokenForm?: string | null;
  reading?: string | null;
  translation?: string | null;
}

export interface CursorPage<T> {
  items: T[];
  nextCursor: string | null;
}

export interface PracticeItem {
  phraseId: string;
  language: Language;
  isNew: boolean;
}

export interface TodayPractice {
  items: PracticeItem[];
  reviewCount: number;
  newCount: number;
}

export interface DictationToken {
  status: DictationTokenStatus;
  expected: string | null;
  actual: string | null;
}

/** A record's properties are camelCase, unlike enum values: { again, hard, good, easy }. */
export type ReviewPreview = Record<Lowercase<ReviewRating>, string>;

export interface AttemptResult {
  attemptId: string;
  tokens: DictationToken[];
  accuracy: number;
  text: string;
  spokenForm: string;
  reading: string | null;
  translation: string | null;
  nextDueAt: ReviewPreview;
}

export interface RateAttemptResult {
  dueAt: string;
  state: ReviewState;
}
