import type { Language } from "./types";

export const LANGUAGE_LABEL: Record<Language, string> = {
  English: "Inglês",
  Japanese: "Japonês",
};

/** The backend sends UTC; a date without an offset is still UTC. */
export function parseDate(value: string): Date {
  return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(value) ? value : `${value}Z`);
}

/** "em 10 min", "em 3 dias"... for the time between now and the given date. */
export function timeUntil(value: string, now = Date.now()): string {
  const minutes = Math.max(Math.round((parseDate(value).getTime() - now) / 60_000), 0);

  if (minutes < 1) return "agora";
  if (minutes < 60) return `em ${minutes} min`;

  const hours = Math.round(minutes / 60);
  if (hours < 24) return `em ${hours} h`;

  const days = Math.round(hours / 24);
  if (days < 30) return days === 1 ? "amanhã" : `em ${days} dias`;

  const months = Math.round(days / 30);
  if (months < 12) return months === 1 ? "em 1 mês" : `em ${months} meses`;

  const years = Math.round(days / 365);
  return years === 1 ? "em 1 ano" : `em ${years} anos`;
}

export function formatDate(value: string): string {
  return parseDate(value).toLocaleDateString("pt-BR", { day: "2-digit", month: "short", year: "numeric" });
}

export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : "Algo deu errado. Tente de novo.";
}
