import type { ReactNode } from "react";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import { PageHeader } from "@/components/layout/PageHeader";

interface CatalogPageShellProps {
  title: string;
  subtitle?: string;
  breadcrumb?: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
}

export function CatalogPageShell({
  title,
  subtitle,
  breadcrumb,
  actions,
  children,
}: CatalogPageShellProps) {
  return (
    <LecturerPageShell>
      {breadcrumb}
      <PageHeader title={title} subtitle={subtitle} actions={actions} />
      {children}
    </LecturerPageShell>
  );
}

export function CatalogListSkeleton() {
  return (
    <div className="flex justify-center py-20 rounded-xl border border-dashed border-line bg-secondary/30">
      <div className="animate-spin w-8 h-8 border-2 border-line border-t-brand-red rounded-full" />
    </div>
  );
}

export function CatalogEmptyState({ message }: { message: string }) {
  return (
    <div className="text-center py-20 text-ink-soft rounded-xl border border-dashed border-line bg-secondary/30 px-6">
      {message}
    </div>
  );
}

export function CatalogErrorState({
  message,
  onRetry,
}: {
  message: string;
  onRetry?: () => void;
}) {
  return (
    <div className="rounded-xl border border-destructive/30 bg-destructive/5 px-5 py-8 text-center shadow-sm">
      <p className="text-destructive text-sm">{message}</p>
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-4 px-4 py-2 rounded-lg bg-primary text-primary-foreground text-sm hover:opacity-90 transition-opacity"
        >
          Thử lại
        </button>
      )}
    </div>
  );
}
