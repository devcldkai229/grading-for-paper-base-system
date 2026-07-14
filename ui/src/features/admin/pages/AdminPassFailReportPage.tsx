import { useEffect, useState, useCallback } from "react";
import { isAxiosError } from "axios";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import { PageHeader } from "@/components/layout/PageHeader";
import {
  CatalogEmptyState,
  CatalogErrorState,
  CatalogListSkeleton,
} from "@/components/catalog/CatalogPageShell";
import { ContentBlock } from "@/components/ui/content-block";
import { AdminField, AdminSelect } from "@/features/admin/components/AdminModal";
import { catalogService } from "@/services/catalogService";
import { reportingService } from "@/services/reportingService";
import type { Semester, Exam } from "@/types/catalog";
import type { SubjectPassFail } from "@/types/reporting";

function PassFailBar({ subject }: { subject: SubjectPassFail }) {
  const total = (subject.passCount ?? 0) + (subject.failCount ?? 0);
  const passPct = total > 0 ? ((subject.passCount ?? 0) / total) * 100 : 0;
  const failPct = total > 0 ? 100 - passPct : 0;

  return (
    <div className="mt-4">
      <div className="flex items-center gap-3 text-sm mb-2">
        <span className="flex items-center gap-1.5 text-ink-soft">
          <span className="inline-block w-2.5 h-2.5 rounded-sm bg-done" /> Đậu
        </span>
        <span className="flex items-center gap-1.5 text-ink-soft">
          <span className="inline-block w-2.5 h-2.5 rounded-sm bg-destructive" /> Rớt
        </span>
      </div>
      <div className="h-6 rounded-md overflow-hidden flex bg-secondary" role="img" aria-label="Tỉ lệ đậu/rớt">
        {passPct > 0 && (
          <div
            className="h-full bg-done flex items-center justify-center text-xs font-medium text-white"
            style={{ width: `${passPct}%` }}
          >
            {passPct >= 12 ? `${Math.round(passPct)}%` : ""}
          </div>
        )}
        {failPct > 0 && (
          <div
            className="h-full bg-destructive flex items-center justify-center text-xs font-medium text-white"
            style={{ width: `${failPct}%` }}
          >
            {failPct >= 12 ? `${Math.round(failPct)}%` : ""}
          </div>
        )}
      </div>
    </div>
  );
}

export function AdminPassFailReportPage() {
  const [semesterId, setSemesterId] = useState("");
  const [examId, setExamId] = useState("");
  const [semesters, setSemesters] = useState<Semester[]>([]);
  const [exams, setExams] = useState<Exam[]>([]);

  const [subjects, setSubjects] = useState<SubjectPassFail[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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
      const result = await reportingService.getPassFailReport(
        semesterId || undefined,
        examId || undefined
      );
      setSubjects(result.subjects);
    } catch (err) {
      setError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Không tải được báo cáo đậu/rớt."
          : "Không tải được báo cáo đậu/rớt."
      );
      setSubjects([]);
    } finally {
      setLoading(false);
    }
  }, [semesterId, examId]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <LecturerPageShell maxWidth="6xl">
      <PageHeader
        title="Thống kê đậu/rớt"
        subtitle="Số lượng và tỉ lệ đậu/rớt theo môn thi, dựa trên điểm đậu đã cấu hình cho từng môn."
      />

      <div className="grid gap-3 sm:grid-cols-2 mb-6">
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
      </div>

      {loading ? (
        <CatalogListSkeleton />
      ) : error ? (
        <CatalogErrorState message={error} onRetry={load} />
      ) : subjects.length === 0 ? (
        <CatalogEmptyState message="Không có môn thi nào khớp bộ lọc." />
      ) : (
        <div className="grid gap-4">
          {subjects.map((s) => (
            <ContentBlock key={s.subjectId} className="p-5">
              <div className="flex items-center justify-between gap-4">
                <div className="min-w-0">
                  <h3 className="font-semibold text-ink truncate">
                    {s.subjectCode}
                    {s.title ? ` — ${s.title}` : ""}
                  </h3>
                  <p className="text-xs text-ink-soft">
                    {s.semesterCode} · {s.examName}
                  </p>
                </div>
                <div className="text-right shrink-0 text-sm text-ink-soft">
                  <p>
                    Đã chấm: <strong className="text-ink">{s.submittedCount}</strong> bài
                  </p>
                  <p>
                    Điểm đậu:{" "}
                    <strong className="text-ink">{s.passScore ?? "Chưa cấu hình"}</strong>
                    {s.passScore !== null ? ` / ${s.maxScore}` : ""}
                  </p>
                </div>
              </div>

              {s.passScore === null ? (
                <p className="text-sm text-brand-orange mt-4">
                  Môn này chưa cấu hình điểm đậu — vào Danh mục thi để thiết lập.
                </p>
              ) : s.submittedCount === 0 ? (
                <p className="text-sm text-ink-soft mt-4">Chưa có bài nào được chấm xong.</p>
              ) : (
                <>
                  <PassFailBar subject={s} />
                  <div className="flex flex-wrap gap-x-6 gap-y-1 text-sm text-ink-soft mt-3">
                    <span>
                      Đậu: <strong className="text-done">{s.passCount}</strong>
                    </span>
                    <span>
                      Rớt: <strong className="text-destructive">{s.failCount}</strong>
                    </span>
                    <span>
                      Tỉ lệ đậu: <strong className="text-ink">{s.passRatePercent}%</strong>
                    </span>
                  </div>
                </>
              )}
            </ContentBlock>
          ))}
        </div>
      )}
    </LecturerPageShell>
  );
}
