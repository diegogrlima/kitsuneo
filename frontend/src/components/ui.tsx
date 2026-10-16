import type { ComponentProps } from "react";

type Variant = "primary" | "secondary" | "ghost" | "danger";

const VARIANTS: Record<Variant, string> = {
  primary: "bg-accent text-white hover:bg-accent-strong",
  secondary: "border border-border bg-surface hover:bg-surface-muted",
  ghost: "hover:bg-surface-muted",
  danger: "text-danger hover:bg-danger-soft",
};

export function Button({
  variant = "primary",
  className = "",
  ...props
}: ComponentProps<"button"> & { variant?: Variant }) {
  return (
    <button
      className={`inline-flex items-center justify-center gap-2 rounded-lg px-4 py-2 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:cursor-not-allowed disabled:opacity-50 ${VARIANTS[variant]} ${className}`}
      {...props}
    />
  );
}

const FIELD =
  "w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm placeholder:text-muted focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/25";

export function Input({ className = "", ...props }: ComponentProps<"input">) {
  return <input className={`${FIELD} ${className}`} {...props} />;
}

export function Textarea({ className = "", ...props }: ComponentProps<"textarea">) {
  return <textarea className={`${FIELD} resize-y ${className}`} {...props} />;
}

export function Select({ className = "", ...props }: ComponentProps<"select">) {
  return <select className={`${FIELD} ${className}`} {...props} />;
}

export function Field({
  label,
  hint,
  htmlFor,
  children,
}: {
  label: string;
  hint?: string;
  htmlFor: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={htmlFor} className="text-sm font-medium">
        {label}
      </label>
      {children}
      {hint && <p className="text-xs text-muted">{hint}</p>}
    </div>
  );
}

export function Alert({ tone = "danger", children }: { tone?: "danger" | "warning"; children: React.ReactNode }) {
  const styles = tone === "danger" ? "bg-danger-soft text-danger" : "bg-warning-soft text-warning";
  return (
    <p role="alert" className={`rounded-lg px-3 py-2 text-sm ${styles}`}>
      {children}
    </p>
  );
}

export function Card({ className = "", children }: { className?: string; children: React.ReactNode }) {
  return <div className={`rounded-2xl border border-border bg-surface p-6 ${className}`}>{children}</div>;
}

export function Spinner({ label = "Carregando" }: { label?: string }) {
  return (
    <span role="status" className="inline-flex items-center gap-2 text-sm text-muted">
      <span className="size-4 animate-spin rounded-full border-2 border-border border-t-accent" />
      {label}…
    </span>
  );
}

export function LanguageBadge({ language }: { language: "English" | "Japanese" }) {
  return (
    <span className="rounded-md bg-surface-muted px-1.5 py-0.5 text-xs font-medium text-muted">
      {language === "English" ? "EN" : "JA"}
    </span>
  );
}
