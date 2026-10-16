"use client";

import { useEffect, useRef, useState } from "react";
import { Alert, Button, Field, Input, Select, Textarea } from "@/components/ui";
import { api } from "@/lib/api";
import { errorMessage, LANGUAGE_LABEL } from "@/lib/format";
import type { Language, Phrase, PhraseInput } from "@/lib/types";

const MAX_LENGTH = 500;

interface PhraseFormDialogProps {
  /** The phrase to edit; null to create one. */
  phrase: Phrase | null;
  open: boolean;
  onClose: () => void;
  onSaved: (phrase: Phrase, created: boolean) => void;
}

export function PhraseFormDialog({ phrase, open, onClose, onSaved }: PhraseFormDialogProps) {
  const dialog = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const element = dialog.current;
    if (!element) return;
    if (open && !element.open) element.showModal();
    if (!open && element.open) element.close();
  }, [open]);

  return (
    <dialog
      ref={dialog}
      onClose={onClose}
      className="m-auto w-[calc(100%-2rem)] max-w-lg rounded-2xl border border-border bg-surface p-0 text-foreground backdrop:bg-black/40"
    >
      {/* Remounted on each opening so the fields start from the phrase being edited */}
      {open && <PhraseForm phrase={phrase} onCancel={onClose} onSaved={onSaved} />}
    </dialog>
  );
}

function PhraseForm({
  phrase,
  onCancel,
  onSaved,
}: {
  phrase: Phrase | null;
  onCancel: () => void;
  onSaved: (phrase: Phrase, created: boolean) => void;
}) {
  const [language, setLanguage] = useState<Language>(phrase?.language ?? "English");
  const [text, setText] = useState(phrase?.text ?? "");
  // The backend fills SpokenForm with the text when it is left empty; show it empty in that case too
  const [spokenForm, setSpokenForm] = useState(phrase && phrase.spokenForm !== phrase.text ? phrase.spokenForm : "");
  const [reading, setReading] = useState(phrase?.reading ?? "");
  const [translation, setTranslation] = useState(phrase?.translation ?? "");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const isJapanese = language === "Japanese";

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setPending(true);

    const input: PhraseInput = {
      language,
      text,
      spokenForm: spokenForm || null,
      reading: isJapanese ? reading || null : null,
      translation: translation || null,
    };

    try {
      if (phrase) {
        await api.phrases.update(phrase.id, input);
        // The backend normalizes the fields, so read the saved version back
        onSaved(await api.phrases.get(phrase.id), false);
      } else {
        onSaved(await api.phrases.create(input), true);
      }
    } catch (e) {
      setError(errorMessage(e));
      setPending(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4 p-6">
      <h2 className="text-lg font-semibold">{phrase ? "Editar frase" : "Nova frase"}</h2>

      <Field label="Idioma" htmlFor="language">
        <Select id="language" value={language} onChange={(e) => setLanguage(e.target.value as Language)}>
          {(Object.keys(LANGUAGE_LABEL) as Language[]).map((value) => (
            <option key={value} value={value}>
              {LANGUAGE_LABEL[value]}
            </option>
          ))}
        </Select>
      </Field>

      <Field label="Frase" htmlFor="text">
        <Textarea
          id="text"
          rows={2}
          required
          maxLength={MAX_LENGTH}
          autoFocus
          lang={isJapanese ? "ja" : "en"}
          placeholder={isJapanese ? "今日はいい天気ですね。" : "It's a nice day today."}
          value={text}
          onChange={(e) => setText(e.target.value)}
        />
      </Field>

      {isJapanese && (
        <Field label="Leitura (kana)" htmlFor="reading" hint="O ditado em japonês é conferido com a leitura, quando houver.">
          <Input
            id="reading"
            lang="ja"
            maxLength={MAX_LENGTH}
            placeholder="きょうはいいてんきですね。"
            value={reading}
            onChange={(e) => setReading(e.target.value)}
          />
        </Field>
      )}

      <Field label="Forma falada" htmlFor="spokenForm" hint="Opcional. O que a voz lê, se for diferente da frase escrita.">
        <Input
          id="spokenForm"
          lang={isJapanese ? "ja" : "en"}
          maxLength={MAX_LENGTH}
          value={spokenForm}
          onChange={(e) => setSpokenForm(e.target.value)}
        />
      </Field>

      <Field label="Tradução" htmlFor="translation">
        <Input
          id="translation"
          maxLength={MAX_LENGTH}
          placeholder="O dia está bonito hoje."
          value={translation}
          onChange={(e) => setTranslation(e.target.value)}
        />
      </Field>

      {error && <Alert>{error}</Alert>}

      <div className="flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onCancel}>
          Cancelar
        </Button>
        <Button type="submit" disabled={pending}>
          {pending ? "Salvando…" : "Salvar"}
        </Button>
      </div>
    </form>
  );
}
