import { Check } from "lucide-react";
import { cn } from "@/lib/utils";

interface ValidScoreTickProps {
  show: boolean;
  className?: string;
}

export function ValidScoreTick({ show, className }: ValidScoreTickProps) {
  if (!show) return <span className={cn("w-5 h-5 shrink-0", className)} />;

  return (
    <span
      className={cn(
        "flex items-center justify-center w-5 h-5 shrink-0 text-brand-red animate-in fade-in zoom-in-75 duration-200",
        className
      )}
      aria-label="Điểm hợp lệ"
    >
      <Check className="h-4 w-4 stroke-[2.5]" />
    </span>
  );
}
