import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

type MaxWidth = "2xl" | "5xl" | "6xl" | "7xl" | "full";

const maxWidthClass: Record<MaxWidth, string> = {
  "2xl": "max-w-2xl",
  "5xl": "max-w-5xl",
  "6xl": "max-w-6xl",
  "7xl": "max-w-7xl",
  full: "max-w-full",
};

interface LecturerPageShellProps {
  children: ReactNode;
  maxWidth?: MaxWidth;
  className?: string;
}

export function LecturerPageShell({
  children,
  maxWidth = "6xl",
  className,
}: LecturerPageShellProps) {
  return (
    <div className="p-4 sm:p-6 lg:p-8 min-h-full">
      <div
        className={cn(
          "mx-auto rounded-2xl border border-dashed border-line bg-card p-5 sm:p-6 lg:p-8",
          "shadow-[0_1px_3px_rgba(27,26,25,0.04)]",
          maxWidthClass[maxWidth],
          className
        )}
      >
        {children}
      </div>
    </div>
  );
}
