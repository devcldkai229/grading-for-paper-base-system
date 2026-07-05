import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { isAxiosError } from "axios";
import { authService } from "@/services/authService";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardFooter } from "@/components/ui/card";
import { Loader2, Mail, ArrowLeft, ShieldAlert, KeyRound, CheckCircle2 } from "lucide-react";

export function ForgotPasswordPage() {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successToken, setSuccessToken] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim()) {
      setError("Vui lòng nhập email.");
      return;
    }

    setLoading(true);
    setError(null);
    setSuccessToken(null);

    try {
      const res = await authService.forgotPassword(email.trim());
      if (res.resetToken) {
        setSuccessToken(res.resetToken);
      } else {
        setError("Email không tồn tại trong hệ thống hoặc không đúng.");
      }
    } catch (err) {
      const apiMsg = isAxiosError(err) ? err.response?.data?.message : null;
      setError(apiMsg || "Gửi yêu cầu thất bại. Vui lòng kiểm tra lại email.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-paper via-background to-card flex flex-col justify-center py-12 sm:px-6 lg:px-8">
      <div className="sm:mx-auto sm:w-full sm:max-w-md">
        <div className="flex justify-center">
          <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-brand-red/10 border border-brand-red/20 shadow-sm">
            <KeyRound className="h-6 w-6 text-brand-red animate-pulse" />
          </div>
        </div>
        <h2 className="mt-6 text-center text-3xl font-display font-extrabold tracking-tight text-ink">
          Quên mật khẩu?
        </h2>
        <p className="mt-2 text-center text-sm text-ink-soft">
          Nhập email của bạn để nhận liên kết khôi phục mật khẩu.
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

            {successToken ? (
              <div className="space-y-6">
                <div className="rounded-lg bg-emerald-500/10 border border-emerald-500/20 p-4 text-sm text-emerald-600 dark:text-emerald-400 flex items-start gap-3">
                  <CheckCircle2 className="h-6 w-6 shrink-0 mt-0.5 text-emerald-500" />
                  <div>
                    <h4 className="font-semibold text-emerald-800 dark:text-emerald-300">Yêu cầu thành công!</h4>
                    <p className="mt-1 text-xs leading-relaxed text-emerald-700/90 dark:text-emerald-400/90">
                      Mã token khôi phục mật khẩu của bạn đã được tạo thành công dưới đây (Mô phỏng Email gửi từ máy chủ).
                    </p>
                  </div>
                </div>

                <div className="bg-card border border-line rounded-lg p-4 font-mono text-center text-sm text-ink select-all break-all shadow-inner">
                  {successToken}
                </div>

                <Button
                  onClick={() => navigate(`/reset-password?token=${successToken}`)}
                  className="w-full bg-emerald-600 text-white hover:bg-emerald-700 font-semibold py-2 rounded-lg shadow-md transition-all flex items-center justify-center gap-2"
                >
                  <KeyRound className="h-4 w-4" />
                  Tiến hành đặt lại mật khẩu
                </Button>
              </div>
            ) : (
              <form onSubmit={handleSubmit} className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="email" className="text-sm font-medium text-ink-soft">
                    Địa chỉ Email đăng nhập
                  </Label>
                  <div className="relative">
                    <Input
                      id="email"
                      type="email"
                      required
                      placeholder="vd: lecture@fpt.edu.vn"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      className="pl-10 h-11 rounded-lg"
                      disabled={loading}
                    />
                    <Mail className="absolute left-3.5 top-3.5 h-4 w-4 text-ink-soft pointer-events-none" />
                  </div>
                </div>

                <Button
                  type="submit"
                  disabled={loading}
                  className="w-full bg-brand-red text-white hover:bg-brand-red/90 font-semibold py-2.5 rounded-lg shadow-md transition-all flex items-center justify-center gap-2"
                >
                  {loading ? (
                    <>
                      <Loader2 className="h-5 w-5 animate-spin" />
                      Đang gửi yêu cầu...
                    </>
                  ) : (
                    "Gửi yêu cầu khôi phục"
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
