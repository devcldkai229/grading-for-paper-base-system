import { GoogleLogin, type CredentialResponse } from "@react-oauth/google";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { LoginForm } from "@/features/auth/components/LoginForm";
import { GraduationCap } from "lucide-react";

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
        <div className="flex items-center justify-center w-10 h-10 rounded-xl bg-primary/15 border border-primary/25">
          <GraduationCap className="size-5 text-primary" />
        </div>
        <span className="text-lg font-semibold tracking-tight">
          Grading System
        </span>
      </div>

      <Card className="border-border/60 bg-card shadow-xl shadow-black/5">
        <CardHeader className="space-y-1.5 pb-6">
          <CardTitle className="text-2xl font-bold tracking-tight">
            Welcome back
          </CardTitle>
          <CardDescription className="text-muted-foreground/80">
            Sign in to your account to continue
          </CardDescription>
        </CardHeader>

        <CardContent className="space-y-6">
          <LoginForm onSubmit={onSubmit} error={error} isLoading={isLoading} />

          <div className="relative">
            <div className="absolute inset-0 flex items-center">
              <span className="w-full border-t border-border/50" />
            </div>
            <div className="relative flex justify-center text-xs">
              <span className="bg-card px-3 text-muted-foreground/60 uppercase tracking-widest">
                or continue with
              </span>
            </div>
          </div>

          <div className="flex justify-center">
            <GoogleLogin
              onSuccess={onGoogleSuccess}
              onError={onGoogleError}
              theme="outline"
              shape="rectangular"
              size="large"
              width={380}
              text="signin_with"
              locale="en"
            />
          </div>
        </CardContent>
      </Card>

      <p className="text-center text-xs text-muted-foreground/50">
        By signing in, you agree to our Terms of Service and Privacy Policy.
      </p>
    </div>
  );
}
