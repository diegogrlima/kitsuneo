"use client";

import { useEffect, useState } from "react";
import { PhraseFormDialog } from "@/components/phrase-form";
import { Alert, Button, Input, LanguageBadge, Select, Spinner } from "@/components/ui";
import { api } from "@/lib/api";
import { errorMessage, formatDate, LANGUAGE_LABEL } from "@/lib/format";
import type { Language, Phrase } from "@/lib/types";
import { usePhraseAudio } from "@/lib/use-phrase-audio";

const PAGE_SIZE = 20;

interface PhraseList {
  /** The language and search the items were loaded for. */
  filter: string | null;
  items: Phrase[];
  nextCursor: string | null;
  error: string | null;
}

export default function PhrasesPage() {
  const [language, setLanguage] = useState<Language | "">("");
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [list, setList] = useState<PhraseList>({ filter: null, items: [], nextCursor: null, error: null });
  const [loadingMore, setLoadingMore] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [editing, setEditing] = useState<Phrase | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const audio = usePhraseAudio();

  useEffect(() => {
    const timeout = setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => clearTimeout(timeout);
  }, [searchInput]);

  // The list belongs to the filter it was loaded for; while they differ, a new one is loading
  const filter = `${language}|${search}`;

  useEffect(() => {
    let active = true;
    api.phrases
      .list({ language: language || null, search, pageSize: PAGE_SIZE })
      .then((page) => active && setList({ filter, items: page.items, nextCursor: page.nextCursor, error: null }))
      .catch((e) => active && setList({ filter, items: [], nextCursor: null, error: errorMessage(e) }));
    return () => {
      active = false;
    };
  }, [filter, language, search]);

  async function loadMore() {
    if (!list.nextCursor) return;
    setLoadingMore(true);
    setActionError(null);
    try {
      const page = await api.phrases.list({ language: language || null, search, cursor: list.nextCursor, pageSize: PAGE_SIZE });
      setList((current) =>
        current.filter === filter
          ? { ...current, items: [...current.items, ...page.items], nextCursor: page.nextCursor }
          : current,
      );
    } catch (e) {
      setActionError(errorMessage(e));
    } finally {
      setLoadingMore(false);
    }
  }

  const setPhrases = (update: (current: Phrase[]) => Phrase[]) =>
    setList((current) => ({ ...current, items: update(current.items) }));

  const loading = list.filter !== filter || loadingMore;
  const { items: phrases, nextCursor } = list;
  const error = actionError ?? list.error;

  function openForm(phrase: Phrase | null) {
    setEditing(phrase);
    setFormOpen(true);
  }

  function handleSaved(saved: Phrase, created: boolean) {
    setFormOpen(false);
    setPhrases((current) => (created ? [saved, ...current] : current.map((p) => (p.id === saved.id ? saved : p))));
  }

  async function handleDelete(phrase: Phrase) {
    if (!confirm(`Excluir “${phrase.text}”? Ela sai da lista e da prática.`)) return;
    setActionError(null);
    try {
      await api.phrases.remove(phrase.id);
      setPhrases((current) => current.filter((p) => p.id !== phrase.id));
    } catch (e) {
      setActionError(errorMessage(e));
    }
  }

  const filtered = Boolean(search || language);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold tracking-tight">Frases</h1>
        <Button onClick={() => openForm(null)}>Nova frase</Button>
      </div>

      <div className="flex flex-col gap-2 sm:flex-row">
        <Input
          type="search"
          aria-label="Buscar frases"
          placeholder="Buscar no texto ou na tradução"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
        />
        <Select
          aria-label="Filtrar por idioma"
          className="sm:w-44"
          value={language}
          onChange={(e) => setLanguage(e.target.value as Language | "")}
        >
          <option value="">Todos os idiomas</option>
          {(Object.keys(LANGUAGE_LABEL) as Language[]).map((value) => (
            <option key={value} value={value}>
              {LANGUAGE_LABEL[value]}
            </option>
          ))}
        </Select>
      </div>

      {error && <Alert>{error}</Alert>}
      {audio.error && <Alert tone="warning">{audio.error}</Alert>}

      {phrases.length > 0 ? (
        <ul className="divide-y divide-border overflow-hidden rounded-2xl border border-border bg-surface">
          {phrases.map((phrase) => (
            <li key={phrase.id} className="flex items-start gap-3 p-4">
              <button
                type="button"
                onClick={() => audio.play(phrase.id)}
                disabled={audio.isLoading(phrase.id)}
                aria-label={`Ouvir “${phrase.text}”`}
                title="Ouvir"
                className="mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-full bg-accent-soft text-accent-strong transition-colors hover:bg-accent hover:text-white disabled:opacity-50"
              >
                {audio.isLoading(phrase.id) ? (
                  <span className="size-3.5 animate-spin rounded-full border-2 border-current border-t-transparent" />
                ) : (
                  <PlayIcon />
                )}
              </button>
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <LanguageBadge language={phrase.language} />
                  <p className="font-medium wrap-break-word" lang={phrase.language === "Japanese" ? "ja" : "en"}>
                    {phrase.text}
                  </p>
                </div>
                {phrase.reading && (
                  <p className="mt-1 text-sm text-muted" lang="ja">
                    {phrase.reading}
                  </p>
                )}
                {phrase.translation && <p className="mt-1 text-sm">{phrase.translation}</p>}
                <p className="mt-1 text-xs text-muted">Adicionada em {formatDate(phrase.createdAt)}</p>
              </div>
              <div className="flex shrink-0 gap-1">
                <Button variant="ghost" className="px-2.5" onClick={() => openForm(phrase)}>
                  Editar
                </Button>
                <Button variant="danger" className="px-2.5" onClick={() => handleDelete(phrase)}>
                  Excluir
                </Button>
              </div>
            </li>
          ))}
        </ul>
      ) : (
        !loading && (
          <div className="rounded-2xl border border-dashed border-border p-10 text-center">
            <p className="font-medium">{filtered ? "Nenhuma frase encontrada." : "Nenhuma frase ainda."}</p>
            <p className="mt-1 text-sm text-muted">
              {filtered ? "Tente outra busca ou idioma." : "Salve frases que você ouviu ou leu e quer aprender."}
            </p>
          </div>
        )
      )}

      {loading ? (
        <div className="flex justify-center">
          <Spinner />
        </div>
      ) : (
        nextCursor && (
          <div className="flex justify-center">
            <Button variant="secondary" onClick={loadMore}>
              Carregar mais
            </Button>
          </div>
        )
      )}

      <PhraseFormDialog phrase={editing} open={formOpen} onClose={() => setFormOpen(false)} onSaved={handleSaved} />
    </div>
  );
}

function PlayIcon() {
  return (
    <svg viewBox="0 0 16 16" fill="currentColor" className="ml-0.5 size-3.5" aria-hidden>
      <path d="M4 2.5v11a.5.5 0 0 0 .76.43l9-5.5a.5.5 0 0 0 0-.86l-9-5.5A.5.5 0 0 0 4 2.5Z" />
    </svg>
  );
}
