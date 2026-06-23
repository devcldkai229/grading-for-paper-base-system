import { useEffect, useState, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { isAxiosError } from "axios";
import { CatalogBreadcrumb } from "@/components/catalog/CatalogBreadcrumb";
import {
  CatalogEmptyState,
  CatalogErrorState,
  CatalogListSkeleton,
  CatalogPageShell,
} from "@/components/catalog/CatalogPageShell";
import { ListPagination } from "@/components/catalog/ListPagination";
import { ContentBlock } from "@/components/ui/content-block";
import { catalogService } from "@/services/catalogService";
import { adminCatalogService } from "@/services/adminCatalogService";
import { useCatalogPaths } from "@/features/catalog/hooks/useCatalogPaths";
import type { Exam, Semester } from "@/types/catalog";
import {
  AdminModal,
  AdminField,
  AdminTextInput,
  AdminSelect,
  AdminPrimaryButton,
  AdminSecondaryButton,
} from "@/features/admin/components/AdminModal";

const PAGE_SIZE = 10;

const examTypeColors: Record<string, string> = {
  FE: "bg-secondary text-ink border-line",
  PE: "bg-brand-red/10 text-brand-red border-brand-red/20",
  PT: "bg-brand-orange/10 text-brand-orange border-brand-orange/20",
  OTHER: "bg-secondary text-ink-soft border-line",
};

const examTypeLabels: Record<string, string> = {
  FE: "Final Exam",
  PE: "Practical Exam",
  PT: "Progress Test",
  OTHER: "Other",
};

export function ExamsPage() {
  const { semesterId } = useParams<{ semesterId: string }>();
  const [semester, setSemester] = useState<Semester | null>(null);
  const [exams, setExams] = useState<Exam[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const navigate = useNavigate();
  const paths = useCatalogPaths();
  const isAdmin = paths.isAdminMode;

  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<Exam | null>(null);
  const [form, setForm] = useState({
    name: "",
    examType: "PE",
    startDate: "",
    endDate: "",
  });
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const openCreate = () => {
    setEditing(null);
    setForm({ name: "", examType: "PE", startDate: "", endDate: "" });
    setFormError(null);
    setModalOpen(true);
  };

  const openEdit = (exam: Exam, e: React.MouseEvent) => {
    e.stopPropagation();
    setEditing(exam);
    setForm({
      name: exam.name,
      examType: exam.examType,
      startDate: exam.startDate ?? "",
      endDate: exam.endDate ?? "",
    });
    setFormError(null);
    setModalOpen(true);
  };

  const handleSave = async () => {
    if (!form.name.trim()) {
      setFormError("Vui lòng nhập tên kỳ thi.");
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      const payload = {
        name: form.name.trim(),
        examType: form.examType,
        startDate: form.startDate || null,
        endDate: form.endDate || null,
      };
      if (editing) {
        await adminCatalogService.updateExam(editing.id, payload);
      } else if (semesterId) {
        await adminCatalogService.createExam({ semesterId, ...payload });
      }
      setModalOpen(false);
      await load();
    } catch (err) {
      setFormError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Lưu kỳ thi thất bại."
          : "Lưu kỳ thi thất bại."
      );
    } finally {
      setSaving(false);
    }
  };

  const load = useCallback(async () => {
    if (!semesterId) return;
    setLoading(true);
    setError(null);
    try {
      const [semesterData, examResult] = await Promise.all([
        catalogService.getSemester(semesterId),
        catalogService.getExamsBySemester(semesterId, page, PAGE_SIZE),
      ]);
      setSemester(semesterData);
      setExams(examResult.items);
      setTotalPages(examResult.totalPages);
      setTotalCount(examResult.totalCount);
    } catch (err) {
      setError(
        isAxiosError(err)
          ? `Không tải được kỳ thi (${err.response?.status ?? "lỗi mạng"}).`
          : "Không tải được kỳ thi."
      );
    } finally {
      setLoading(false);
    }
  }, [semesterId, page]);

  useEffect(() => {
    load();
  }, [load]);

  const semesterLabel = semester
    ? `${semester.code} — ${semester.name}`
    : "Học kỳ";

  return (
    <CatalogPageShell
      title="Danh sách Kỳ thi"
      subtitle={semester ? `Học kỳ: ${semester.name}` : "Chọn kỳ thi để xem các môn"}
      breadcrumb={
        <CatalogBreadcrumb
          items={[
            { label: "Học kỳ", to: paths.semesters },
            { label: semesterLabel },
          ]}
        />
      }
      actions={
        isAdmin ? (
          <button
            type="button"
            onClick={openCreate}
            className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm font-medium hover:opacity-90"
          >
            + Tạo kỳ thi
          </button>
        ) : undefined
      }
    >
      {loading ? (
        <CatalogListSkeleton />
      ) : error ? (
        <CatalogErrorState message={error} onRetry={load} />
      ) : exams.length === 0 ? (
        <CatalogEmptyState message="Chưa có kỳ thi nào trong học kỳ này" />
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {exams.map((e) => (
            <ContentBlock
              key={e.id}
              interactive
              role="button"
              tabIndex={0}
              className="group p-5 cursor-pointer"
              onClick={() =>
                semesterId && navigate(paths.examSubjects(semesterId, e.id))
              }
              onKeyDown={(ev) => {
                if (ev.key === "Enter" && semesterId) {
                  navigate(paths.examSubjects(semesterId, e.id));
                }
              }}
            >
              <div className="flex items-start justify-between mb-3">
                <h3 className="font-semibold text-lg group-hover:text-brand-red transition-colors text-ink">
                  {e.name}
                </h3>
                <span
                  className={`px-2.5 py-1 rounded-full text-xs font-medium border ${
                    examTypeColors[e.examType] || examTypeColors.OTHER
                  }`}
                  title={examTypeLabels[e.examType] || e.examType}
                >
                  {e.examType}
                </span>
              </div>
              <div className="flex items-center justify-between text-sm text-ink-soft">
                <span>{e.subjectCount} môn thi</span>
                <div className="flex items-center gap-3">
                  {e.startDate && e.endDate && (
                    <span>
                      {e.startDate} → {e.endDate}
                    </span>
                  )}
                  {isAdmin && (
                    <button
                      type="button"
                      onClick={(ev) => openEdit(e, ev)}
                      className="text-xs text-brand-red hover:underline"
                    >
                      Sửa
                    </button>
                  )}
                </div>
              </div>
            </ContentBlock>
          ))}
        </div>
      )}

      <ListPagination
        page={page}
        totalPages={totalPages}
        totalCount={totalCount}
        pageSize={PAGE_SIZE}
        onPageChange={setPage}
      />

      <AdminModal
        open={modalOpen}
        title={editing ? "Sửa kỳ thi" : "Tạo kỳ thi"}
        onClose={() => setModalOpen(false)}
        footer={
          <>
            <AdminSecondaryButton onClick={() => setModalOpen(false)}>
              Hủy
            </AdminSecondaryButton>
            <AdminPrimaryButton disabled={saving} onClick={() => void handleSave()}>
              {saving ? "Đang lưu…" : "Lưu"}
            </AdminPrimaryButton>
          </>
        }
      >
        {formError && <p className="text-sm text-destructive">{formError}</p>}
        <AdminField label="Tên kỳ thi">
          <AdminTextInput
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
          />
        </AdminField>
        <AdminField label="Loại">
          <AdminSelect
            value={form.examType}
            onChange={(e) => setForm({ ...form, examType: e.target.value })}
          >
            <option value="PE">PE</option>
            <option value="FE">FE</option>
            <option value="PT">PT</option>
            <option value="OTHER">OTHER</option>
          </AdminSelect>
        </AdminField>
        <div className="grid grid-cols-2 gap-3">
          <AdminField label="Bắt đầu">
            <AdminTextInput
              type="date"
              value={form.startDate}
              onChange={(e) => setForm({ ...form, startDate: e.target.value })}
            />
          </AdminField>
          <AdminField label="Kết thúc">
            <AdminTextInput
              type="date"
              value={form.endDate}
              onChange={(e) => setForm({ ...form, endDate: e.target.value })}
            />
          </AdminField>
        </div>
      </AdminModal>
    </CatalogPageShell>
  );
}
