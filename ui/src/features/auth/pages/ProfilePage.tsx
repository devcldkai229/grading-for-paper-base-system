import { useState, useMemo } from "react";
import { KeyRound, ShieldAlert, CheckCircle2, XCircle } from "lucide-react";
import { authService } from "@/services/authService";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export function ProfilePage() {
  const user = authService.decodeToken();

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Strength checks
  const passwordCriteria = useMemo(() => {
    return {
      minLength: newPassword.length >= 8,
      hasUpper: /[A-Z]/.test(newPassword),
      hasLower: /[a-z]/.test(newPassword),
      hasDigit: /[0-9]/.test(newPassword),
      hasSpecial: /[^A-Za-z0-9]/.test(newPassword),
      matchesConfirm: newPassword !== "" && newPassword === confirmPassword,
    };
  }, [newPassword, confirmPassword]);

  const isFormValid = useMemo(() => {
    return (
      currentPassword.trim() !== "" &&
      Object.values(passwordCriteria).every(Boolean)
    );
  }, [currentPassword, passwordCriteria]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isFormValid) return;

    setLoading(true);
    setError(null);
    setSuccess(null);

    try {
      const result = await authService.changePassword(currentPassword, newPassword);
      if (result.success) {
        setSuccess("Đổi mật khẩu thành công!");
        setCurrentPassword("");
        setNewPassword("");
        setConfirmPassword("");
      } else {
        setError(result.errors.join(", ") || "Đổi mật khẩu thất bại");
      }
    } catch (err: any) {
      const apiMsg = err.response?.data?.message;
      setError(apiMsg || "Có lỗi xảy ra trong quá trình đổi mật khẩu.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="p-4 sm:p-6 lg:p-8 min-h-full flex flex-col items-center">
      <div className="w-full max-w-2xl space-y-6">
        <div>
          <h1 className="font-display text-2xl font-semibold text-ink">Hồ sơ cá nhân</h1>
          <p className="text-sm text-ink-soft mt-1">
            Quản lý thông tin tài khoản và cấu hình bảo mật.
          </p>
        </div>

        {/* User Info Card */}
        <Card className="border border-line bg-card">
          <CardHeader>
            <CardTitle>Thông tin tài khoản</CardTitle>
            <CardDescription>Chi tiết thông tin đăng nhập của bạn.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-3 gap-2 py-2 border-b border-line/60">
              <span className="text-ink-soft text-sm">Họ và tên</span>
              <span className="col-span-2 text-ink font-medium text-sm">
                {user?.name || user?.email?.split("@")[0]}
              </span>
            </div>
            <div className="grid grid-cols-3 gap-2 py-2 border-b border-line/60">
              <span className="text-ink-soft text-sm">Địa chỉ Email</span>
              <span className="col-span-2 text-ink font-medium text-sm">{user?.email}</span>
            </div>
            <div className="grid grid-cols-3 gap-2 py-2">
              <span className="text-ink-soft text-sm">Vai trò</span>
              <span className="col-span-2 text-brand-red font-medium text-sm">
                {user?.role === "Admin" ? "Quản trị viên (Admin)" : "Giảng viên (Lecturer)"}
              </span>
            </div>
          </CardContent>
        </Card>

        {/* Change Password Card */}
        <Card className="border border-line bg-card">
          <CardHeader>
            <div className="flex items-center gap-2">
              <KeyRound className="h-5 w-5 text-brand-red" />
              <CardTitle>Đổi mật khẩu</CardTitle>
            </div>
            <CardDescription>Cập nhật mật khẩu để bảo vệ tài khoản.</CardDescription>
          </CardHeader>
          <CardContent>
            {success && (
              <div className="mb-4 p-3 rounded-lg bg-done/10 border border-done/30 text-done text-sm flex items-center gap-2">
                <CheckCircle2 className="h-4 w-4 shrink-0" />
                <span>{success}</span>
              </div>
            )}

            {error && (
              <div className="mb-4 p-3 rounded-lg bg-destructive/10 border border-destructive/30 text-destructive text-sm flex items-center gap-2">
                <ShieldAlert className="h-4 w-4 shrink-0" />
                <span>{error}</span>
              </div>
            )}

            <form onSubmit={handleSubmit} className="space-y-5">
              <div className="space-y-2">
                <Label htmlFor="current-password">Mật khẩu hiện tại</Label>
                <Input
                  id="current-password"
                  type="password"
                  required
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
                  placeholder="Nhập mật khẩu hiện tại"
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="new-password">Mật khẩu mới</Label>
                <Input
                  id="new-password"
                  type="password"
                  required
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  placeholder="Nhập mật khẩu mới"
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="confirm-password">Xác nhận mật khẩu mới</Label>
                <Input
                  id="confirm-password"
                  type="password"
                  required
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  placeholder="Nhập lại mật khẩu mới"
                />
              </div>

              {/* Password strength checklist */}
              <div className="p-3 bg-secondary/40 rounded-lg border border-line space-y-2 text-xs">
                <p className="font-semibold text-ink-soft mb-1">Yêu cầu đối với mật khẩu mới:</p>
                <div className="grid grid-cols-2 gap-2">
                  <div className="flex items-center gap-1.5 text-ink-soft">
                    {passwordCriteria.minLength ? (
                      <CheckCircle2 className="h-3.5 w-3.5 text-done" />
                    ) : (
                      <XCircle className="h-3.5 w-3.5 text-destructive/70" />
                    )}
                    <span>Tối thiểu 8 ký tự</span>
                  </div>
                  <div className="flex items-center gap-1.5 text-ink-soft">
                    {passwordCriteria.hasUpper ? (
                      <CheckCircle2 className="h-3.5 w-3.5 text-done" />
                    ) : (
                      <XCircle className="h-3.5 w-3.5 text-destructive/70" />
                    )}
                    <span>Chứa ít nhất 1 chữ hoa</span>
                  </div>
                  <div className="flex items-center gap-1.5 text-ink-soft">
                    {passwordCriteria.hasLower ? (
                      <CheckCircle2 className="h-3.5 w-3.5 text-done" />
                    ) : (
                      <XCircle className="h-3.5 w-3.5 text-destructive/70" />
                    )}
                    <span>Chứa ít nhất 1 chữ thường</span>
                  </div>
                  <div className="flex items-center gap-1.5 text-ink-soft">
                    {passwordCriteria.hasDigit ? (
                      <CheckCircle2 className="h-3.5 w-3.5 text-done" />
                    ) : (
                      <XCircle className="h-3.5 w-3.5 text-destructive/70" />
                    )}
                    <span>Chứa ít nhất 1 chữ số</span>
                  </div>
                  <div className="flex items-center gap-1.5 text-ink-soft">
                    {passwordCriteria.hasSpecial ? (
                      <CheckCircle2 className="h-3.5 w-3.5 text-done" />
                    ) : (
                      <XCircle className="h-3.5 w-3.5 text-destructive/70" />
                    )}
                    <span>Ký tự đặc biệt (vd: @, #, $, ...)</span>
                  </div>
                  <div className="flex items-center gap-1.5 text-ink-soft">
                    {passwordCriteria.matchesConfirm ? (
                      <CheckCircle2 className="h-3.5 w-3.5 text-done" />
                    ) : (
                      <XCircle className="h-3.5 w-3.5 text-destructive/70" />
                    )}
                    <span>Xác nhận khớp mật khẩu mới</span>
                  </div>
                </div>
              </div>

              <Button
                type="submit"
                disabled={loading || !isFormValid}
                className="w-full bg-brand-red hover:bg-brand-red/90 text-white font-medium cursor-pointer"
              >
                {loading ? "Đang xử lý..." : "Cập nhật mật khẩu"}
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
