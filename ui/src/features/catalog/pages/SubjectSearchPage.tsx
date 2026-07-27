import { useEffect, useState, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { isAxiosError } from "axios";
import { CatalogBreadcrumb } from "@/components/catalog/CatalogBreadcrumb";
import {
  CatalogEmptyState,
  CatalogErrorState,
  CatalogListSkeleton,
  CatalogPageShell,
} from "@/components/catalog/CatalogPageShell";
import { ContentBlockButton } from "@/components/ui/content-block";
import { ListPagination } from "@/components/catalog/ListPagination";
import { catalogService } from "@/services/catalogService";
import { useCatalogPaths } from "@/features/catalog/hooks/useCatalogPaths";
import type { Semester, Exam, SubjectSearchResult } from "@/types/catalog";
import { AdminField, AdminTextInput, AdminSelect } from "@/features/admin/components/AdminModal";

const PAGE_SIZE = 10;
const DEBOUNCE_MS = 350;

const statusConfig: Record<string, { label: string; color: string }> = {
  Draft: { label: "Nháp", color: "bg-secondary text-ink-soft border-line" },
  Open: { label: "Mở", color: "bg-done/10 text-done border-done/25" },
  Grading: { label: "Đang chấm", color: "bg-brand-orange/10 text-brand-orange border-brand-orange/25" },
  Closed: { label: "Đã đóng", color: "bg-brand-red/10 text-brand-red border-brand-red/25" },
};

export function SubjectSearchPage() {
  const navigate = useNavigate();
  const paths = useCatalogPaths();

  const [code, setCode] = useState("");
  const [debouncedCode, setDebouncedCode] = useState("");
  const [semesterId, setSemesterId] = useState("");
  const [examId, setExamId] = useState("");
  const [status, setStatus] = useState("");

  const [semesters, setSemesters] = useState<Semester[]>([]);
  const [exams, setExams] = useState<Exam[]>([]);

  const [subjects, setSubjects] = useState<SubjectSearchResult[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  // Debounce the free-text code search so we don't fire a request on every keystroke.
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedCode(code.trim()), DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [code]);

  // Reset to page 1 whenever a filter changes.
  useEffect(() => {
    setPage(1);
  }, [debouncedCode, semesterId, examId, status]);

  useEffect(() => {
    catalogService
      .getSemesters(undefined, 1, 100)
      .then((r) => setSemesters(r.items))
      .catch(() => setSemesters([]));
  }, []);

  useEffect(() => {
    if (!semesterId) {
      setExams([]);
      setExamId("");
      return;
    }
    catalogService
      .getExamsBySemester(semesterId, 1, 100)
      .then((r) => setExams(r.items))
      .catch(() => setExams([]));
    setExamId("");
  }, [semesterId]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await catalogService.searchSubjects({
        code: debouncedCode || undefined,
        semesterId: semesterId || undefined,
        examId: examId || undefined,
        status: status || undefined,
        page,
        pageSize: PAGE_SIZE,
        all: true,
      });
      setSubjects(result.items);
      setTotalPages(result.totalPages);
      setTotalCount(result.totalCount);
    } catch (err) {
      setError(
        isAxiosError(err)
          ? `Không tìm được môn thi (${err.response?.status ?? "lỗi mạng"}).`
          : "Không tìm được môn thi."
      );
      setSubjects([]);
    } finally {
      setLoading(false);
    }
  }, [debouncedCode, semesterId, examId, status, page]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <CatalogPageShell
      title="Tìm kiếm môn thi"
      subtitle="Tìm theo mã môn, học kỳ, kỳ thi hoặc trạng thái"
      breadcrumb={
        <CatalogBreadcrumb
          items={[{ label: "Danh mục thi", to: paths.semesters }, { label: "Tìm kiếm" }]}
        />
      }
    >
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 mb-6">
        <AdminField label="Mã môn">
          <AdminTextInput
            value={code}
            onChange={(e) => setCode(e.target.value)}
            placeholder="PRN222"
          />
        </AdminField>
        <AdminField label="Học kỳ">
          <AdminSelect value={semesterId} onChange={(e) => setSemesterId(e.target.value)}>
            <option value="">Tất cả học kỳ</option>
            {semesters.map((s) => (
              <option key={s.id} value={s.id}>
                {s.code} — {s.name}
              </option>
            ))}
          </AdminSelect>
        </AdminField>
        <AdminField label="Kỳ thi">
          <AdminSelect
            value={examId}
            onChange={(e) => setExamId(e.target.value)}
            disabled={!semesterId}
          >
            <option value="">Tất cả kỳ thi</option>
            {exams.map((e) => (
              <option key={e.id} value={e.id}>
                {e.name} ({e.examType})
              </option>
            ))}
          </AdminSelect>
        </AdminField>
        <AdminField label="Trạng thái">
          <AdminSelect value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">Tất cả trạng thái</option>
            <option value="Draft">Nháp</option>
            <option value="Open">Mở</option>
            <option value="Grading">Đang chấm</option>
            <option value="Closed">Đã đóng</option>
          </AdminSelect>
        </AdminField>
      </div>

      {loading ? (
        <CatalogListSkeleton />
      ) : error ? (
        <CatalogErrorState message={error} onRetry={load} />
      ) : subjects.length === 0 ? (
        <CatalogEmptyState message="Không tìm thấy môn thi nào khớp bộ lọc." />
      ) : (
        <div className="grid gap-4">
          {subjects.map((s) => {
            const st = statusConfig[s.status] || statusConfig.Draft;
            return (
              <ContentBlockButton
                key={s.id}
                className="group p-5"
                onClick={() => navigate(paths.subject(s.id))}
              >
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-3 min-w-0">
                    <h3 className="font-semibold text-lg group-hover:text-brand-red transition-colors text-ink truncate">
                      {s.subjectCode}
                    </h3>
                    {s.title && (
                      <span className="text-ink-soft truncate">— {s.title}</span>
                    )}
                  </div>
                  <span
                    className={`px-2.5 py-1 rounded-full text-xs font-medium border shrink-0 ${st.color}`}
                  >
                    {st.label}
                  </span>
                </div>
                <div className="flex flex-wrap items-center gap-x-6 gap-y-1 text-sm text-ink-soft">
                  <span>{s.semesterCode}</span>
                  <span>{s.examName}</span>
                  <span>
                    Điểm tối đa:{" "}
                    <strong className="font-score text-brand-red">{s.maxScore}</strong>
                  </span>
                  <span>{s.questionCount} câu hỏi</span>
                </div>
              </ContentBlockButton>
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
    </CatalogPageShell>
  );
}
