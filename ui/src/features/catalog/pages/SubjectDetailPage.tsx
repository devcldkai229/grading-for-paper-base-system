import { useEffect, useState, useCallback, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { CatalogBreadcrumb } from "@/components/catalog/CatalogBreadcrumb";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import { FilePreviewOverlay } from "@/components/FilePreviewOverlay";
import { catalogService } from "@/services/catalogService";
import { useCatalogPaths } from "@/features/catalog/hooks/useCatalogPaths";
import type { Exam, Semester, SubjectDetail, FileUrlResponse } from "@/types/catalog";
import { isLecturer } from "@/lib/roles";
import { authService } from "@/services/authService";
import { PaperCard } from "@/components/ui/paper-card";

type PreviewType = "exam-paper" | "rubric";

export function SubjectDetailPage() {
  const { subjectId } = useParams<{ subjectId: string }>();
  const [subject, setSubject] = useState<SubjectDetail | null>(null);
  const [semester, setSemester] = useState<Semester | null>(null);
  const [exam, setExam] = useState<Exam | null>(null);
  const [loading, setLoading] = useState(true);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [fileView, setFileView] = useState<FileUrlResponse | null>(null);
  const [fileLoading, setFileLoading] = useState(false);
  const [fileError, setFileError] = useState<string | null>(null);
  const [fileType, setFileType] = useState<PreviewType | null>(null);
  const [originalDownloadLoading, setOriginalDownloadLoading] = useState(false);
  const urlCacheRef = useRef<Record<string, { data: FileUrlResponse; expiresAt: number }>>({});
  const navigate = useNavigate();
  const paths = useCatalogPaths();
  const showUploadBai = !paths.isAdminMode && isLecturer(authService.decodeToken()?.role);

  useEffect(() => {
    if (!subjectId) return;
    setLoading(true);
    catalogService
      .getSubjectDetail(subjectId)
      .then(async (detail) => {
        setSubject(detail);
        const examData = await catalogService.getExam(detail.examId);
        setExam(examData);
        const semesterData = await catalogService.getSemester(examData.semesterId);
        setSemester(semesterData);
      })
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [subjectId]);

  const closePreview = useCallback(() => {
    setPreviewOpen(false);
    setFileType(null);
    setFileError(null);
  }, []);

  const openFile = useCallback(
    async (type: PreviewType) => {
      if (!subjectId || !subject) return;

      setPreviewOpen(true);
      setFileLoading(true);
      setFileError(null);
      setFileType(type);
      setFileView(null);

      const cacheKey =
        type === "exam-paper"
          ? `exam-paper_${subjectId}`
          : `rubric_${subjectId}_v${subject.rubricVersion}`;

      const now = Date.now();
      const cached = urlCacheRef.current[cacheKey];
      if (cached && cached.expiresAt > now) {
        setFileView(cached.data);
        setFileLoading(false);
        return;
      }

      try {
        const data =
          type === "exam-paper"
            ? await catalogService.getExamPaperUrl(subjectId)
            : await catalogService.getRubricUrl(subjectId);

        urlCacheRef.current[cacheKey] = {
          data,
          expiresAt: now + 9 * 60 * 1000,
        };

        setFileView(data);
      } catch {
        setFileError(
          type === "exam-paper"
            ? "Chưa có đề thi hoặc không tải được file."
            : "Chưa có barem hoặc không tải được file."
        );
      } finally {
        setFileLoading(false);
      }
    },
    [subjectId, subject]
  );

  const handleDownloadOriginal = useCallback(async () => {
    if (!subjectId || !fileType) return;
    setOriginalDownloadLoading(true);
    try {
      const data =
        fileType === "exam-paper"
          ? await catalogService.getExamPaperOriginalUrl(subjectId)
          : await catalogService.getRubricOriginalUrl(subjectId);
      const link = document.createElement("a");
      link.href = data.url;
      link.download = data.fileName;
      link.rel = "noopener";
      link.click();
    } catch {
      setFileError("Không tải được bản gốc.");
    } finally {
      setOriginalDownloadLoading(false);
    }
  }, [subjectId, fileType]);

  if (loading) {
    return (
      <div className="p-6 lg:p-8 flex items-center justify-center min-h-[40vh]">
        <div className="animate-spin w-8 h-8 border-2 border-line border-t-brand-red rounded-full" />
      </div>
    );
  }

  if (!subject) {
    return (
      <div className="p-6 lg:p-8 flex items-center justify-center min-h-[40vh] text-ink-soft">
        Không tìm thấy môn thi
      </div>
    );
  }

  const totalScore = subject.questions.reduce((sum, q) => sum + q.maxScore, 0);
  const previewTitle =
    fileType === "exam-paper"
      ? "Đề thi"
      : fileType === "rubric"
        ? "Barem chấm điểm"
        : "Xem file";

  return (
    <>
      <LecturerPageShell maxWidth="7xl">
        <CatalogBreadcrumb
          items={[
            { label: "Học kỳ", to: paths.semesters },
            {
              label: semester ? `${semester.code} — ${semester.name}` : "Học kỳ",
              to: semester ? paths.semesterExams(semester.id) : undefined,
            },
            {
              label: exam ? `${exam.name} (${exam.examType})` : "Kỳ thi",
              to:
                semester && exam
                  ? paths.examSubjects(semester.id, exam.id)
                  : undefined,
            },
            { label: subject.subjectCode },
          ]}
        />

        <div className="flex items-start justify-between mb-8 gap-4 flex-wrap">
          <div>
            <h1 className="font-display text-3xl font-semibold text-brand-red">
              {subject.subjectCode}
            </h1>
            {subject.title && (
              <p className="text-ink-soft mt-1 text-lg">{subject.title}</p>
            )}
            <div className="flex gap-4 mt-3 text-sm text-ink-soft flex-wrap">
              <span>
                Điểm tối đa:{" "}
                <strong className="font-score text-brand-red">{subject.maxScore}</strong>
              </span>
              <span>
                Trạng thái: <strong className="text-ink">{subject.status}</strong>
              </span>
              {subject.hasRubric && <span>Barem v{subject.rubricVersion}</span>}
            </div>
          </div>
          <div className="flex gap-3 flex-wrap">
            <button
              type="button"
              disabled={!subject.hasExamPaper}
              onClick={() => void openFile("exam-paper")}
              className={`px-4 py-2.5 rounded-lg text-sm font-medium border transition-all ${
                subject.hasExamPaper
                  ? "border-brand-red text-brand-red hover:bg-brand-red/5"
                  : "border-line text-ink-soft/50 cursor-not-allowed"
              }`}
            >
              Xem đề thi
            </button>
            <button
              type="button"
              disabled={!subject.hasRubric}
              onClick={() => void openFile("rubric")}
              className={`px-4 py-2.5 rounded-lg text-sm font-medium border transition-all ${
                subject.hasRubric
                  ? "border-brand-orange text-brand-orange hover:bg-brand-orange/5"
                  : "border-line text-ink-soft/50 cursor-not-allowed"
              }`}
            >
              Xem barem
            </button>
            {showUploadBai && (
              <button
                type="button"
                onClick={() => navigate(`/batches/upload?subjectId=${subjectId}`)}
                className="px-4 py-2.5 bg-primary text-primary-foreground hover:opacity-90 rounded-lg text-sm font-medium transition-opacity"
              >
                Upload bài
              </button>
            )}
          </div>
        </div>

        <div>
          <h2 className="font-display text-xl font-semibold mb-4 text-ink">
            Lưới điểm ({subject.questions.length} câu)
          </h2>
          <PaperCard className="overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-secondary border-b border-line">
                  <th className="text-left px-4 py-3 text-ink-soft font-medium">#</th>
                  <th className="text-left px-4 py-3 text-ink-soft font-medium">Nhóm</th>
                  <th className="text-left px-4 py-3 text-ink-soft font-medium">Câu</th>
                  <th className="text-left px-4 py-3 text-ink-soft font-medium">Mô tả</th>
                  <th className="text-right px-4 py-3 text-ink-soft font-medium">Điểm</th>
                </tr>
              </thead>
              <tbody>
                {subject.questions.map((q, i) => (
                  <tr
                    key={q.id}
                    className={`border-t border-line ${i % 2 === 1 ? "bg-paper" : "bg-card"}`}
                  >
                    <td className="px-4 py-3 text-ink-soft">{q.orderIndex + 1}</td>
                    <td className="px-4 py-3 text-ink-soft">{q.groupLabel || "—"}</td>
                    <td className="px-4 py-3 font-medium text-ink">{q.questionNumber}</td>
                    <td className="px-4 py-3 text-ink-soft">{q.label || "—"}</td>
                    <td className="px-4 py-3 text-right font-score font-medium text-brand-red">
                      {q.maxScore}
                    </td>
                  </tr>
                ))}
                <tr className="border-t border-line bg-secondary">
                  <td colSpan={4} className="px-4 py-3 font-semibold text-ink">
                    Tổng
                  </td>
                  <td className="px-4 py-3 text-right font-score font-bold text-lg text-brand-red">
                    {totalScore}
                  </td>
                </tr>
              </tbody>
            </table>
          </PaperCard>
        </div>
      </LecturerPageShell>

      <FilePreviewOverlay
        open={previewOpen}
        title={previewTitle}
        loading={fileLoading}
        error={fileError}
        file={fileView}
        onClose={closePreview}
        onDownloadOriginal={() => void handleDownloadOriginal()}
        originalDownloadLoading={originalDownloadLoading}
      />
    </>
  );
}
