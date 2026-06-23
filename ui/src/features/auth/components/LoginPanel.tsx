import { GoogleLogin, type CredentialResponse } from "@react-oauth/google";
import { LoginForm } from "@/features/auth/components/LoginForm";
import { GraduationCap } from "lucide-react";
import { PaperCard } from "@/components/ui/paper-card";

interface LoginPanelProps {
  onSubmit: (email: string, password: string, rememberMe: boolean) => Promise<void>;
  error: string | null;
  isLoading: boolean;
  onGoogleSuccess: (credentialResponse: CredentialResponse) => void;
  onGoogleError: () => void;
}

export function LoginPanel({
  onSubmit,
  error,
  isLoading,
  onGoogleSuccess,
  onGoogleError,
}: LoginPanelProps) {
  return (
    <div className="w-full max-w-[420px] space-y-8">
      <div className="flex items-center gap-3 lg:hidden">
        <div className="flex items-center justify-center w-10 h-10 rounded-xl bg-brand-red/10 border border-brand-red/20">
          <GraduationCap className="size-5 text-brand-red" />
        </div>
        <span className="font-display text-lg font-semibold tracking-tight text-ink">
          GradePaper
        </span>
      </div>

      <PaperCard blur className="shadow-md">
        <div className="p-6 space-y-6">
          <div className="space-y-1.5">
            <h2 className="font-display text-2xl font-semibold tracking-tight text-ink">
              Sign In
            </h2>
            <p className="text-sm text-ink-soft">
              Sign in to grade exams and manage exams
            </p>
          </div>

          <LoginForm onSubmit={onSubmit} error={error} isLoading={isLoading} />

          <div className="relative">
            <div className="absolute inset-0 flex items-center">
              <span className="w-full border-t border-line" />
            </div>
            <div className="relative flex justify-center text-xs">
              <span className="bg-card px-3 text-ink-soft uppercase tracking-widest">
                or
              </span>
            </div>
          </div>

          <div className="flex justify-center [&>div]:w-full [&>div]:flex [&>div]:justify-center">
            <GoogleLogin
              onSuccess={onGoogleSuccess}
              onError={onGoogleError}
              theme="outline"
              shape="rectangular"
              size="large"
              width={340}
              text="signin_with"
            />
          </div>
        </div>
      </PaperCard>

      <p className="text-center text-xs text-ink-soft/60">
        By signing in, you agree to the terms of use.
      </p>
    </div>
  );
}
