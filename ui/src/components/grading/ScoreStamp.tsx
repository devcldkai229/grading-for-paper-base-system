import { useEffect, useState } from "react";

interface ScoreStampProps {
  total: number;
  max: number;
  label?: string;
}

function HandUnderline() {
  return (
    <svg
      className="w-full h-2 text-brand-red"
      viewBox="0 0 120 8"
      fill="none"
      preserveAspectRatio="none"
      aria-hidden
    >
      <path
        d="M1 5.5 C 25 2, 45 7, 70 4.5 S 100 6, 119 3.5"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}

export function ScoreStamp({ total, max, label = "TỔNG" }: ScoreStampProps) {
  const [displayTotal, setDisplayTotal] = useState(total);
  const reducedMotion =
    typeof window !== "undefined" &&
    window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  useEffect(() => {
    if (reducedMotion) {
      setDisplayTotal(total);
      return;
    }
    const start = displayTotal;
    const diff = total - start;
    if (Math.abs(diff) < 0.01) return;

    const steps = 12;
    let step = 0;
    const id = window.setInterval(() => {
      step += 1;
      const t = step / steps;
      setDisplayTotal(start + diff * t);
      if (step >= steps) {
        setDisplayTotal(total);
        clearInterval(id);
      }
    }, 24);
    return () => clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [total, reducedMotion]);

  return (
    <div className="pt-4 border-t border-line">
      <p className="text-xs font-medium text-ink-soft uppercase tracking-wider mb-1">
        {label}
      </p>
      <div className="font-score text-3xl font-medium text-brand-red">
        {displayTotal.toFixed(1)}
        <span className="text-ink-soft text-xl"> / {max}</span>
      </div>
      <div className="max-w-[140px] mt-1">
        <HandUnderline />
      </div>
    </div>
  );
}
