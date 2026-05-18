import { GraduationCap } from "lucide-react";

export function LoginBrandPanel() {
  return (
    <>
      <div className="absolute inset-0 bg-gradient-to-br from-emerald-950 via-emerald-900 to-slate-950" />
      <div
        className="absolute inset-0 opacity-[0.04]"
        style={{
          backgroundImage:
            "radial-gradient(circle at 1px 1px, white 1px, transparent 0)",
          backgroundSize: "32px 32px",
        }}
      />
      <div className="absolute -top-24 -left-24 w-96 h-96 bg-emerald-500/20 rounded-full blur-3xl animate-pulse" />
      <div className="absolute -bottom-32 -right-32 w-[500px] h-[500px] bg-emerald-600/15 rounded-full blur-3xl animate-pulse [animation-delay:2s]" />
      <div className="absolute top-1/2 left-1/3 w-64 h-64 bg-teal-500/10 rounded-full blur-3xl animate-pulse [animation-delay:4s]" />

      <div className="relative z-10 flex flex-col justify-between p-12 w-full">
        <div className="flex items-center gap-3">
          <div className="flex items-center justify-center w-10 h-10 rounded-xl bg-emerald-500/20 border border-emerald-500/30">
            <GraduationCap className="size-5 text-emerald-400" />
          </div>
          <span className="text-lg font-semibold text-white/90 tracking-tight">
            Grading System
          </span>
        </div>

        <div className="space-y-6 max-w-md">
          <h1 className="text-4xl font-bold tracking-tight text-white leading-tight">
            Paper-Based
            <br />
            <span className="text-emerald-400">Grading System</span>
          </h1>
          <p className="text-base text-white/50 leading-relaxed">
            Streamline your grading workflow with our intelligent paper-based examination
            management platform. Efficient, accurate, and secure.
          </p>
          <div className="flex items-center gap-6 text-sm text-white/40">
            <div className="flex items-center gap-2">
              <div className="w-1.5 h-1.5 rounded-full bg-emerald-400" />
              <span>Secure</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="w-1.5 h-1.5 rounded-full bg-emerald-400" />
              <span>Reliable</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="w-1.5 h-1.5 rounded-full bg-emerald-400" />
              <span>Fast</span>
            </div>
          </div>
        </div>

        <p className="text-xs text-white/30">
          (c) {new Date().getFullYear()} Grading Paper System. All rights reserved.
        </p>
      </div>
    </>
  );
}
