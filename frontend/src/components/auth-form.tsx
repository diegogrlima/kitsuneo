"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { Alert, Button, Field, Input } from "@/components/ui";
import { useAuth } from "@/lib/auth";
import { errorMessage } from "@/lib/format";

const COPY = {
  login: {
    title: "Entrar",
    submit: "Entrar",
    pending: "Entrando",
    switchText: "Ainda não tem conta?",
    switchLink: "Criar conta",
    switchHref: "/register",
  },
  register: {
    title: "Criar conta",
    submit: "Criar conta",
    pending: "Criando",
    switchText: "Já tem conta?",
    switchLink: "Entrar",
    switchHref: "/login",
  },
} as const;

export function AuthForm({ mode }: { mode: "login" | "register" }) {
  const copy = COPY[mode];
  const { status, login, register } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    if (status === "authenticated") router.replace("/");
  }, [status, router]);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setPending(true);
    try {
      await (mode === "login" ? login : register)(email, password);
    } catch (e) {
      setError(errorMessage(e));
      setPending(false);
    }
  }

  return (
    <main className="flex flex-1 items-center justify-center px-4 py-12">
      <div className="w-full max-w-sm">
        <div className="mb-8 text-center">
          <p className="text-4xl" aria-hidden>
            🦊
          </p>
          <h1 className="mt-2 text-2xl font-semibold tracking-tight">Kitsuneo</h1>
          <p className="mt-1 text-sm text-muted">Frases em inglês e japonês, no seu ritmo.</p>
        </div>

        <form onSubmit={handleSubmit} className="flex flex-col gap-4 rounded-2xl border border-border bg-surface p-6">
          <h2 className="text-lg font-semibold">{copy.title}</h2>

          <Field label="E-mail" htmlFor="email">
            <Input
              id="email"
              type="email"
              autoComplete="email"
              required
              maxLength={254}
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </Field>

          <Field
            label="Senha"
            htmlFor="password"
            hint={mode === "register" ? "Entre 8 e 128 caracteres." : undefined}
          >
            <Input
              id="password"
              type="password"
              autoComplete={mode === "login" ? "current-password" : "new-password"}
              required
              minLength={mode === "register" ? 8 : undefined}
              maxLength={128}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </Field>

          {error && <Alert>{error}</Alert>}

          <Button type="submit" disabled={pending}>
            {pending ? `${copy.pending}…` : copy.submit}
          </Button>
        </form>

        <p className="mt-4 text-center text-sm text-muted">
          {copy.switchText}{" "}
          <Link href={copy.switchHref} className="font-medium text-accent hover:underline">
            {copy.switchLink}
          </Link>
        </p>
      </div>
    </main>
  );
}
