import { useNavigate } from "react-router-dom";
import { BookOpen, ClipboardList, Upload } from "lucide-react";
import { authService } from "@/services/authService";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import { PageHeader } from "@/components/layout/PageHeader";
import { ContentBlockButton } from "@/components/ui/content-block";
import { GradingProgressCard } from "@/features/auth/components/GradingProgressCard";

const quickLinks = [
  {
    title: "Danh mục thi",
    description: "Học kỳ → Kỳ thi PE → Môn thi",
    to: "/catalog/semesters",
    icon: BookOpen,
    accent: "text-brand-red",
    borderAccent: "border-t-brand-red/60",
  },
  {
    title: "Upload bài làm",
    description: "Nộp ZIP hoặc file lẻ theo môn",
    to: "/batches/upload",
    icon: Upload,
    accent: "text-brand-orange",
    borderAccent: "border-t-brand-orange/60",
  },
  {
    title: "Bài đã nộp",
    description: "Xem và chấm các bài bạn đã upload",
    to: "/submissions",
    icon: ClipboardList,
    accent: "text-done",
    borderAccent: "border-t-done/60",
  },
];

export function DashboardPage() {
  const navigate = useNavigate();
  const user = authService.decodeToken();

  return (
    <LecturerPageShell maxWidth="5xl">
      <PageHeader
        title={user?.email?.split("@")[0] ?? "Giảng viên"}
        subtitle="Chọn chức năng bên trái hoặc bắt đầu từ một thẻ bên dưới."
      />

      <GradingProgressCard />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {quickLinks.map((item) => (
          <ContentBlockButton
            key={item.to}
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
