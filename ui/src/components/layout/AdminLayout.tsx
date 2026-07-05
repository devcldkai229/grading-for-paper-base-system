import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { BookOpen, LayoutDashboard, LogOut, Settings2, Users } from "lucide-react";
import { authService } from "@/services/authService";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const navItems = [
  { to: "/admin/dashboard", label: "Tổng quan", icon: LayoutDashboard, end: true },
  { to: "/admin/catalog/semesters", label: "Danh mục thi", icon: BookOpen },
  { to: "/admin/users", label: "Tài khoản", icon: Users },
];

export function AdminLayout() {
  const navigate = useNavigate();
  const user = authService.decodeToken();

  const handleLogout = async () => {
    await authService.logout();
    navigate("/login", { replace: true });
  };

  return (
    <div className="min-h-svh bg-paper text-ink flex">
      <aside className="w-64 shrink-0 border-r border-line bg-card flex flex-col shadow-[1px_0_6px_rgba(27,26,25,0.03)]">
        <div className="px-5 py-6 border-b border-line">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-brand-red/10 border border-brand-red/20">
              <Settings2 className="h-5 w-5 text-brand-red" />
            </div>
            <div className="min-w-0">
              <p className="font-display font-semibold text-sm tracking-tight truncate text-ink">
                GradePaper
              </p>
              <p className="text-xs text-brand-red truncate font-medium">Quản trị</p>
            </div>
          </div>
        </div>

        <nav className="flex-1 p-3 space-y-1.5">
          {navItems.map(({ to, label, icon: Icon, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) =>
                cn(
                  "flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors border",
                  isActive
                    ? "bg-brand-red/10 text-brand-red border-brand-red/30 shadow-sm"
                    : "text-ink-soft border-transparent hover:text-ink hover:bg-secondary hover:border-line"
                )
              }
            >
              <Icon className="h-4 w-4 shrink-0" />
              {label}
            </NavLink>
          ))}
        </nav>

        <div className="p-4 border-t border-line space-y-3">
          {user && (
            <NavLink
              to="/admin/profile"
              className="block px-2 py-2 rounded-lg border border-line bg-secondary/50 hover:bg-secondary hover:border-brand-red/30 transition-colors cursor-pointer"
              title="Xem hồ sơ cá nhân"
            >
              <p className="text-sm font-medium truncate text-ink">
                {user.name || user.email.split("@")[0]}
              </p>
              <p className="text-xs text-ink-soft truncate">{user.email}</p>
            </NavLink>
          )}
          <Button
            variant="outline"
            size="sm"
            className="w-full justify-start gap-2 border border-line bg-transparent hover:bg-secondary cursor-pointer"
            onClick={() => void handleLogout()}
          >
            <LogOut className="h-3.5 w-3.5" />
            Đăng xuất
          </Button>
        </div>
      </aside>

      <main className="flex-1 min-w-0 overflow-auto bg-paper">
        <Outlet />
      </main>
    </div>
  );
}
