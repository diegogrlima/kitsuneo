"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect } from "react";
import { Button, Spinner } from "@/components/ui";
import { useAuth } from "@/lib/auth";

const NAV = [
  { href: "/", label: "Início" },
  { href: "/practice", label: "Praticar" },
  { href: "/phrases", label: "Frases" },
];

export function AppShell({ children }: { children: React.ReactNode }) {
  const { status, user, logout } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (status === "anonymous") router.replace("/login");
  }, [status, router]);

  if (status !== "authenticated") {
    return (
      <div className="flex flex-1 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  return (
    <>
      <header className="sticky top-0 z-10 border-b border-border bg-background/85 backdrop-blur">
        <div className="mx-auto flex h-14 max-w-4xl items-center gap-2 px-4">
          <Link href="/" className="mr-2 flex items-center gap-1.5 font-semibold tracking-tight sm:mr-4">
            <span aria-hidden>🦊</span>
            <span className="hidden sm:inline">Kitsuneo</span>
          </Link>
          <nav className="flex gap-1">
            {NAV.map((item) => {
              const active = item.href === "/" ? pathname === "/" : pathname.startsWith(item.href);
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  aria-current={active ? "page" : undefined}
                  className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
                    active ? "bg-accent-soft text-accent-strong" : "text-muted hover:bg-surface-muted hover:text-foreground"
                  }`}
                >
                  {item.label}
                </Link>
              );
            })}
          </nav>
          <div className="ml-auto flex items-center gap-2">
            <span className="hidden max-w-48 truncate text-sm text-muted md:inline" title={user?.email}>
              {user?.email}
            </span>
            <Button variant="ghost" className="px-3" onClick={() => logout()}>
              Sair
            </Button>
          </div>
        </div>
      </header>
      <main className="mx-auto w-full max-w-4xl flex-1 px-4 py-8">{children}</main>
    </>
  );
}
