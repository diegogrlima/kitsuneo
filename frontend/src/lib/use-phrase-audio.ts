"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { api, ApiError } from "./api";

/**
 * Plays a phrase's audio. Each MP3 is downloaded once (the backend also caches it, but every
 * request still costs a round trip) and kept as an object URL until the component unmounts.
 */
export function usePhraseAudio() {
  const urls = useRef(new Map<string, string>());
  const audio = useRef<HTMLAudioElement | null>(null);
  const [loadingKey, setLoadingKey] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const cache = urls.current;
    return () => {
      audio.current?.pause();
      cache.forEach((url) => URL.revokeObjectURL(url));
      cache.clear();
    };
  }, []);

  const play = useCallback(async (phraseId: string, slow = false): Promise<boolean> => {
    const key = `${phraseId}:${slow}`;
    setError(null);

    let url = urls.current.get(key);
    if (!url) {
      setLoadingKey(key);
      try {
        url = URL.createObjectURL(await api.phrases.audio(phraseId, slow));
        urls.current.set(key, url);
      } catch (e) {
        setError(
          e instanceof ApiError && e.status === 503
            ? "A voz não está disponível agora. Verifique a chave do ElevenLabs no backend."
            : "Não foi possível carregar o áudio.",
        );
        return false;
      } finally {
        setLoadingKey(null);
      }
    }

    audio.current?.pause();
    audio.current = new Audio(url);
    try {
      await audio.current.play();
      return true;
    } catch {
      setError("O navegador bloqueou o áudio. Clique para tocar de novo.");
      return false;
    }
  }, []);

  const isLoading = useCallback(
    (phraseId: string, slow = false) => loadingKey === `${phraseId}:${slow}`,
    [loadingKey],
  );

  return { play, isLoading, error };
}
