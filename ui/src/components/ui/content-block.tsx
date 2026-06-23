import type { ButtonHTMLAttributes, HTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/utils";
import {
  contentBlockClass,
  contentBlockInteractiveClass,
} from "@/components/ui/content-block-styles";

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

type ContentBlockButtonProps = ButtonHTMLAttributes<HTMLButtonElement>;

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
