"use client";

import { createContext, use, useCallback, useEffect, useMemo, useState } from "react";
import { api, onSessionLost, refreshSession, setAccessToken } from "./api";
import type { AuthResponse, User } from "./types";

type AuthStatus = "loading" | "authenticated" | "anonymous";

interface AuthContextValue {
  status: AuthStatus;
  user: User | null;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [status, setStatus] = useState<AuthStatus>("loading");

  const start = useCallback((session: AuthResponse) => {
    setAccessToken(session.accessToken);
    setUser(session.user);
    setStatus("authenticated");
  }, []);

  const end = useCallback(() => {
    setAccessToken(null);
    setUser(null);
    setStatus("anonymous");
  }, []);

  // The access token is lost on reload; the refresh cookie brings the session back
  useEffect(() => {
    let active = true;
    refreshSession()
      .then((session) => {
        if (!active) return;
        if (session) start(session);
        else end();
      })
      .catch(() => active && end());

    const unsubscribe = onSessionLost(end);
    return () => {
      active = false;
      unsubscribe();
    };
  }, [start, end]);

  const value = useMemo<AuthContextValue>(
    () => ({
      status,
      user,
      login: async (email, password) => start(await api.auth.login(email, password)),
      register: async (email, password) => start(await api.auth.register(email, password)),
      logout: async () => {
        try {
          await api.auth.logout();
        } finally {
          end();
        }
      },
    }),
    [status, user, start, end],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}

export function useAuth(): AuthContextValue {
  const context = use(AuthContext);
  if (!context) throw new Error("useAuth must be used inside <AuthProvider>.");
  return context;
}
