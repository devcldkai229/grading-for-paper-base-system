import { useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { type CredentialResponse } from "@react-oauth/google";
import { LoginBrandPanel } from "@/features/auth/components/LoginBrandPanel";
import { LoginLayout } from "@/features/auth/components/LoginLayout";
import { LoginPanel } from "@/features/auth/components/LoginPanel";
import { authService } from "@/services/authService";
import { resolvePostLoginPath } from "@/lib/roles";

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from;

  const navigateAfterAuth = () => {
    const role = authService.decodeToken()?.role;
    navigate(resolvePostLoginPath(from, role), { replace: true });
  };
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const handleLogin = async (email: string, password: string, rememberMe: boolean) => {
    setError(null);
    setIsLoading(true);
    try {
      const result = await authService.login(email, password, rememberMe);
      if (result.success) {
        navigateAfterAuth();
      } else {
        setError(result.errors?.[0] || "Login failed. Please try again.");
      }
    } catch {
      setError("Unable to connect to server. Please try again later.");
    } finally {
      setIsLoading(false);
    }
  };

  const handleGoogleSuccess = async (credentialResponse: CredentialResponse) => {
    if (!credentialResponse.credential) {
      setError("Google login failed. No credential received.");
      return;
    }

    setError(null);
    setIsLoading(true);
    try {
      const result = await authService.googleLogin(credentialResponse.credential);
      if (result.success) {
        navigateAfterAuth();
      } else {
        setError(result.errors?.[0] || "Google login failed.");
      }
    } catch {
      setError("Unable to connect to server. Please try again later.");
    } finally {
      setIsLoading(false);
    }
  };

  const handleGoogleError = () => {
    setError("Google login failed. Please try again.");
  };

  return (
    <LoginLayout
      left={<LoginBrandPanel />}
      right={
        <LoginPanel
          onSubmit={handleLogin}
          error={error}
          isLoading={isLoading}
          onGoogleSuccess={handleGoogleSuccess}
          onGoogleError={handleGoogleError}
        />
      }
    />
  );
}
