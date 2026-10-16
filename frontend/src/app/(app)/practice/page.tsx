"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import { Alert, Button, Card, LanguageBadge, Select, Spinner, Textarea } from "@/components/ui";
import { api } from "@/lib/api";
import { errorMessage, LANGUAGE_LABEL, timeUntil } from "@/lib/format";
import type { AttemptResult, DictationToken, Language, PracticeItem, ReviewRating, TodayPractice } from "@/lib/types";
import { usePhraseAudio } from "@/lib/use-phrase-audio";

const RATINGS: { rating: ReviewRating; label: string; className: string }[] = [
  { rating: "Again", label: "Errei", className: "border-danger/40 text-danger hover:bg-danger-soft" },
  { rating: "Hard", label: "Difícil", className: "border-warning/40 text-warning hover:bg-warning-soft" },
  { rating: "Good", label: "Bom", className: "border-success/40 text-success hover:bg-success-soft" },
  { rating: "Easy", label: "Fácil", className: "border-accent/40 text-accent-strong hover:bg-accent-soft" },
];

type Session =
  | { phase: "setup" }
  | { phase: "running"; items: PracticeItem[]; index: number; accuracies: number[] }
  | { phase: "done"; accuracies: number[] };

export default function PracticePage() {
  // One language at a time: mixing English and Japanese dictations in a session is confusing
  const [language, setLanguage] = useState<Language>("English");
  const [session, setSession] = useState<Session>({ phase: "setup" });
  // Bumped when going back to the setup screen, so it reloads what is left
  const [round, setRound] = useState(0);
  const [today, setToday] = useState<{ key: string; data: TodayPractice | null; error: string | null } | null>(null);
  const key = `${language}|${round}`;

  useEffect(() => {
    let active = true;
    api.practice
      .today(language)
      .then((data) => active && setToday({ key, data, error: null }))
      .catch((e) => active && setToday({ key, data: null, error: errorMessage(e) }));
    return () => {
      active = false;
    };
  }, [key, language]);

  const current = today?.key === key ? today : null;
  const practice = current?.data;
  const backToSetup = () => {
    setRound((r) => r + 1);
    setSession({ phase: "setup" });
  };

  const advance = useCallback((accuracy: number) => {
    setSession((current) => {
      if (current.phase !== "running") return current;
      const accuracies = [...current.accuracies, accuracy];
      return current.index + 1 < current.items.length
        ? { ...current, index: current.index + 1, accuracies }
        : { phase: "done", accuracies };
    });
  }, []);

  if (session.phase === "running") {
    const item = session.items[session.index];
    return (
      <div className="flex flex-col gap-4">
        <Progress current={session.index + 1} total={session.items.length} onQuit={backToSetup} />
        {/* Keyed by phrase so each one starts with a clean state */}
        <Exercise key={item.phraseId} item={item} onDone={advance} />
        <p className="text-center text-xs text-muted">Voz gerada por ElevenLabs</p>
      </div>
    );
  }

  if (session.phase === "done") {
    const average = session.accuracies.reduce((sum, a) => sum + a, 0) / session.accuracies.length;
    return (
      <Card className="mx-auto flex max-w-md flex-col items-center gap-3 text-center">
        <p className="text-4xl" aria-hidden>
          🎉
        </p>
        <h1 className="text-xl font-semibold">Prática concluída</h1>
        <p className="text-sm text-muted">
          {session.accuracies.length} {session.accuracies.length === 1 ? "frase" : "frases"} · acerto médio de{" "}
          {Math.round(average)}%
        </p>
        <div className="mt-2 flex gap-2">
          <Button variant="secondary" onClick={backToSetup}>
            Ver o que falta
          </Button>
          <Link href="/" className="rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white hover:bg-accent-strong">
            Voltar ao início
          </Link>
        </div>
      </Card>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Praticar</h1>
          <p className="mt-1 text-sm text-muted">Ouça a frase, escreva o que entendeu e avalie como foi.</p>
        </div>
        <Select
          aria-label="Idioma da prática"
          className="sm:w-44"
          value={language}
          onChange={(e) => setLanguage(e.target.value as Language)}
        >
          {(Object.keys(LANGUAGE_LABEL) as Language[]).map((value) => (
            <option key={value} value={value}>
              {LANGUAGE_LABEL[value]}
            </option>
          ))}
        </Select>
      </div>

      {current?.error && <Alert>{current.error}</Alert>}

      {!current && <Spinner />}

      {practice && (
        <Card className="flex flex-col items-center gap-4 py-10 text-center">
          {practice.items.length > 0 ? (
            <>
              <p className="text-sm text-muted">
                {practice.reviewCount} {practice.reviewCount === 1 ? "revisão" : "revisões"} · {practice.newCount}{" "}
                {practice.newCount === 1 ? "frase nova" : "frases novas"}
              </p>
              <Button
                className="px-6 py-3 text-base"
                onClick={() => setSession({ phase: "running", items: practice.items, index: 0, accuracies: [] })}
              >
                Começar
              </Button>
            </>
          ) : (
            <>
              <p className="font-medium">Nada para praticar agora.</p>
              <p className="text-sm text-muted">
                As revisões voltam quando chegar a hora. Frases novas entram até 10 por dia.
              </p>
              <Link href="/phrases" className="text-sm font-medium text-accent hover:underline">
                Adicionar frases
              </Link>
            </>
          )}
        </Card>
      )}
    </div>
  );
}

function Progress({ current, total, onQuit }: { current: number; total: number; onQuit: () => void }) {
  return (
    <div className="flex items-center gap-3">
      <div className="h-2 flex-1 overflow-hidden rounded-full bg-surface-muted">
        <div className="h-full rounded-full bg-accent transition-all" style={{ width: `${((current - 1) / total) * 100}%` }} />
      </div>
      <span className="text-sm tabular-nums text-muted">
        {current}/{total}
      </span>
      <Button variant="ghost" className="px-2.5" onClick={onQuit}>
        Encerrar
      </Button>
    </div>
  );
}

function Exercise({ item, onDone }: { item: PracticeItem; onDone: (accuracy: number) => void }) {
  const audio = usePhraseAudio();
  const [answer, setAnswer] = useState("");
  const [result, setResult] = useState<AttemptResult | null>(null);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const stats = useRef({ playCount: 0, usedSlowSpeed: false, startedAt: 0 });
  const input = useRef<HTMLTextAreaElement>(null);
  const isJapanese = item.language === "Japanese";

  const playAudio = audio.play;
  const play = useCallback(
    async (slow: boolean) => {
      if (await playAudio(item.phraseId, slow)) {
        stats.current.playCount += 1;
        if (slow) stats.current.usedSlowSpeed = true;
      }
    },
    [playAudio, item.phraseId],
  );

  // Plays as soon as the exercise appears; the click that led here allows autoplay.
  // The ref keeps Strict Mode's second effect run from playing (and counting) it twice.
  const autoplayed = useRef(false);
  useEffect(() => {
    input.current?.focus();
    if (autoplayed.current) return;
    autoplayed.current = true;
    stats.current.startedAt = Date.now();
    play(false);
  }, [play]);

  async function submit(text: string) {
    setPending(true);
    setError(null);
    try {
      const { playCount, usedSlowSpeed, startedAt } = stats.current;
      setResult(
        await api.practice.submitAttempt(item.phraseId, {
          answer: text,
          playCount,
          usedSlowSpeed,
          durationMs: Date.now() - startedAt,
        }),
      );
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setPending(false);
    }
  }

  const rate = useCallback(
    async (rating: ReviewRating) => {
      if (!result || pending) return;
      setPending(true);
      setError(null);
      try {
        await api.practice.rate(result.attemptId, rating);
        onDone(result.accuracy);
      } catch (e) {
        setError(errorMessage(e));
        setPending(false);
      }
    },
    [result, pending, onDone],
  );

  // 1–4 rate the attempt once the correction is shown
  useEffect(() => {
    if (!result) return;
    function onKeyDown(event: KeyboardEvent) {
      const index = ["1", "2", "3", "4"].indexOf(event.key);
      if (index >= 0 && !event.metaKey && !event.ctrlKey && !event.altKey) rate(RATINGS[index].rating);
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [result, rate]);

  return (
    <Card className="flex flex-col gap-5">
      <div className="flex items-center gap-2 text-sm text-muted">
        <LanguageBadge language={item.language} />
        <span>{item.isNew ? "Frase nova" : "Revisão"}</span>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Button onClick={() => play(false)} disabled={audio.isLoading(item.phraseId)}>
          {audio.isLoading(item.phraseId) ? "Carregando…" : "▶ Ouvir"}
        </Button>
        <Button variant="secondary" onClick={() => play(true)} disabled={audio.isLoading(item.phraseId, true)}>
          {audio.isLoading(item.phraseId, true) ? "Carregando…" : "🐢 Mais devagar"}
        </Button>
      </div>

      {audio.error && <Alert tone="warning">{audio.error}</Alert>}

      {!result ? (
        <form
          className="flex flex-col gap-3"
          onSubmit={(e) => {
            e.preventDefault();
            submit(answer);
          }}
        >
          <label htmlFor="answer" className="text-sm font-medium">
            {isJapanese ? "O que você ouviu? (em kana)" : "O que você ouviu?"}
          </label>
          <Textarea
            id="answer"
            ref={input}
            rows={2}
            lang={isJapanese ? "ja" : "en"}
            autoComplete="off"
            autoCorrect="off"
            spellCheck={false}
            value={answer}
            onChange={(e) => setAnswer(e.target.value)}
            onKeyDown={(e) => {
              // Enter checks, Shift+Enter breaks the line; IME composition keeps its own Enter
              if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
                e.preventDefault();
                submit(answer);
              }
            }}
          />
          {error && <Alert>{error}</Alert>}
          <div className="flex justify-between gap-2">
            <Button type="button" variant="ghost" disabled={pending} onClick={() => submit("")}>
              Não sei, mostrar
            </Button>
            <Button type="submit" disabled={pending || !answer.trim()}>
              {pending ? "Conferindo…" : "Conferir"}
            </Button>
          </div>
        </form>
      ) : (
        <div className="flex flex-col gap-5">
          <div>
            <div className="flex items-baseline justify-between gap-2">
              <p className="text-sm font-medium">Correção</p>
              <p className={`text-sm font-semibold tabular-nums ${accuracyColor(result.accuracy)}`}>
                {Math.round(result.accuracy)}% de acerto
              </p>
            </div>
            <Correction tokens={result.tokens} japanese={isJapanese} />
          </div>

          <div className="rounded-xl bg-surface-muted p-4">
            <p className="text-lg font-medium" lang={isJapanese ? "ja" : "en"}>
              {result.text}
            </p>
            {result.reading && (
              <p className="mt-1 text-sm text-muted" lang="ja">
                {result.reading}
              </p>
            )}
            {result.spokenForm !== result.text && (
              <p className="mt-1 text-sm text-muted">
                Falado: <span lang={isJapanese ? "ja" : "en"}>{result.spokenForm}</span>
              </p>
            )}
            {result.translation && <p className="mt-2 text-sm">{result.translation}</p>}
          </div>

          <div>
            <p className="mb-2 text-sm font-medium">Como foi?</p>
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              {RATINGS.map(({ rating, label, className }, index) => (
                <button
                  key={rating}
                  type="button"
                  disabled={pending}
                  onClick={() => rate(rating)}
                  className={`flex flex-col items-center rounded-xl border bg-surface px-3 py-2.5 transition-colors disabled:opacity-50 ${className}`}
                >
                  <span className="text-sm font-semibold">
                    {label} <kbd className="ml-1 hidden font-mono text-xs opacity-60 sm:inline">{index + 1}</kbd>
                  </span>
                  <span className="text-xs opacity-80">{timeUntil(result.nextDueAt[previewKey(rating)])}</span>
                </button>
              ))}
            </div>
          </div>

          {error && <Alert>{error}</Alert>}
        </div>
      )}
    </Card>
  );
}

function previewKey(rating: ReviewRating) {
  return rating.toLowerCase() as Lowercase<ReviewRating>;
}

function accuracyColor(accuracy: number): string {
  if (accuracy >= 90) return "text-success";
  if (accuracy >= 50) return "text-warning";
  return "text-danger";
}

function Correction({ tokens, japanese }: { tokens: DictationToken[]; japanese: boolean }) {
  return (
    <p
      className={`mt-2 flex flex-wrap items-baseline text-lg leading-relaxed ${japanese ? "" : "gap-x-2"}`}
      lang={japanese ? "ja" : "en"}
    >
      {tokens.map((token, index) => (
        <TokenView key={index} token={token} />
      ))}
    </p>
  );
}

function TokenView({ token }: { token: DictationToken }) {
  switch (token.status) {
    case "Correct":
      return <span className="text-success">{token.expected}</span>;
    case "Wrong":
      return (
        <span title={`Você escreveu “${token.actual}”`}>
          <span className="text-sm text-danger line-through decoration-2">{token.actual}</span>
          <span className="text-danger underline decoration-2 underline-offset-4">{token.expected}</span>
        </span>
      );
    case "Missing":
      return (
        <span title="Faltou" className="text-warning underline decoration-dashed decoration-2 underline-offset-4">
          {token.expected}
        </span>
      );
    case "Extra":
      return (
        <span title="Sobrou" className="text-sm text-muted line-through decoration-2">
          {token.actual}
        </span>
      );
  }
}
