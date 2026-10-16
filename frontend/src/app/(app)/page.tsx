"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Alert, Card, Spinner } from "@/components/ui";
import { api } from "@/lib/api";
import { errorMessage } from "@/lib/format";
import type { TodayPractice } from "@/lib/types";

export default function HomePage() {
  const [today, setToday] = useState<TodayPractice | null>(null);
  const [phraseCount, setPhraseCount] = useState<{ count: number; more: boolean } | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([api.practice.today(), api.phrases.list({ pageSize: 100 })])
      .then(([practice, phrases]) => {
        setToday(practice);
        setPhraseCount({ count: phrases.items.length, more: phrases.nextCursor !== null });
      })
      .catch((e) => setError(errorMessage(e)));
  }, []);

  if (error) return <Alert>{error}</Alert>;
  if (!today || !phraseCount) return <Spinner />;

  const total = today.items.length;
  const hasPhrases = phraseCount.count > 0;

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Hoje</h1>
        <p className="mt-1 text-sm text-muted">
          {total > 0
            ? "Ouça cada frase, escreva o que entendeu e diga como foi."
            : hasPhrases
              ? "Nada pendente por agora. Volte mais tarde ou adicione frases novas."
              : "Comece salvando algumas frases que você quer aprender."}
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <Stat label="Revisões pendentes" value={today.reviewCount} />
        <Stat label="Frases novas hoje" value={today.newCount} />
        <Stat label="Frases salvas" value={phraseCount.more ? `${phraseCount.count}+` : phraseCount.count} />
      </div>

      <Card className="flex flex-col items-start gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="font-semibold">{total > 0 ? `${total} ${total === 1 ? "frase" : "frases"} para praticar` : "Tudo em dia"}</h2>
          <p className="mt-1 text-sm text-muted">
            {total > 0 ? "Revisões primeiro, depois as novas." : "A repetição espaçada vai trazer as frases de volta na hora certa."}
          </p>
        </div>
        {total > 0 ? (
          <Link
            href="/practice"
            className="rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-accent-strong"
          >
            Começar prática
          </Link>
        ) : (
          <Link
            href="/phrases"
            className="rounded-lg border border-border px-4 py-2 text-sm font-medium transition-colors hover:bg-surface-muted"
          >
            {hasPhrases ? "Ver frases" : "Adicionar frases"}
          </Link>
        )}
      </Card>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="rounded-2xl border border-border bg-surface p-5">
      <p className="text-sm text-muted">{label}</p>
      <p className="mt-1 text-3xl font-semibold tabular-nums">{value}</p>
    </div>
  );
}
