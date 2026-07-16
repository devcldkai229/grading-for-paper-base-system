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
import type { SubjectScoreDistribution } from "@/types/reporting";

type HoveredBucket = { subjectId: string; index: number } | null;

function formatScore(value: number | null): string {
  return value === null ? "—" : value.toString();
}

function ScoreHistogram({
  subject,
  hovered,
  onHover,
}: {
  subject: SubjectScoreDistribution;
  hovered: HoveredBucket;
  onHover: (value: HoveredBucket) => void;
}) {
  const maxCount = Math.max(1, ...subject.histogram.map((b) => b.count));

  return (
    <div className="mt-4">
      <div className="flex items-end gap-0.5 h-32" role="img" aria-label="Biểu đồ phân phối điểm">
        {subject.histogram.map((bucket, index) => {
          const heightPct = (bucket.count / maxCount) * 100;
          const isHovered = hovered?.subjectId === subject.subjectId && hovered.index === index;
          return (
            <div
              key={index}
              className="relative flex-1 h-full flex flex-col items-center justify-end"
              onMouseEnter={() => onHover({ subjectId: subject.subjectId, index })}
              onMouseLeave={() => onHover(null)}
              onFocus={() => onHover({ subjectId: subject.subjectId, index })}
              onBlur={() => onHover(null)}
              tabIndex={0}
            >
              {isHovered && (
                <div className="absolute -top-9 z-10 whitespace-nowrap rounded-md bg-ink px-2 py-1 text-xs text-paper shadow-md">
                  <strong>{bucket.count}</strong> bài ({bucket.rangeStart}–{bucket.rangeEnd})
                </div>
              )}
              <div
                className={`w-full max-w-[24px] rounded-t transition-colors ${
                  isHovered ? "bg-brand-red" : "bg-brand-red/70"
                }`}
                style={{ height: `${heightPct}%`, minHeight: bucket.count > 0 ? "2px" : "0px" }}
              />
            </div>
          );
        })}
      </div>
      <div className="flex justify-between text-xs text-ink-soft mt-1">
        <span>0</span>
        <span>{subject.maxScore}</span>
      </div>
    </div>
  );
}

export function AdminScoreDistributionPage() {
  const [semesterId, setSemesterId] = useState("");
  const [examId, setExamId] = useState("");
  const [semesters, setSemesters] = useState<Semester[]>([]);
  const [exams, setExams] = useState<Exam[]>([]);

  const [subjects, setSubjects] = useState<SubjectScoreDistribution[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [hovered, setHovered] = useState<HoveredBucket>(null);

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
      const result = await reportingService.getScoreDistribution(
        semesterId || undefined,
        examId || undefined
      );
      setSubjects(result.subjects);
    } catch (err) {
      setError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Không tải được báo cáo phân phối điểm."
          : "Không tải được báo cáo phân phối điểm."
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
        title="Phân phối điểm"
        subtitle="Biểu đồ phân phối điểm và thống kê min/trung bình/max theo môn thi."
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
                    TB: <strong className="text-ink">{formatScore(s.scoreAvg)}</strong> · Min{" "}
                    {formatScore(s.scoreMin)} · Max {formatScore(s.scoreMax)}
                  </p>
                </div>
              </div>

              {s.submittedCount === 0 ? (
                <p className="text-sm text-ink-soft mt-4">Chưa có bài nào được chấm xong.</p>
              ) : (
                <ScoreHistogram subject={s} hovered={hovered} onHover={setHovered} />
              )}
            </ContentBlock>
          ))}
        </div>
      )}
    </LecturerPageShell>
  );
}
