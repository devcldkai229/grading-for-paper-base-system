import type { ButtonHTMLAttributes, HTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/utils";

export const contentBlockClass =
  "rounded-xl border border-line bg-card shadow-[0_1px_2px_rgba(27,26,25,0.04)]";

export const contentBlockInteractiveClass = cn(
  contentBlockClass,
  "transition-all duration-200 hover:border-brand-red/50 hover:bg-brand-red/[0.03] hover:shadow-[0_2px_8px_rgba(215,38,61,0.08)]"
);

interface ContentBlockProps extends HTMLAttributes<HTMLDivElement> {
  interactive?: boolean;
}

export function ContentBlock({
  interactive = false,
  className,
  children,
  ...props
}: ContentBlockProps) {
  return (
    <div
      className={cn(
        interactive ? contentBlockInteractiveClass : contentBlockClass,
        className
      )}
      {...props}
    >
      {children}
    </div>
  );
}

interface ContentBlockButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {}

export function ContentBlockButton({
  className,
  children,
  type = "button",
  ...props
}: ContentBlockButtonProps) {
  return (
    <button
      type={type}
      className={cn(
        contentBlockInteractiveClass,
        "w-full text-left p-5",
        className
      )}
      {...props}
    >
      {children}
    </button>
  );
}

interface FilterBarProps {
  children: ReactNode;
  className?: string;
}

export function FilterBar({ children, className }: FilterBarProps) {
  return (
    <div
      className={cn(
        "flex flex-wrap gap-2 mb-6 p-3 rounded-xl border border-dashed border-line bg-secondary/50",
        className
      )}
    >
      {children}
    </div>
  );
}
