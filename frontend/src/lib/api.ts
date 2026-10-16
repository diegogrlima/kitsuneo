import type {
  AttemptResult,
  AuthResponse,
  CursorPage,
  Language,
  Phrase,
  PhraseInput,
  RateAttemptResult,
  ReviewRating,
  TodayPractice,
  User,
} from "./types";

// Same origin: next.config.ts proxies /api/v1/* to the backend
const BASE = "/api/v1";

interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

// The backend speaks English; the most common messages are shown in Portuguese
const MESSAGES: Record<string, string> = {
  "Invalid email or password.": "E-mail ou senha incorretos.",
  "Invalid or expired session. Log in again.": "Sua sessão expirou. Entre de novo.",
  "Email is already registered.": "Já existe uma conta com esse e-mail.",
  "Email is required.": "Informe o e-mail.",
  "Email is invalid.": "E-mail inválido.",
  "Password must have at least 8 characters.": "A senha precisa ter pelo menos 8 caracteres.",
  "Password must have at most 128 characters.": "A senha pode ter no máximo 128 caracteres.",
  "Password cannot be only spaces.": "A senha não pode ser só espaços.",
  "Text is required.": "Informe o texto da frase.",
  "Reading is only supported for Japanese phrases.": "A leitura só vale para frases em japonês.",
};

function translate(message: string): string {
  return MESSAGES[message] ?? message;
}

async function toApiError(response: Response): Promise<ApiError> {
  if (response.status === 429)
    return new ApiError(429, "Muitas tentativas. Aguarde um minuto e tente de novo.");

  let problem: ProblemDetails | null = null;
  try {
    problem = (await response.json()) as ProblemDetails;
  } catch {
    // Not every error has a body
  }

  const validation = problem?.errors ? Object.values(problem.errors).flat()[0] : undefined;
  const message =
    validation ?? problem?.detail ?? problem?.title ?? `Erro inesperado (${response.status}).`;

  return new ApiError(response.status, translate(message));
}

// --- Session -----------------------------------------------------------------
// The access token lives only in memory; the refresh token is an HttpOnly cookie
// the browser sends to /api/v1/auth on its own.

let accessToken: string | null = null;
let refreshing: Promise<AuthResponse | null> | null = null;
const sessionLostListeners = new Set<() => void>();

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function onSessionLost(listener: () => void): () => void {
  sessionLostListeners.add(listener);
  return () => sessionLostListeners.delete(listener);
}

/**
 * Trades the refresh cookie for a new access token. Concurrent callers share one request:
 * the backend rotates the cookie, and reusing an already rotated one revokes the whole session.
 * Resolves to null when there is no valid session.
 */
export function refreshSession(): Promise<AuthResponse | null> {
  refreshing ??= (async () => {
    try {
      const response = await fetch(`${BASE}/auth/refresh`, {
        method: "POST",
        credentials: "same-origin",
      });
      if (response.status === 401) {
        accessToken = null;
        return null;
      }
      if (!response.ok) throw await toApiError(response);

      const session = (await response.json()) as AuthResponse;
      accessToken = session.accessToken;
      return session;
    } finally {
      refreshing = null;
    }
  })();

  return refreshing;
}

interface RequestOptions extends Omit<RequestInit, "body"> {
  body?: unknown;
  /** Sends the access token and retries once after a refresh on 401. Defaults to true. */
  auth?: boolean;
}

async function request(path: string, options: RequestOptions = {}): Promise<Response> {
  const { body, auth = true, headers, ...init } = options;

  const send = () => {
    const finalHeaders = new Headers(headers);
    if (body !== undefined) finalHeaders.set("Content-Type", "application/json");
    if (auth && accessToken) finalHeaders.set("Authorization", `Bearer ${accessToken}`);

    return fetch(`${BASE}${path}`, {
      ...init,
      headers: finalHeaders,
      credentials: "same-origin",
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  };

  let response = await send();

  if (response.status === 401 && auth) {
    const session = await refreshSession();
    if (session) {
      response = await send();
    }
    if (!session || response.status === 401) {
      accessToken = null;
      sessionLostListeners.forEach((listener) => listener());
      throw new ApiError(401, "Sua sessão expirou. Entre de novo.");
    }
  }

  if (!response.ok) throw await toApiError(response);
  return response;
}

async function json<T>(path: string, options?: RequestOptions): Promise<T> {
  const response = await request(path, options);
  return (await response.json()) as T;
}

function query(params: Record<string, string | number | null | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== null && value !== undefined && value !== "") search.set(key, String(value));
  }
  const result = search.toString();
  return result ? `?${result}` : "";
}

// --- Endpoints ---------------------------------------------------------------

export const api = {
  auth: {
    register: (email: string, password: string) =>
      json<AuthResponse>("/auth/register", { method: "POST", body: { email, password }, auth: false }),
    login: (email: string, password: string) =>
      json<AuthResponse>("/auth/login", { method: "POST", body: { email, password }, auth: false }),
    logout: () => request("/auth/logout", { method: "POST" }),
    me: () => json<User>("/auth/me"),
  },

  phrases: {
    list: (params: { language?: Language | null; search?: string; cursor?: string | null; pageSize?: number }) =>
      json<CursorPage<Phrase>>(`/phrases${query(params)}`),
    get: (id: string) => json<Phrase>(`/phrases/${id}`),
    create: (input: PhraseInput) => json<Phrase>("/phrases", { method: "POST", body: input }),
    update: (id: string, input: PhraseInput) =>
      request(`/phrases/${id}`, { method: "PUT", body: input }).then(() => undefined),
    remove: (id: string) => request(`/phrases/${id}`, { method: "DELETE" }).then(() => undefined),
    /** The phrase read aloud (MP3). Fetched with the token, so it cannot be an <audio src> URL. */
    audio: (id: string, slow = false) =>
      request(`/phrases/${id}/audio${query({ slow: slow ? "true" : null })}`).then((r) => r.blob()),
  },

  practice: {
    today: (language?: Language | null) => json<TodayPractice>(`/practice/today${query({ language })}`),
    submitAttempt: (
      phraseId: string,
      attempt: { answer: string; playCount: number; usedSlowSpeed: boolean; durationMs: number },
    ) => json<AttemptResult>(`/phrases/${phraseId}/attempts`, { method: "POST", body: attempt }),
    rate: (attemptId: string, rating: ReviewRating) =>
      json<RateAttemptResult>(`/attempts/${attemptId}/rating`, { method: "POST", body: { rating } }),
  },
};
