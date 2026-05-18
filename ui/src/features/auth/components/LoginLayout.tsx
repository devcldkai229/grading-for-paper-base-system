import type { ReactNode } from "react";

interface LoginLayoutProps {
  left: ReactNode;
  right: ReactNode;
}

export function LoginLayout({ left, right }: LoginLayoutProps) {
  return (
    <div className="min-h-svh flex">
      <div className="hidden lg:flex lg:w-1/2 relative overflow-hidden">
        {left}
      </div>
      <div className="flex-1 flex items-center justify-center p-6 sm:p-8 lg:p-12 bg-background">
        {right}
      </div>
    </div>
  );
}
