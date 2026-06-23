import { useEffect, useState, useCallback, useRef } from "react";
import { useParams } from "react-router-dom";
import { isAxiosError } from "axios";
import { CatalogBreadcrumb } from "@/components/catalog/CatalogBreadcrumb";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import { FilePreviewOverlay } from "@/components/FilePreviewOverlay";
import { PaperCard } from "@/components/ui/paper-card";
import { catalogService } from "@/services/catalogService";
import { adminCatalogService } from "@/services/adminCatalogService";
import { useCatalogPaths } from "@/features/catalog/hooks/useCatalogPaths";
import {
  ScoreGridEditor,
  type ScoreGridRow,
} from "@/features/admin/components/ScoreGridEditor";
import type { Exam, Semester, SubjectDetail, FileUrlResponse } from "@/types/catalog";

type PreviewType = "exam-paper" | "rubric";
type GridMode = "saved" | "draft";

export function AdminSubjectDetailPage() {
  const { subjectId } = useParams<{ subjectId: string }>();
  const paths = useCatalogPaths();

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

  const [gridMode, setGridMode] = useState<GridMode>("saved");
  const [draftRows, setDraftRows] = useState<ScoreGridRow[]>([]);
  const [draftWarnings, setDraftWarnings] = useState<string[]>([]);

  const [uploadingPaper, setUploadingPaper] = useState(false);
  const [uploadingRubric, setUploadingRubric] = useState(false);
  const [extracting, setExtracting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  const paperInputRef = useRef<HTMLInputElement>(null);
  const rubricInputRef = useRef<HTMLInputElement>(null);

  const loadSubject = useCallback(async () => {
    if (!subjectId) return;
    setLoading(true);
    try {
      const detail = await catalogService.getSubjectDetail(subjectId);
      setSubject(detail);
      const examData = await catalogService.getExam(detail.examId);
      setExam(examData);
      const semesterData = await catalogService.getSemester(examData.semesterId);
      setSemester(semesterData);
      setGridMode("saved");
      setDraftRows([]);
      setDraftWarnings([]);
    } catch (err) {
      console.error(err);
      setSubject(null);
    } finally {
      setLoading(false);
    }
  }, [subjectId]);

  useEffect(() => {
    void loadSubject();
  }, [loadSubject]);

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

  const handleUploadPaper = async (file: File) => {
    if (!subjectId) return;
    setUploadingPaper(true);
    setActionError(null);
    setActionSuccess(null);
    try {
      await adminCatalogService.uploadExamPaper(subjectId, file);
      urlCacheRef.current = {};
      setActionSuccess("Đã upload đề thi.");
      await loadSubject();
    } catch (err) {
      setActionError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Upload đề thi thất bại."
          : "Upload đề thi thất bại."
      );
    } finally {
      setUploadingPaper(false);
    }
  };

  const handleUploadRubric = async (file: File) => {
    if (!subjectId) return;
    setUploadingRubric(true);
    setActionError(null);
    setActionSuccess(null);
    try {
      const result = await adminCatalogService.uploadRubric(subjectId, file);
      urlCacheRef.current = {};
      setActionSuccess(
        `Đã upload barem (v${result.rubricVersion}). Gợi ý trích lưới AI và lưu lại.`
      );
      await loadSubject();
    } catch (err) {
      setActionError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Upload barem thất bại."
          : "Upload barem thất bại."
      );
    } finally {
      setUploadingRubric(false);
    }
  };

  const handleExtractGrid = async () => {
    if (!subjectId || !subject) return;
    setExtracting(true);
    setActionError(null);
    setActionSuccess(null);
    try {
      const result = await adminCatalogService.extractRubricGrid(subjectId);
      setDraftRows(
        result.questions.map((q, i) => ({
          groupLabel: q.groupLabel,
          questionNumber: q.questionNumber,
          label: q.label,
          maxScore: q.maxScore,
          orderIndex: i,
          confidence: q.confidence,
          warning:
            q.confidence < 0.6 ? `Độ tin cậy thấp (${Math.round(q.confidence * 100)}%)` : undefined,
        }))
      );
      setDraftWarnings(result.warnings);
      setGridMode("draft");
      setActionSuccess("Đã trích lưới từ barem. Kiểm tra và lưu khi sẵn sàng.");
    } catch (err) {
      setActionError(
        isAxiosError(err)
          ? err.response?.data?.message ??
              "Trích lưới AI thất bại. Bạn có thể nhập tay."
          : "Trích lưới AI thất bại."
      );
      setDraftRows([]);
      setGridMode("draft");
    } finally {
      setExtracting(false);
    }
  };

  const startManualGrid = () => {
    setDraftRows([
      {
        groupLabel: null,
        questionNumber: "1",
        label: "",
        maxScore: 0,
        orderIndex: 0,
      },
    ]);
    setDraftWarnings([]);
    setGridMode("draft");
    setActionError(null);
  };

  const handleSaveGrid = async () => {
    if (!subjectId || !subject) return;
    if (draftRows.length === 0) {
      setActionError("Lưới điểm trống.");
      return;
    }
    setSaving(true);
    setActionError(null);
    setActionSuccess(null);
    try {
      const result = await adminCatalogService.saveQuestions(
        subjectId,
        draftRows.map((r, i) => ({
          groupLabel: r.groupLabel,
          questionNumber: r.questionNumber,
          label: r.label,
          maxScore: r.maxScore,
          orderIndex: i,
        }))
      );
      setDraftWarnings(result.warnings);
      setActionSuccess("Đã lưu lưới điểm.");
      setGridMode("saved");
      setDraftRows([]);
      await loadSubject();
    } catch (err) {
      setActionError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Lưu lưới điểm thất bại."
          : "Lưu lưới điểm thất bại."
      );
    } finally {
      setSaving(false);
    }
  };

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

  const previewTitle =
    fileType === "exam-paper"
      ? "Đề thi"
      : fileType === "rubric"
        ? "Barem chấm điểm"
        : "Xem file";

  const savedRows: ScoreGridRow[] = subject.questions.map((q) => ({
    groupLabel: q.groupLabel,
    questionNumber: q.questionNumber,
    label: q.label,
    maxScore: q.maxScore,
    orderIndex: q.orderIndex,
  }));

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

        <div className="flex items-start justify-between mb-6 gap-4 flex-wrap">
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
        </div>

        {(actionError || actionSuccess) && (
          <div
            className={`mb-6 rounded-lg border p-3 text-sm ${
              actionError
                ? "border-destructive/30 bg-destructive/5 text-destructive"
                : "border-done/30 bg-done/5 text-done"
            }`}
          >
            {actionError ?? actionSuccess}
          </div>
        )}

        <div className="grid gap-6 lg:grid-cols-2 mb-8">
          <PaperCard className="p-5 space-y-4">
            <h2 className="font-display text-lg font-semibold text-ink">Đề thi</h2>
            <p className="text-sm text-ink-soft">
              {subject.hasExamPaper
                ? subject.examPaperFileName ?? "Đã có file đề"
                : "Chưa upload đề thi"}
            </p>
            <div className="flex flex-wrap gap-2">
              <input
                ref={paperInputRef}
                type="file"
                accept=".pdf,.docx,.txt,image/jpeg,image/png,image/webp,.jpg,.jpeg,.png,.webp"
                className="hidden"
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  if (f) void handleUploadPaper(f);
                  e.target.value = "";
                }}
              />
              <button
                type="button"
                disabled={uploadingPaper}
                onClick={() => paperInputRef.current?.click()}
                className="px-4 py-2 rounded-lg text-sm font-medium bg-primary text-primary-foreground hover:opacity-90 disabled:opacity-50"
              >
                {uploadingPaper ? "Đang upload…" : "Upload đề"}
              </button>
              <button
                type="button"
                disabled={!subject.hasExamPaper}
                onClick={() => void openFile("exam-paper")}
                className="px-4 py-2 rounded-lg text-sm font-medium border border-brand-red text-brand-red hover:bg-brand-red/5 disabled:opacity-50"
              >
                Xem đề
              </button>
            </div>
          </PaperCard>

          <PaperCard className="p-5 space-y-4">
            <h2 className="font-display text-lg font-semibold text-ink">Barem chấm điểm</h2>
            <p className="text-sm text-ink-soft">
              {subject.hasRubric
                ? `${subject.rubricFileName ?? "Barem"} (v${subject.rubricVersion})`
                : "Chưa upload barem"}
            </p>
            <div className="flex flex-wrap gap-2">
              <input
                ref={rubricInputRef}
                type="file"
                accept=".pdf,.docx,.txt,image/jpeg,image/png,image/webp,.jpg,.jpeg,.png,.webp"
                className="hidden"
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  if (f) void handleUploadRubric(f);
                  e.target.value = "";
                }}
              />
              <button
                type="button"
                disabled={uploadingRubric}
                onClick={() => rubricInputRef.current?.click()}
                className="px-4 py-2 rounded-lg text-sm font-medium bg-brand-orange text-white hover:opacity-90 disabled:opacity-50"
              >
                {uploadingRubric ? "Đang upload…" : "Upload barem"}
              </button>
              <button
                type="button"
                disabled={!subject.hasRubric}
                onClick={() => void openFile("rubric")}
                className="px-4 py-2 rounded-lg text-sm font-medium border border-brand-orange text-brand-orange hover:bg-brand-orange/5 disabled:opacity-50"
              >
                Xem barem
              </button>
              <button
                type="button"
                disabled={!subject.hasRubric || extracting}
                onClick={() => void handleExtractGrid()}
                className="px-4 py-2 rounded-lg text-sm font-medium border border-line text-ink hover:bg-secondary disabled:opacity-50"
              >
                {extracting ? "Đang trích…" : "Trích lưới AI"}
              </button>
            </div>
          </PaperCard>
        </div>

        <div className="space-y-4">
          <div className="flex items-center justify-between gap-4 flex-wrap">
            <h2 className="font-display text-xl font-semibold text-ink">
              Lưới điểm
              {gridMode === "saved" && ` (${subject.questions.length} câu)`}
              {gridMode === "draft" && " — bản nháp"}
            </h2>
            <div className="flex gap-2 flex-wrap">
              {gridMode === "saved" && (
                <>
                  <button
                    type="button"
                    onClick={startManualGrid}
                    className="px-3 py-1.5 text-sm border border-line rounded-lg hover:bg-secondary"
                  >
                    Nhập tay
                  </button>
                  {subject.hasRubric && (
                    <button
                      type="button"
                      disabled={extracting}
                      onClick={() => void handleExtractGrid()}
                      className="px-3 py-1.5 text-sm border border-brand-orange text-brand-orange rounded-lg hover:bg-brand-orange/5 disabled:opacity-50"
                    >
                      Trích từ barem
                    </button>
                  )}
                </>
              )}
              {gridMode === "draft" && (
                <>
                  <button
                    type="button"
                    onClick={() => {
                      setGridMode("saved");
                      setDraftRows([]);
                      setDraftWarnings([]);
                    }}
                    className="px-3 py-1.5 text-sm border border-line rounded-lg hover:bg-secondary"
                  >
                    Hủy
                  </button>
                  <button
                    type="button"
                    disabled={saving}
                    onClick={() => void handleSaveGrid()}
                    className="px-3 py-1.5 text-sm bg-primary text-primary-foreground rounded-lg hover:opacity-90 disabled:opacity-50"
                  >
                    {saving ? "Đang lưu…" : "Lưu lưới điểm"}
                  </button>
                </>
              )}
            </div>
          </div>

          {gridMode === "draft" ? (
            <ScoreGridEditor
              rows={draftRows}
              subjectMaxScore={subject.maxScore}
              warnings={draftWarnings}
              onChange={setDraftRows}
            />
          ) : savedRows.length > 0 ? (
            <ScoreGridEditor
              rows={savedRows}
              subjectMaxScore={subject.maxScore}
              onChange={() => {}}
              readOnly
            />
          ) : (
            <PaperCard className="p-8 text-center text-ink-soft">
              Chưa có lưới điểm. Upload barem và trích AI, hoặc nhập tay.
            </PaperCard>
          )}
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
