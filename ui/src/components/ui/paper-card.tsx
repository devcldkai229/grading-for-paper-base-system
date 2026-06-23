import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/utils";

type PaperCardVariant = "default" | "solid" | "dashed";

interface PaperCardProps extends HTMLAttributes<HTMLDivElement> {
  children: ReactNode;
  blur?: boolean;
  variant?: PaperCardVariant;
}

const variantClass: Record<PaperCardVariant, string> = {
  default: "border border-line shadow-sm",
  solid: "border border-line shadow-[0_1px_2px_rgba(27,26,25,0.05)]",
  dashed: "border border-dashed border-line shadow-[0_1px_2px_rgba(27,26,25,0.04)]",
};

export function PaperCard({
  children,
  className,
  blur = false,
  variant = "solid",
  ...props
}: PaperCardProps) {
  return (
    <div
      className={cn(
        "rounded-xl bg-card text-card-foreground",
        variantClass[variant],
        blur && "backdrop-blur-sm bg-card/95",
        className
      )}
      {...props}
    >
      {children}
    </div>
  );
}
