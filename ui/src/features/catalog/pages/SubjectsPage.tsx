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
import { ContentBlock } from "@/components/ui/content-block";
import { ListPagination } from "@/components/catalog/ListPagination";
import { catalogService } from "@/services/catalogService";
import { adminCatalogService } from "@/services/adminCatalogService";
import { useCatalogPaths } from "@/features/catalog/hooks/useCatalogPaths";
import type { Exam, Semester, SubjectSummary } from "@/types/catalog";
import { authService } from "@/services/authService";
import { isLecturer } from "@/lib/roles";
import {
  AdminModal,
  AdminField,
  AdminTextInput,
  AdminSelect,
  AdminPrimaryButton,
  AdminSecondaryButton,
} from "@/features/admin/components/AdminModal";

const PAGE_SIZE = 10;

const statusConfig: Record<string, { label: string; color: string }> = {
  Draft: { label: "Nháp", color: "bg-secondary text-ink-soft border-line" },
  Open: { label: "Mở", color: "bg-done/10 text-done border-done/25" },
  Grading: { label: "Đang chấm", color: "bg-brand-orange/10 text-brand-orange border-brand-orange/25" },
  Closed: { label: "Đã đóng", color: "bg-brand-red/10 text-brand-red border-brand-red/25" },
};

export function SubjectsPage() {
  const { semesterId: semesterIdParam, examId } = useParams<{
    semesterId?: string;
    examId: string;
  }>();
  const [resolvedSemesterId, setResolvedSemesterId] = useState<string | null>(
    semesterIdParam ?? null
  );
  const [semester, setSemester] = useState<Semester | null>(null);
  const [exam, setExam] = useState<Exam | null>(null);
  const [subjects, setSubjects] = useState<SubjectSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const navigate = useNavigate();
  const paths = useCatalogPaths();
  const role = authService.decodeToken()?.role;
  const showUpload = !paths.isAdminMode && isLecturer(role);
  const isAdmin = paths.isAdminMode;

  const [modalOpen, setModalOpen] = useState(false);
  const [form, setForm] = useState({
    subjectCode: "",
    title: "",
    maxScore: "10",
    passScore: "",
    status: "Draft",
  });
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const openCreate = () => {
    setForm({ subjectCode: "", title: "", maxScore: "10", passScore: "", status: "Draft" });
    setFormError(null);
    setModalOpen(true);
  };

  const handleSave = async () => {
    if (!examId || !form.subjectCode.trim()) {
      setFormError("Vui lòng nhập mã môn.");
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      await adminCatalogService.createSubject({
        examId,
        subjectCode: form.subjectCode.trim(),
        title: form.title.trim() || undefined,
        maxScore: Number(form.maxScore) || 0,
        passScore: form.passScore.trim() === "" ? null : Number(form.passScore),
        status: form.status,
      });
      setModalOpen(false);
      await load();
    } catch (err) {
      setFormError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Tạo môn thất bại."
          : "Tạo môn thất bại."
      );
    } finally {
      setSaving(false);
    }
  };

  const load = useCallback(async () => {
    if (!examId) return;
    setLoading(true);
    setError(null);
    try {
      const examData = await catalogService.getExam(examId);
      const semesterId = semesterIdParam ?? examData.semesterId;
      setResolvedSemesterId(semesterId);
      const [semesterData, subjectResult] = await Promise.all([
        catalogService.getSemester(semesterId),
        catalogService.getSubjectsByExam(examId, page, PAGE_SIZE),
      ]);
      setSemester(semesterData);
      setExam(examData);
      setSubjects(subjectResult.items);
      setTotalPages(subjectResult.totalPages);
      setTotalCount(subjectResult.totalCount);
    } catch (err) {
      setError(
        isAxiosError(err)
          ? `Không tải được môn thi (${err.response?.status ?? "lỗi mạng"}).`
          : "Không tải được môn thi."
      );
    } finally {
      setLoading(false);
    }
  }, [semesterIdParam, examId, page]);

  useEffect(() => {
    load();
  }, [load]);

  const semesterLabel = semester
    ? `${semester.code} — ${semester.name}`
    : "Học kỳ";
  const examLabel = exam ? `${exam.name} (${exam.examType})` : "Kỳ thi";

  return (
    <CatalogPageShell
      title="Danh sách Môn thi"
      subtitle={
        exam && semester
          ? `${semester.name} · ${exam.name} · ${exam.examType}`
          : "Chọn môn để xem chi tiết"
      }
      breadcrumb={
        <CatalogBreadcrumb
          items={[
            { label: "Học kỳ", to: paths.semesters },
            {
              label: semesterLabel,
              to: resolvedSemesterId
                ? paths.semesterExams(resolvedSemesterId)
                : undefined,
            },
            { label: examLabel },
          ]}
        />
      }
      actions={
        isAdmin ? (
          <button
            type="button"
            onClick={openCreate}
            className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm font-medium hover:opacity-90 shrink-0"
          >
            + Tạo môn
          </button>
        ) : showUpload ? (
          <button
            type="button"
            onClick={() => navigate(`/batches/upload?examId=${examId}`)}
            className="px-4 py-2 bg-primary hover:opacity-90 text-primary-foreground rounded-lg text-sm font-medium transition-opacity shrink-0"
          >
            Upload bài làm
          </button>
        ) : undefined
      }
    >
      {loading ? (
        <CatalogListSkeleton />
      ) : error ? (
        <CatalogErrorState message={error} onRetry={load} />
      ) : subjects.length === 0 ? (
        <CatalogEmptyState message="Chưa có môn thi nào trong kỳ thi này" />
      ) : (
        <div className="grid gap-4">
          {subjects.map((s) => {
            const st = statusConfig[s.status] || statusConfig.Draft;
            return (
              <ContentBlock
                key={s.id}
                interactive
                role="button"
                tabIndex={0}
                className="group p-5 cursor-pointer"
                onClick={() => navigate(paths.subject(s.id))}
                onKeyDown={(e) => {
                  if (e.key === "Enter") navigate(paths.subject(s.id));
                }}
              >
                <div className="flex items-center justify-between mb-3">
                  <div className="flex items-center gap-3">
                    <h3 className="font-semibold text-lg group-hover:text-brand-red transition-colors text-ink">
                      {s.subjectCode}
                    </h3>
                    {s.title && <span className="text-ink-soft">— {s.title}</span>}
                  </div>
                  <span
                    className={`px-2.5 py-1 rounded-full text-xs font-medium border ${st.color}`}
                  >
                    {st.label}
                  </span>
                </div>
                <div className="flex flex-wrap items-center gap-6 text-sm text-ink-soft">
                  <span>
                    Điểm tối đa:{" "}
                    <strong className="font-score text-brand-red">{s.maxScore}</strong>
                  </span>
                  <span>
                    Điểm đậu:{" "}
                    <strong className="font-score text-ink">
                      {s.passScore ?? "Chưa cấu hình"}
                    </strong>
                  </span>
                  <span>{s.questionCount} câu hỏi</span>
                  <div className="flex items-center gap-3 ml-auto">
                    <span
                      className={`flex items-center gap-1.5 ${
                        s.hasExamPaper ? "text-done" : "text-ink-soft/50"
                      }`}
                    >
                      {s.hasExamPaper ? "✓" : "✗"} Đề thi
                    </span>
                    <span
                      className={`flex items-center gap-1.5 ${
                        s.hasRubric ? "text-done" : "text-ink-soft/50"
                      }`}
                    >
                      {s.hasRubric ? "✓" : "✗"} Barem
                    </span>
                  </div>
                </div>
              </ContentBlock>
            );
          })}
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
        title="Tạo môn thi"
        onClose={() => setModalOpen(false)}
        footer={
          <>
            <AdminSecondaryButton onClick={() => setModalOpen(false)}>
              Hủy
            </AdminSecondaryButton>
            <AdminPrimaryButton disabled={saving} onClick={() => void handleSave()}>
              {saving ? "Đang lưu…" : "Tạo"}
            </AdminPrimaryButton>
          </>
        }
      >
        {formError && <p className="text-sm text-destructive">{formError}</p>}
        <AdminField label="Mã môn">
          <AdminTextInput
            value={form.subjectCode}
            onChange={(e) => setForm({ ...form, subjectCode: e.target.value })}
            placeholder="PRN222"
          />
        </AdminField>
        <AdminField label="Tiêu đề">
          <AdminTextInput
            value={form.title}
            onChange={(e) => setForm({ ...form, title: e.target.value })}
          />
        </AdminField>
        <AdminField label="Điểm tối đa">
          <AdminTextInput
            type="number"
            min={0}
            step={0.5}
            value={form.maxScore}
            onChange={(e) => setForm({ ...form, maxScore: e.target.value })}
          />
        </AdminField>
        <AdminField label="Điểm đậu (để trống nếu chưa xác định)">
          <AdminTextInput
            type="number"
            min={0}
            step={0.5}
            value={form.passScore}
            onChange={(e) => setForm({ ...form, passScore: e.target.value })}
            placeholder="vd: 5"
          />
        </AdminField>
        <AdminField label="Trạng thái">
          <AdminSelect
            value={form.status}
            onChange={(e) => setForm({ ...form, status: e.target.value })}
          >
            <option value="Draft">Draft</option>
            <option value="Open">Open</option>
            <option value="Grading">Grading</option>
            <option value="Closed">Closed</option>
          </AdminSelect>
        </AdminField>
      </AdminModal>
    </CatalogPageShell>
  );
}
