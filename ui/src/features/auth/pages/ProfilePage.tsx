import { useState, useEffect, useMemo, useRef } from "react";
import { KeyRound, ShieldAlert, CheckCircle2, XCircle, Camera, Loader2, User as UserIcon } from "lucide-react";
import { isAxiosError } from "axios";
import { authService } from "@/services/authService";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import type { User } from "@/types/user";

export function ProfilePage() {
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Profile fields state
  const [profile, setProfile] = useState<User | null>(null);
  const [fullName, setFullName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [avatarUrl, setAvatarUrl] = useState("");

  // Change Password state
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  // General state
  const [loadingProfile, setLoadingProfile] = useState(true);
  const [savingProfile, setSavingProfile] = useState(false);
  const [uploadingAvatar, setUploadingAvatar] = useState(false);
  const [changingPassword, setChangingPassword] = useState(false);

  const [profileSuccess, setProfileSuccess] = useState<string | null>(null);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [passwordSuccess, setPasswordSuccess] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);

  // Load profile
  useEffect(() => {
    async function loadProfile() {
      try {
        const data = await authService.getProfile();
        setProfile(data);
        setFullName(data.fullName || "");
        setPhoneNumber(data.phoneNumber || "");
        setAvatarUrl(data.avatarUrl || "");
      } catch (err) {
        console.error("Không tải được thông tin cá nhân", err);
      } finally {
        setLoadingProfile(false);
      }
    }
    loadProfile();
  }, []);

  // Avatar URL helper
  const getAvatarFullUrl = (url?: string) => {
    if (!url) return "";
    if (url.startsWith("http://") || url.startsWith("https://")) {
      return url;
    }
    const baseUrl = import.meta.env.VITE_API_BASE_URL || "http://localhost:5016/api";
    return `${baseUrl}${url}`;
  };

  // Avatar upload handler
  const handleAvatarClick = () => {
    fileInputRef.current?.click();
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploadingAvatar(true);
    setProfileError(null);
    setProfileSuccess(null);

    try {
      const result = await authService.uploadAvatar(file);
      setAvatarUrl(result.avatarUrl);
      
      // Auto-save the avatar url to the profile immediately
      if (profile) {
        const updated = await authService.updateProfile({
          fullName: fullName.trim() || profile.email.split("@")[0],
          phoneNumber: phoneNumber.trim() || undefined,
          avatarUrl: result.avatarUrl,
        });
        setProfile(updated);
        
        // Cache profile
        localStorage.setItem(
          "user_profile_cache",
          JSON.stringify({ fullName: updated.fullName, avatarUrl: updated.avatarUrl })
        );
        window.dispatchEvent(new Event("profile-updated"));
        setProfileSuccess("Ảnh đại diện đã được cập nhật thành công!");
      }
    } catch (err) {
      const apiMsg = isAxiosError(err) ? err.response?.data?.message : null;
      setProfileError(apiMsg || "Tải lên ảnh đại diện thất bại.");
    } finally {
      setUploadingAvatar(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  };

  // Profile save handler
  const handleSaveProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!fullName.trim()) {
      setProfileError("Họ và tên không được để trống.");
      return;
    }

    setSavingProfile(true);
    setProfileError(null);
    setProfileSuccess(null);

    try {
      const updated = await authService.updateProfile({
        fullName: fullName.trim(),
        phoneNumber: phoneNumber.trim() || undefined,
        avatarUrl: avatarUrl || undefined,
      });

      setProfile(updated);
      setProfileSuccess("Cập nhật thông tin cá nhân thành công!");

      // Update cache and dispatch reactive update event
      localStorage.setItem(
        "user_profile_cache",
        JSON.stringify({ fullName: updated.fullName, avatarUrl: updated.avatarUrl })
      );
      window.dispatchEvent(new Event("profile-updated"));
    } catch (err) {
      const apiMsg = isAxiosError(err) ? err.response?.data?.message : null;
      setProfileError(apiMsg || "Lưu thông tin cá nhân thất bại.");
    } finally {
      setSavingProfile(false);
    }
  };

  // Password Strength checks
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

  const isPasswordFormValid = useMemo(() => {
    return (
      currentPassword.trim() !== "" &&
      Object.values(passwordCriteria).every(Boolean)
    );
  }, [currentPassword, passwordCriteria]);

  // Change password handler
  const handleChangePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isPasswordFormValid) return;

    setChangingPassword(true);
    setPasswordError(null);
    setPasswordSuccess(null);

    try {
      const result = await authService.changePassword(currentPassword, newPassword);
      if (result.success) {
        setPasswordSuccess("Đổi mật khẩu thành công!");
        setCurrentPassword("");
        setNewPassword("");
        setConfirmPassword("");
      } else {
        setPasswordError(result.errors.join(", ") || "Đổi mật khẩu thất bại");
      }
    } catch (err) {
      const apiMsg = isAxiosError(err) ? err.response?.data?.message : null;
      setPasswordError(apiMsg || "Có lỗi xảy ra trong quá trình đổi mật khẩu.");
    } finally {
      setChangingPassword(false);
    }
  };

  if (loadingProfile) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[400px] gap-2">
        <Loader2 className="h-8 w-8 animate-spin text-brand-red" />
        <p className="text-sm text-ink-soft">Đang tải thông tin cá nhân...</p>
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8 min-h-full flex flex-col items-center">
      <div className="w-full max-w-3xl space-y-6">
        <div>
          <h1 className="font-display text-2xl font-semibold text-ink">Hồ sơ cá nhân</h1>
          <p className="text-sm text-ink-soft mt-1">
            Quản lý thông tin tài khoản và cấu hình bảo mật.
          </p>
        </div>

        <div className="grid gap-6 md:grid-cols-3">
          {/* Avatar upload Card */}
          <Card className="border border-line bg-card md:col-span-1 h-fit flex flex-col items-center p-6 text-center">
            <div className="relative group cursor-pointer" onClick={handleAvatarClick}>
              <div className="h-28 w-28 rounded-full border border-line bg-secondary flex items-center justify-center font-display font-semibold text-brand-red overflow-hidden relative shadow-sm">
                {avatarUrl ? (
                  <img
                    src={getAvatarFullUrl(avatarUrl)}
                    alt="Avatar"
                    className="h-full w-full object-cover"
                  />
                ) : (
                  <UserIcon className="h-10 w-10 text-ink-soft" />
                )}
                
                {/* Upload overlay */}
                <div className="absolute inset-0 bg-black/40 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity">
                  <Camera className="h-6 w-6 text-white" />
                </div>

                {uploadingAvatar && (
                  <div className="absolute inset-0 bg-black/60 flex items-center justify-center">
                    <Loader2 className="h-6 w-6 animate-spin text-white" />
                  </div>
                )}
              </div>
            </div>
            <input
              type="file"
              ref={fileInputRef}
              onChange={handleFileChange}
              accept="image/*"
              className="hidden"
            />
            <p className="text-sm font-semibold mt-4 text-ink">
              {profile?.fullName || profile?.email.split("@")[0]}
            </p>
            <p className="text-xs text-ink-soft mt-1">{profile?.email}</p>
            <Button
              variant="outline"
              size="sm"
              onClick={handleAvatarClick}
              disabled={uploadingAvatar}
              className="mt-4 border border-line hover:bg-secondary cursor-pointer"
            >
              Chọn ảnh mới
            </Button>
          </Card>

          {/* Profile fields Card */}
          <Card className="border border-line bg-card md:col-span-2">
            <CardHeader>
              <CardTitle>Thông tin tài khoản</CardTitle>
              <CardDescription>Cập nhật thông tin chi tiết và liên hệ.</CardDescription>
            </CardHeader>
            <CardContent>
              {profileSuccess && (
                <div className="mb-4 p-3 rounded-lg bg-done/10 border border-done/30 text-done text-sm flex items-center gap-2">
                  <CheckCircle2 className="h-4 w-4 shrink-0" />
                  <span>{profileSuccess}</span>
                </div>
              )}

              {profileError && (
                <div className="mb-4 p-3 rounded-lg bg-destructive/10 border border-destructive/30 text-destructive text-sm flex items-center gap-2">
                  <ShieldAlert className="h-4 w-4 shrink-0" />
                  <span>{profileError}</span>
                </div>
              )}

              <form onSubmit={handleSaveProfile} className="space-y-4">
                <div className="grid gap-2">
                  <Label htmlFor="profile-email">Địa chỉ Email</Label>
                  <Input
                    id="profile-email"
                    type="email"
                    disabled
                    value={profile?.email || ""}
                    className="bg-secondary/40 text-ink-soft cursor-not-allowed"
                  />
                </div>

                <div className="grid gap-2">
                  <Label htmlFor="profile-role">Vai trò</Label>
                  <Input
                    id="profile-role"
                    type="text"
                    disabled
                    value={profile?.role === 1 ? "Quản trị viên (Admin)" : "Giảng viên (Lecturer)"}
                    className="bg-secondary/40 text-ink-soft cursor-not-allowed"
                  />
                </div>

                <div className="grid gap-2">
                  <Label htmlFor="profile-fullname">Họ và tên hiển thị</Label>
                  <Input
                    id="profile-fullname"
                    type="text"
                    required
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    placeholder="Nguyễn Văn A"
                  />
                </div>

                <div className="grid gap-2">
                  <Label htmlFor="profile-phone">Số điện thoại</Label>
                  <Input
                    id="profile-phone"
                    type="text"
                    value={phoneNumber}
                    onChange={(e) => setPhoneNumber(e.target.value)}
                    placeholder="Nhập số điện thoại liên hệ"
                  />
                </div>

                <Button
                  type="submit"
                  disabled={savingProfile || !fullName.trim()}
                  className="bg-brand-red hover:bg-brand-red/90 text-white font-medium cursor-pointer"
                >
                  {savingProfile ? "Đang lưu..." : "Lưu thay đổi"}
                </Button>
              </form>
            </CardContent>
          </Card>
        </div>

        {/* Change Password Card */}
        <Card className="border border-line bg-card">
          <CardHeader>
            <div className="flex items-center gap-2">
              <KeyRound className="h-5 w-5 text-brand-red" />
              <CardTitle>Đổi mật khẩu bảo mật</CardTitle>
            </div>
            <CardDescription>Cập nhật mật khẩu định kỳ để nâng cao bảo mật.</CardDescription>
          </CardHeader>
          <CardContent>
            {passwordSuccess && (
              <div className="mb-4 p-3 rounded-lg bg-done/10 border border-done/30 text-done text-sm flex items-center gap-2">
                <CheckCircle2 className="h-4 w-4 shrink-0" />
                <span>{passwordSuccess}</span>
              </div>
            )}

            {passwordError && (
              <div className="mb-4 p-3 rounded-lg bg-destructive/10 border border-destructive/30 text-destructive text-sm flex items-center gap-2">
                <ShieldAlert className="h-4 w-4 shrink-0" />
                <span>{passwordError}</span>
              </div>
            )}

            <form onSubmit={handleChangePassword} className="space-y-5">
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
                disabled={changingPassword || !isPasswordFormValid}
                className="w-full bg-brand-red hover:bg-brand-red/90 text-white font-medium cursor-pointer"
              >
                {changingPassword ? "Đang xử lý..." : "Cập nhật mật khẩu"}
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
