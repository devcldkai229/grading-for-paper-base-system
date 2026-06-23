import { GraduationCap } from "lucide-react";

export function LoginBrandPanel() {
  return (
    <div className="relative z-10 flex flex-col justify-between p-12 w-full h-full">
      <div className="flex items-center gap-3">
        <div className="flex items-center justify-center w-10 h-10 rounded-xl bg-brand-red/10 border border-brand-red/20">
          <GraduationCap className="size-5 text-brand-red" />
        </div>
        <span className="font-display text-lg font-semibold text-ink tracking-tight">
          FGrader
        </span>
      </div>

      <div className="space-y-6 max-w-md">
        <h1 className="font-display text-4xl font-bold tracking-tight text-ink leading-tight">
          FGrader
          <br />
          <span className="text-brand-red">grading for paper-based</span>
        </h1>
        <p className="text-base text-ink-soft leading-relaxed">
          The platform for grading paper-based exams with AI assistance.
        </p>
        <div className="flex items-center gap-6 text-sm text-ink-soft">
          <div className="flex items-center gap-2">
            <div className="w-1.5 h-1.5 rounded-full bg-brand-red" />
            <span>Manual</span>
          </div>
          <div className="flex items-center gap-2">
            <div className="w-1.5 h-1.5 rounded-full bg-brand-orange" />
            <span>Recommendation</span>
          </div>
          <div className="flex items-center gap-2">
            <div className="w-1.5 h-1.5 rounded-full bg-done" />
            <span>Continuous</span>
          </div>
        </div>
      </div>

      <p className="text-xs text-ink-soft/60">
        © {new Date().getFullYear()} FGrader
      </p>
    </div>
  );
}
