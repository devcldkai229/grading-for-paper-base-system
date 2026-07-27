import type { ReactNode } from "react";
import { LoginParticleBackground } from "@/components/background/LoginParticleBackground";

interface LoginLayoutProps {
  left: ReactNode;
  right: ReactNode;
}

export function LoginLayout({ left, right }: LoginLayoutProps) {
  return (
    <div className="relative min-h-svh flex bg-white overflow-hidden">
      <LoginParticleBackground />
      <div className="hidden lg:flex lg:w-1/2 relative z-[2]">
        {left}
      </div>
      <div className="flex-1 flex items-center justify-center p-6 sm:p-8 lg:p-12 relative z-[2]">
        {right}
      </div>
    </div>
  );
}
