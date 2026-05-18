import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { authService } from "@/services/authService";
import type { JwtPayload } from "@/types/auth";
import {
  GraduationCap,
  LogOut,
  Mail,
  Shield,
  User,
  Clock,
  Loader2,
} from "lucide-react";

export function DashboardPage() {
  const navigate = useNavigate();
  const [user, setUser] = useState<JwtPayload | null>(null);
  const [isLoggingOut, setIsLoggingOut] = useState(false);

  useEffect(() => {
    const decoded = authService.decodeToken();
    if (!decoded) {
      navigate("/login", { replace: true });
      return;
    }
    setUser(decoded);
  }, [navigate]);

  const handleLogout = async () => {
    setIsLoggingOut(true);
    try {
      await authService.logout();
    } finally {
      navigate("/login", { replace: true });
    }
  };

  if (!user) return null;

  const expiresAt = new Date(user.exp * 1000);
  const isExpired = expiresAt < new Date();

  const infoRows = [
    {
      icon: Mail,
      label: "Email",
      value: user.email,
    },
    {
      icon: User,
      label: "Full Name",
      value: user.name || "—",
    },
    {
      icon: Shield,
      label: "Role",
      value: user.role,
      badge: true,
    },
    {
      icon: Clock,
      label: "Token Expires",
      value: expiresAt.toLocaleString(),
      status: isExpired ? "expired" : "active",
    },
  ];

  return (
    <div className="min-h-svh bg-background flex flex-col">
      {/* Top Bar */}
      <header className="border-b border-border/60 bg-card">
        <div className="max-w-5xl mx-auto flex items-center justify-between px-6 h-16">
          <div className="flex items-center gap-3">
            <div className="flex items-center justify-center w-9 h-9 rounded-lg bg-primary/15 border border-primary/25">
              <GraduationCap className="size-4 text-primary" />
            </div>
            <span className="font-semibold tracking-tight text-sm">
              Grading System
            </span>
          </div>

          <Button
            variant="outline"
            size="sm"
            onClick={handleLogout}
            disabled={isLoggingOut}
            className="gap-1.5 cursor-pointer"
          >
            {isLoggingOut ? (
              <Loader2 className="size-3.5 animate-spin" />
            ) : (
              <LogOut className="size-3.5" />
            )}
            <span>Logout</span>
          </Button>
        </div>
      </header>

      {/* Content */}
      <main className="flex-1 flex items-start justify-center p-6 sm:p-8 pt-12 sm:pt-16">
        <div className="w-full max-w-lg space-y-6 animate-in fade-in slide-in-from-bottom-4 duration-500">
          {/* Welcome Banner */}
          <div className="space-y-1">
            <h1 className="text-2xl font-bold tracking-tight">
              Welcome, {user.name || user.email.split("@")[0]}
            </h1>
            <p className="text-sm text-muted-foreground">
              You are signed in successfully. Here's your account information.
            </p>
          </div>

          {/* User Info Card */}
          <Card className="border-border/60 bg-card shadow-lg shadow-black/5">
            <CardHeader className="pb-4">
              <CardTitle className="text-base font-semibold flex items-center gap-2">
                <div className="w-10 h-10 rounded-full bg-primary/15 border border-primary/25 flex items-center justify-center">
                  <span className="text-primary font-bold text-sm">
                    {(user.name || user.email)[0].toUpperCase()}
                  </span>
                </div>
                <div>
                  <p className="text-sm font-semibold">{user.name || "User"}</p>
                  <p className="text-xs text-muted-foreground font-normal">
                    ID: {user.sub.slice(0, 8)}...
                  </p>
                </div>
              </CardTitle>
            </CardHeader>

            <CardContent>
              <div className="divide-y divide-border/30">
                {infoRows.map(({ icon: Icon, label, value, badge, status }) => (
                  <div
                    key={label}
                    className="flex items-center justify-between py-3.5 first:pt-0 last:pb-0"
                  >
                    <div className="flex items-center gap-2.5 text-muted-foreground">
                      <Icon className="size-4" />
                      <span className="text-sm">{label}</span>
                    </div>
                    <div className="text-right">
                      {badge ? (
                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-md text-xs font-medium bg-primary/15 text-primary border border-primary/20">
                          {value}
                        </span>
                      ) : status ? (
                        <div className="flex items-center gap-2">
                          <span className="text-sm text-foreground/80">
                            {value}
                          </span>
                          <span
                            className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wider ${
                              status === "active"
                                ? "bg-emerald-50 text-emerald-700 border border-emerald-200"
                                : "bg-destructive/15 text-destructive border border-destructive/20"
                            }`}
                          >
                            <div
                              className={`w-1 h-1 rounded-full ${
                                status === "active"
                                  ? "bg-emerald-600"
                                  : "bg-destructive"
                              }`}
                            />
                            {status}
                          </span>
                        </div>
                      ) : (
                        <span className="text-sm text-foreground/80">
                          {value}
                        </span>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>

          {/* Note */}
          <p className="text-xs text-muted-foreground/40 text-center">
            This is a temporary dashboard. Full features will be available soon.
          </p>
        </div>
      </main>
    </div>
  );
}
