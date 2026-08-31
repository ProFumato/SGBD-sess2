import type { ReactNode } from "react";

export function LoadingState({ label = "Loading..." }: { label?: string }) {
  return (
    <p className="feedback" role="status">
      {label}
    </p>
  );
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <p className="feedback feedback-empty">{children}</p>;
}

export function ErrorState({ children }: { children: ReactNode }) {
  return (
    <p className="feedback feedback-error" role="alert">
      {children}
    </p>
  );
}
