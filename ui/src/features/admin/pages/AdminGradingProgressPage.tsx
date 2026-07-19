import { useEffect, useState, useCallback } from "react";
import { isAxiosError } from "axios";
import { ChevronDown, ChevronRight } from "lucide-react";
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
import { userService } from "@/services/userService";
import type { Semester, Exam } from "@/types/catalog";
import type { SubjectGradingProgress } from "@/types/reporting";

function formatDateTime(iso: string | null): string {
  if (!iso) return "—";
  return new Date(iso).toLocaleString("vi-VN");
}

function formatThroughput(value: number | null): string {
  if (value === null) return "—";
  return `${value.toFixed(2)} bài/giờ`;
}

function formatAvgMinutes(value: number | null): string {
  if (value === null) return "—";
  return `${value.toFixed(1)} phút/bài`;
}

function formatDeadline(value: string | null): string {
  if (!value) return "—";
  return new Date(value).toLocaleDateString("vi-VN");
}

function isOverdue(value: string | null): boolean {
  if (!value) return false;
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return new Date(value) < today;
}

export function AdminGradingProgressPage() {
  const [semesterId, setSemesterId] = useState("");
  const [examId, setExamId] = useState("");
  const [semesters, setSemesters] = useState<Semester[]>([]);
  const [exams, setExams] = useState<Exam[]>([]);

  const [subjects, setSubjects] = useState<SubjectGradingProgress[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [lecturerNames, setLecturerNames] = useState<Record<string, string>>({});

  useEffect(() => {
    catalogService
      .getSemesters(undefined, 1, 100)
      .then((r) => setSemesters(r.items))
      .catch(() => setSemesters([]));
    userService
      .getUsers(1, 100)
      .then((r) => {
        const map: Record<string, string> = {};
        for (const u of r.items) {
          map[u.id] = u.fullName || u.email;
        }
        setLecturerNames(map);
      })
      .catch(() => setLecturerNames({}));
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
      const result = await reportingService.getGradingProgressDashboard(
        semesterId || undefined,
        examId || undefined
      );
      setSubjects(result.subjects);
    } catch (err) {
      setError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Không tải được tiến độ chấm bài."
          : "Không tải được tiến độ chấm bài."
      );
      setSubjects([]);
    } finally {
      setLoading(false);
    }
  }, [semesterId, examId]);

  useEffect(() => {
    load();
  }, [load]);

  const toggleExpanded = (subjectId: string) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(subjectId)) next.delete(subjectId);
      else next.add(subjectId);
      return next;
    });
  };

  return (
    <LecturerPageShell maxWidth="6xl">
      <PageHeader
        title="Tiến độ chấm bài"
        subtitle="Tỉ lệ hoàn thành, tốc độ chấm và dự kiến hoàn tất theo môn thi và theo giảng viên."
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
          {subjects.map((s) => {
            const isExpanded = expanded.has(s.subjectId);
            return (
              <ContentBlock key={s.subjectId} className="p-5">
                <button
                  type="button"
                  onClick={() => toggleExpanded(s.subjectId)}
                  className="w-full flex items-center justify-between gap-4 text-left"
                >
                  <div className="flex items-center gap-3 min-w-0">
                    {isExpanded ? (
                      <ChevronDown className="h-4 w-4 shrink-0 text-ink-soft" />
                    ) : (
                      <ChevronRight className="h-4 w-4 shrink-0 text-ink-soft" />
                    )}
                    <div className="min-w-0">
                      <h3 className="font-semibold text-ink truncate">
                        {s.subjectCode}
                        {s.title ? ` — ${s.title}` : ""}
                      </h3>
                      <p className="text-xs text-ink-soft">
                        {s.semesterCode} · {s.examName}
                      </p>
                    </div>
                  </div>
                  <div className="text-right shrink-0">
                    <span className="font-score text-xl font-bold text-brand-red">
                      {s.completionPercent}%
                    </span>
                    <p className="text-xs text-ink-soft">
                      {s.completedPapers}/{s.totalPapers} bài
                    </p>
                  </div>
                </button>

                <div className="mt-3 h-2 rounded-full bg-secondary overflow-hidden">
                  <div
                    className="h-full bg-brand-red transition-all"
                    style={{ width: `${Math.min(100, s.completionPercent)}%` }}
                  />
                </div>

                <div className="mt-3 flex flex-wrap gap-x-6 gap-y-1 text-sm text-ink-soft">
                  <span>Đang chấm: {s.draftingPapers}</span>
                  <span>Chưa chấm: {s.notStartedPapers}</span>
                  <span>Tốc độ: {formatThroughput(s.throughputPerHour)}</span>
                  <span>TB thời gian/bài: {formatAvgMinutes(s.avgGradingMinutesPerPaper)}</span>
                  <span>Dự kiến xong: {formatDateTime(s.estimatedFinish)}</span>
                  <span
                    className={
                      isOverdue(s.gradingDeadline) && s.completionPercent < 100
                        ? "text-brand-red font-medium"
                        : undefined
                    }
                  >
                    Hạn chấm: {formatDeadline(s.gradingDeadline)}
                  </span>
                  {s.scoreAvg !== null && (
                    <span>
                      Điểm TB: <strong className="text-ink">{s.scoreAvg}</strong> (min {s.scoreMin}, max{" "}
                      {s.scoreMax})
                    </span>
                  )}
                </div>

                {isExpanded && (
                  <div className="mt-4 border-t border-line pt-4">
                    {s.lecturers.length === 0 ? (
                      <p className="text-sm text-ink-soft">Chưa có giảng viên nào được phân công.</p>
                    ) : (
                      <div className="overflow-x-auto">
                        <table className="w-full text-sm">
                          <thead>
                            <tr className="text-left text-ink-soft border-b border-line">
                              <th className="py-2 pr-4 font-medium">Giảng viên</th>
                              <th className="py-2 pr-4 font-medium">Hoàn thành</th>
                              <th className="py-2 pr-4 font-medium">Đang chấm</th>
                              <th className="py-2 pr-4 font-medium">Chưa chấm</th>
                              <th className="py-2 pr-4 font-medium">Tốc độ</th>
                              <th className="py-2 pr-4 font-medium">TB phút/bài</th>
                              <th className="py-2 pr-4 font-medium">Hoạt động gần nhất</th>
                              <th className="py-2 font-medium">Dự kiến xong</th>
                            </tr>
                          </thead>
                          <tbody>
                            {s.lecturers.map((l) => (
                              <tr key={l.teacherId} className="border-b border-line/50 last:border-0">
                                <td className="py-2 pr-4 text-ink">
                                  {lecturerNames[l.teacherId] || l.teacherId}
                                </td>
                                <td className="py-2 pr-4">
                                  {l.completedCount}/{l.assignedCount}
                                </td>
                                <td className="py-2 pr-4">{l.draftingCount}</td>
                                <td className="py-2 pr-4">{l.notStartedCount}</td>
                                <td className="py-2 pr-4">{formatThroughput(l.throughputPerHour)}</td>
                                <td className="py-2 pr-4">{formatAvgMinutes(l.avgGradingMinutesPerPaper)}</td>
                                <td className="py-2 pr-4">{formatDateTime(l.lastActivityAt)}</td>
                                <td className="py-2">{formatDateTime(l.estimatedFinish)}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </div>
                )}
              </ContentBlock>
            );
          })}
        </div>
      )}
    </LecturerPageShell>
  );
}
