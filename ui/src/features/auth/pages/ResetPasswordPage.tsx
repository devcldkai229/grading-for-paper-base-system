import { useState, useMemo, useEffect } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { isAxiosError } from "axios";
import { authService } from "@/services/authService";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardFooter } from "@/components/ui/card";
import { 
  Loader2, 
  ArrowLeft, 
  ShieldAlert, 
  KeyRound, 
  CheckCircle2, 
  XCircle, 
  Lock, 
  Key,
  Eye,
  EyeOff
} from "lucide-react";

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();

  const [token, setToken] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Automatically read token from search params if present
  useEffect(() => {
    const tokenParam = searchParams.get("token");
    if (tokenParam) {
      setToken(tokenParam);
    }
  }, [searchParams]);

  // Real-time password strength check
  const passwordCriteria = useMemo(() => {
    return {
      minLength: newPassword.length >= 8,
      hasUpper: /[A-Z]/.test(newPassword),
      hasLower: /[a-z]/.test(newPassword),
      hasDigit: /[0-9]/.test(newPassword),
      hasSpecial: /[^A-Za-z0-9]/.test(newPassword),
    };
  }, [newPassword]);

  const isPasswordFormValid = useMemo(() => {
    const isStrengthValid = Object.values(passwordCriteria).every(Boolean);
    const isMatch = newPassword === confirmPassword && newPassword !== "";
    const hasToken = token.trim() !== "";
    return isStrengthValid && isMatch && hasToken;
  }, [passwordCriteria, newPassword, confirmPassword, token]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isPasswordFormValid) return;

    setLoading(true);
    setError(null);
    setSuccess(null);

    try {
      const res = await authService.resetPassword(token.trim(), newPassword);
      if (res.success) {
        setSuccess("Đặt lại mật khẩu thành công! Bạn có thể sử dụng mật khẩu mới để đăng nhập.");
        setToken("");
        setNewPassword("");
        setConfirmPassword("");
      } else {
        setError(res.errors.join(", ") || "Đặt lại mật khẩu thất bại.");
      }
    } catch (err) {
      const apiMsg = isAxiosError(err) ? err.response?.data?.message : null;
      setError(apiMsg || "Đặt lại mật khẩu thất bại. Token có thể đã hết hạn hoặc không hợp lệ.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-paper via-background to-card flex flex-col justify-center py-12 sm:px-6 lg:px-8">
      <div className="sm:mx-auto sm:w-full sm:max-w-md">
        <div className="flex justify-center">
          <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-brand-red/10 border border-brand-red/20 shadow-sm">
            <KeyRound className="h-6 w-6 text-brand-red" />
          </div>
        </div>
        <h2 className="mt-6 text-center text-3xl font-display font-extrabold tracking-tight text-ink">
          Đặt lại mật khẩu mới
        </h2>
        <p className="mt-2 text-center text-sm text-ink-soft">
          Nhập mã khôi phục và mật khẩu mới của bạn bên dưới.
        </p>
      </div>

      <div className="mt-8 sm:mx-auto sm:w-full sm:max-w-md px-4">
        <Card className="shadow-xl border-line backdrop-blur-md bg-background/95">
          <CardContent className="pt-6">
            {error && (
              <div className="mb-4 rounded-lg bg-rose-500/10 border border-rose-500/20 p-3.5 text-sm text-rose-600 dark:text-rose-400 flex items-start gap-2.5">
                <ShieldAlert className="h-5 w-5 shrink-0 mt-0.5" />
                <span>{error}</span>
              </div>
            )}

            {success ? (
              <div className="space-y-6 text-center py-2">
                <div className="rounded-lg bg-emerald-500/10 border border-emerald-500/20 p-4 text-sm text-emerald-600 dark:text-emerald-400 flex flex-col items-center gap-3">
                  <CheckCircle2 className="h-10 w-10 text-emerald-500 animate-bounce" />
                  <div>
                    <h4 className="font-semibold text-emerald-800 dark:text-emerald-300 text-base">Thành công!</h4>
                    <p className="mt-1 text-sm text-emerald-700/90 dark:text-emerald-400/90">
                      {success}
                    </p>
                  </div>
                </div>

                <Link to="/login" className="block">
                  <Button className="w-full bg-brand-red text-white hover:bg-brand-red/90 font-semibold py-2.5 rounded-lg shadow-md transition-all">
                    Đăng nhập ngay
                  </Button>
                </Link>
              </div>
            ) : (
              <form onSubmit={handleSubmit} className="space-y-4">
                {/* Reset Token Input */}
                <div className="space-y-2">
                  <Label htmlFor="token" className="text-sm font-medium text-ink-soft">
                    Mã khôi phục (Token)
                  </Label>
                  <div className="relative">
                    <Input
                      id="token"
                      required
                      placeholder="Nhập mã token nhận được..."
                      value={token}
                      onChange={(e) => setToken(e.target.value)}
                      className="pl-10 h-10 rounded-lg font-mono text-xs"
                      disabled={loading}
                    />
                    <Key className="absolute left-3.5 top-3 h-4 w-4 text-ink-soft pointer-events-none" />
                  </div>
                </div>

                {/* New Password Input */}
                <div className="space-y-2">
                  <Label htmlFor="newPassword" className="text-sm font-medium text-ink-soft">
                    Mật khẩu mới
                  </Label>
                  <div className="relative">
                    <Input
                      id="newPassword"
                      type={showPassword ? "text" : "password"}
                      required
                      placeholder="••••••••"
                      value={newPassword}
                      onChange={(e) => setNewPassword(e.target.value)}
                      className="pl-10 pr-10 h-10 rounded-lg"
                      disabled={loading}
                    />
                    <Lock className="absolute left-3.5 top-3 h-4 w-4 text-ink-soft pointer-events-none" />
                    <button
                      type="button"
                      onClick={() => setShowPassword(!showPassword)}
                      className="absolute right-3 top-3 text-ink-soft hover:text-ink transition-colors"
                    >
                      {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                    </button>
                  </div>
                </div>

                {/* Confirm Password Input */}
                <div className="space-y-2">
                  <Label htmlFor="confirmPassword" className="text-sm font-medium text-ink-soft">
                    Xác nhận mật khẩu mới
                  </Label>
                  <div className="relative">
                    <Input
                      id="confirmPassword"
                      type={showPassword ? "text" : "password"}
                      required
                      placeholder="••••••••"
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      className="pl-10 h-10 rounded-lg"
                      disabled={loading}
                    />
                    <Lock className="absolute left-3.5 top-3 h-4 w-4 text-ink-soft pointer-events-none" />
                  </div>
                </div>

                {/* Real-time Validation Checklist */}
                {newPassword && (
                  <div className="rounded-lg bg-card border border-line p-3 text-xs space-y-2">
                    <p className="font-semibold text-ink-soft mb-1.5">Độ mạnh mật khẩu mới:</p>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                      <div className="flex items-center gap-1.5">
                        {passwordCriteria.minLength ? (
                          <CheckCircle2 className="h-4 w-4 text-emerald-500 shrink-0" />
                        ) : (
                          <XCircle className="h-4 w-4 text-rose-400 shrink-0" />
                        )}
                        <span className={passwordCriteria.minLength ? "text-emerald-600 dark:text-emerald-400" : "text-ink-soft"}>
                          Ít nhất 8 ký tự
                        </span>
                      </div>

                      <div className="flex items-center gap-1.5">
                        {passwordCriteria.hasUpper ? (
                          <CheckCircle2 className="h-4 w-4 text-emerald-500 shrink-0" />
                        ) : (
                          <XCircle className="h-4 w-4 text-rose-400 shrink-0" />
                        )}
                        <span className={passwordCriteria.hasUpper ? "text-emerald-600 dark:text-emerald-400" : "text-ink-soft"}>
                          1 chữ hoa (A-Z)
                        </span>
                      </div>

                      <div className="flex items-center gap-1.5">
                        {passwordCriteria.hasLower ? (
                          <CheckCircle2 className="h-4 w-4 text-emerald-500 shrink-0" />
                        ) : (
                          <XCircle className="h-4 w-4 text-rose-400 shrink-0" />
                        )}
                        <span className={passwordCriteria.hasLower ? "text-emerald-600 dark:text-emerald-400" : "text-ink-soft"}>
                          1 chữ thường (a-z)
                        </span>
                      </div>

                      <div className="flex items-center gap-1.5">
                        {passwordCriteria.hasDigit ? (
                          <CheckCircle2 className="h-4 w-4 text-emerald-500 shrink-0" />
                        ) : (
                          <XCircle className="h-4 w-4 text-rose-400 shrink-0" />
                        )}
                        <span className={passwordCriteria.hasDigit ? "text-emerald-600 dark:text-emerald-400" : "text-ink-soft"}>
                          1 chữ số (0-9)
                        </span>
                      </div>

                      <div className="flex items-center gap-1.5 sm:col-span-2 font-medium">
                        {passwordCriteria.hasSpecial ? (
                          <CheckCircle2 className="h-4 w-4 text-emerald-500 shrink-0" />
                        ) : (
                          <XCircle className="h-4 w-4 text-rose-400 shrink-0" />
                        )}
                        <span className={passwordCriteria.hasSpecial ? "text-emerald-600 dark:text-emerald-400" : "text-ink-soft"}>
                          1 ký tự đặc biệt (!@#$...)
                        </span>
                      </div>
                    </div>

                    <div className="border-t border-line/60 pt-2 flex items-center gap-1.5 font-medium">
                      {newPassword === confirmPassword && confirmPassword !== "" ? (
                        <CheckCircle2 className="h-4 w-4 text-emerald-500 shrink-0" />
                      ) : (
                        <XCircle className="h-4 w-4 text-rose-400 shrink-0" />
                      )}
                      <span className={newPassword === confirmPassword && confirmPassword !== "" ? "text-emerald-600 dark:text-emerald-400" : "text-ink-soft"}>
                        Xác nhận mật khẩu khớp nhau
                      </span>
                    </div>
                  </div>
                )}

                <Button
                  type="submit"
                  disabled={loading || !isPasswordFormValid}
                  className={`w-full font-semibold py-2.5 rounded-lg shadow-md transition-all flex items-center justify-center gap-2 ${
                    isPasswordFormValid 
                      ? 'bg-brand-red text-white hover:bg-brand-red/90' 
                      : 'bg-card text-ink-soft border border-line cursor-not-allowed'
                  }`}
                >
                  {loading ? (
                    <>
                      <Loader2 className="h-5 w-5 animate-spin" />
                      Đang xử lý đặt lại...
                    </>
                  ) : (
                    "Đặt lại mật khẩu mới"
                  )}
                </Button>
              </form>
            )}
          </CardContent>
          <CardFooter className="flex justify-center border-t border-line py-4 bg-card/10">
            <Link
              to="/login"
              className="inline-flex items-center gap-1.5 text-sm font-medium text-brand-red hover:underline"
            >
              <ArrowLeft className="h-4 w-4" />
              Quay lại Đăng nhập
            </Link>
          </CardFooter>
        </Card>
      </div>
    </div>
  );
}
