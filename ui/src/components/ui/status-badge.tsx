import { cn } from "@/lib/utils";

type BadgeVariant = "default" | "ai" | "done" | "error";

interface StatusBadgeProps {
  children: React.ReactNode;
  variant?: BadgeVariant;
  className?: string;
}

const variants: Record<BadgeVariant, string> = {
  default: "bg-secondary text-ink-soft border-line",
  ai: "bg-brand-orange/10 text-brand-orange border-brand-orange/25",
  done: "bg-done/10 text-done border-done/25",
  error: "bg-destructive/10 text-destructive border-destructive/25",
};

export function StatusBadge({
  children,
  variant = "default",
  className,
}: StatusBadgeProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center px-2 py-0.5 rounded text-xs font-medium border",
        variants[variant],
        className
      )}
    >
      {children}
    </span>
  );
}
