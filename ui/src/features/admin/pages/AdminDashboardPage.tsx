import { useNavigate } from "react-router-dom";
import { BookOpen, CalendarRange, FileStack } from "lucide-react";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import { PageHeader } from "@/components/layout/PageHeader";
import { ContentBlockButton } from "@/components/ui/content-block";
import { authService } from "@/services/authService";

const quickLinks = [
  {
    title: "Quản lý học kỳ",
    description: "Tạo, sửa học kỳ và kỳ thi",
    to: "/admin/catalog/semesters",
    icon: CalendarRange,
    accent: "text-brand-red",
    borderAccent: "border-t-brand-red/60",
  },
  {
    title: "Danh mục thi",
    description: "Đi tới danh sách học kỳ",
    to: "/admin/catalog/semesters",
    icon: BookOpen,
    accent: "text-brand-orange",
    borderAccent: "border-t-brand-orange/60",
  },
  {
    title: "Thiết lập môn",
    description: "Upload đề, barem và lưới điểm",
    to: "/admin/catalog/semesters",
    icon: FileStack,
    accent: "text-done",
    borderAccent: "border-t-done/60",
  },
];

export function AdminDashboardPage() {
  const navigate = useNavigate();
  const user = authService.decodeToken();

  return (
    <LecturerPageShell maxWidth="5xl">
      <PageHeader
        title={user?.name ?? user?.email?.split("@")[0] ?? "Admin"}
        subtitle="Quản lý học kỳ, kỳ thi, môn thi — upload đề/barem và lưới điểm."
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {quickLinks.map((item) => (
          <ContentBlockButton
            key={item.title}
            onClick={() => navigate(item.to)}
            className={`border-t-4 ${item.borderAccent}`}
          >
            <item.icon className={`h-6 w-6 mb-4 ${item.accent}`} />
            <h2 className="font-semibold text-ink">{item.title}</h2>
            <p className="text-sm text-ink-soft mt-1">{item.description}</p>
          </ContentBlockButton>
        ))}
      </div>
    </LecturerPageShell>
  );
}
