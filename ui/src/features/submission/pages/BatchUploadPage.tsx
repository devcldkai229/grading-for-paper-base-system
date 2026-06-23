import { useState, useEffect, useCallback } from "react";
import { useSearchParams, useNavigate, Link } from "react-router-dom";
import { submissionService } from "@/services/submissionService";
import { catalogService } from "@/services/catalogService";
import { startGradingFlow } from "@/lib/startGradingFlow";
import type { BatchStatus } from "@/types/submission";
import type { SubjectSummary } from "@/types/catalog";
import { PageHeader } from "@/components/layout/PageHeader";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import { PaperCard } from "@/components/ui/paper-card";
import { ContentBlock } from "@/components/ui/content-block";

export function BatchUploadPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const querySubjectId = searchParams.get("subjectId") || "";
  const queryExamId = searchParams.get("examId") || "";

  const [subjectId, setSubjectId] = useState(querySubjectId);
  const [subjectLabel, setSubjectLabel] = useState<string | null>(null);
  const [examSubjects, setExamSubjects] = useState<SubjectSummary[]>([]);
  const [loadingSubjects, setLoadingSubjects] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [batchId, setBatchId] = useState<string | null>(null);
  const [batchStatus, setBatchStatus] = useState<BatchStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [startingGrading, setStartingGrading] = useState(false);

  useEffect(() => {
    setSubjectId(querySubjectId);
  }, [querySubjectId]);

  useEffect(() => {
    if (!subjectId) {
      setSubjectLabel(null);
      return;
    }
    catalogService
      .getSubjectDetail(subjectId)
      .then((s) => setSubjectLabel(`${s.subjectCode}${s.title ? ` — ${s.title}` : ""}`))
      .catch(() => setSubjectLabel(null));
  }, [subjectId]);

  useEffect(() => {
    if (!queryExamId || querySubjectId) return;
    setLoadingSubjects(true);
    catalogService
      .getSubjectsByExam(queryExamId, 1, 100)
      .then((r) => setExamSubjects(r.items))
      .catch(() => setExamSubjects([]))
      .finally(() => setLoadingSubjects(false));
  }, [queryExamId, querySubjectId]);

  useEffect(() => {
    if (!batchId) return;

    const interval = setInterval(async () => {
      try {
        const status = await submissionService.getBatchStatus(batchId);
        setBatchStatus(status);
        if (status.status === "Ready" || status.status === "Failed") {
          clearInterval(interval);
        }
      } catch {
        clearInterval(interval);
      }
    }, 2000);

    return () => clearInterval(interval);
  }, [batchId]);

  const handleUpload = useCallback(async () => {
    if (!subjectId) {
      setError("Chọn môn thi trước khi upload.");
      return;
    }
    if (!file) {
      setError("Chọn file ZIP trước khi upload.");
      return;
    }
    if (!file.name.endsWith(".zip")) {
      setError("Chỉ chấp nhận file .zip.");
      return;
    }
    setError(null);
    setUploading(true);
    try {
      const result = await submissionService.uploadBatch(subjectId, file);
      setBatchId(result.batchId);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Upload thất bại.";
      setError(msg);
    } finally {
      setUploading(false);
    }
  }, [subjectId, file]);

  const handleStartGrading = useCallback(async () => {
    if (!batchId) return;
    setStartingGrading(true);
    setError(null);
    try {
      await startGradingFlow(batchId, navigate);
    } catch (err: unknown) {
      const msg =
        err instanceof Error ? err.message : "Không thể bắt đầu chấm bài.";
      setError(msg);
    } finally {
      setStartingGrading(false);
    }
  }, [batchId, navigate]);

  const statusColors: Record<string, string> = {
    Uploaded: "text-brand-orange",
    Extracting: "text-ink-soft",
    Ready: "text-done",
    Failed: "text-destructive",
  };

  const inputClass =
    "w-full px-4 py-3 bg-card border border-line rounded-lg text-ink placeholder:text-ink-soft/50 focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary/30 transition-all";

  const canUpload = Boolean(subjectId && file && !uploading);

  return (
    <LecturerPageShell maxWidth="2xl">
      <PageHeader
        title="Upload bài làm"
        subtitle="Kéo thả hoặc chọn file ZIP bài làm học sinh. Môn thi được chọn từ danh mục — không cần nhập mã ID."
      />

      {!batchId ? (
        <PaperCard className="p-6 space-y-6">
          {subjectLabel ? (
            <ContentBlock className="p-4 bg-brand-red/5 border-brand-red/25">
              <p className="text-xs text-ink-soft uppercase tracking-wide mb-1">
                Môn thi
              </p>
              <p className="font-semibold text-ink">{subjectLabel}</p>
            </ContentBlock>
          ) : queryExamId && examSubjects.length > 0 ? (
            <div>
              <label
                htmlFor="subject-select"
                className="block text-sm font-medium text-ink-soft mb-2"
              >
                Chọn môn thi
              </label>
              <select
                id="subject-select"
                value={subjectId}
                onChange={(e) => setSubjectId(e.target.value)}
                className={inputClass}
                disabled={loadingSubjects}
              >
                <option value="">— Chọn môn —</option>
                {examSubjects.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.subjectCode}
                    {s.title ? ` — ${s.title}` : ""}
                  </option>
                ))}
              </select>
            </div>
          ) : (
            <ContentBlock className="p-5 text-center">
              <p className="text-ink-soft text-sm mb-3">
                Chưa chọn môn thi. Vào Danh mục thi → chọn môn → bấm Upload bài.
              </p>
              <Link
                to="/catalog/semesters"
                className="text-sm font-medium text-brand-red hover:underline"
              >
                Mở danh mục thi
              </Link>
            </ContentBlock>
          )}

          <div>
            <label className="block text-sm font-medium text-ink-soft mb-2">
              File ZIP bài làm
            </label>
            <div
              className={`relative rounded-xl border border-dashed transition-colors ${
                file
                  ? "border-done/50 bg-done/5"
                  : "border-line bg-secondary/40 hover:border-brand-red/30"
              } ${!subjectId ? "opacity-50 pointer-events-none" : ""}`}
            >
              <input
                type="file"
                accept=".zip"
                disabled={!subjectId}
                onChange={(e) => setFile(e.target.files?.[0] || null)}
                className="absolute inset-0 w-full h-full opacity-0 cursor-pointer disabled:cursor-not-allowed"
              />
              <div className="px-6 py-10 text-center pointer-events-none">
                {file ? (
                  <>
                    <p className="font-medium text-ink">{file.name}</p>
                    <p className="text-sm text-ink-soft mt-1">
                      {(file.size / 1024 / 1024).toFixed(2)} MB
                    </p>
                  </>
                ) : (
                  <>
                    <p className="font-medium text-ink">Chọn hoặc kéo thả file ZIP</p>
                    <p className="text-sm text-ink-soft mt-1">
                      Một file ZIP chứa bài làm của nhiều học sinh
                    </p>
                  </>
                )}
              </div>
            </div>
          </div>

          {error && (
            <div className="p-3 bg-destructive/10 border border-destructive/30 rounded-lg text-destructive text-sm">
              {error}
            </div>
          )}

          <button
            type="button"
            onClick={() => void handleUpload()}
            disabled={!canUpload}
            className="w-full py-3 bg-primary text-primary-foreground hover:opacity-90 disabled:opacity-50 disabled:cursor-not-allowed rounded-lg font-medium transition-opacity"
          >
            {uploading ? (
              <span className="flex items-center justify-center gap-2">
                <div className="animate-spin w-4 h-4 border-2 border-line border-t-white rounded-full" />
                Đang upload...
              </span>
            ) : (
              "Upload"
            )}
          </button>
        </PaperCard>
      ) : (
        <PaperCard className="p-6">
          <h2 className="font-display text-lg font-semibold mb-4 text-ink">
            Trạng thái xử lý
          </h2>
          <div className="space-y-4">
            {subjectLabel && (
              <div className="flex items-center justify-between gap-4">
                <span className="text-ink-soft">Môn thi</span>
                <span className="text-sm font-medium text-ink text-right">{subjectLabel}</span>
              </div>
            )}
            <div className="flex items-center justify-between">
              <span className="text-ink-soft">Batch ID</span>
              <code className="text-sm bg-secondary px-3 py-1 rounded font-mono text-ink">
                {batchId}
              </code>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-ink-soft">Trạng thái</span>
              <span
                className={`font-semibold ${statusColors[batchStatus?.status || "Uploaded"]}`}
              >
                {batchStatus?.status === "Extracting" && (
                  <span className="inline-block mr-2 animate-spin w-4 h-4 border-2 border-line border-t-brand-red rounded-full align-middle" />
                )}
                {batchStatus?.status || "Uploaded"}
              </span>
            </div>
            {batchStatus && batchStatus.totalPapers > 0 && (
              <div className="flex items-center justify-between">
                <span className="text-ink-soft">Số bài</span>
                <span className="font-score font-semibold text-done">
                  {batchStatus.totalPapers}
                </span>
              </div>
            )}
            {batchStatus?.errorMessage && (
              <div className="p-3 bg-destructive/10 border border-destructive/30 rounded-lg text-destructive text-sm">
                {batchStatus.errorMessage}
              </div>
            )}
            {batchStatus?.status === "Ready" && (
              <>
                <div className="p-3 bg-done/10 border border-done/25 rounded-lg text-done text-sm">
                  Xử lý thành công. {batchStatus.totalPapers} bài đã được nạp.
                </div>
                <button
                  type="button"
                  onClick={() => void handleStartGrading()}
                  disabled={startingGrading}
                  className="w-full py-3 mt-2 bg-primary text-primary-foreground hover:opacity-90 disabled:opacity-50 rounded-lg font-medium transition-opacity"
                >
                  {startingGrading ? "Đang khởi tạo..." : "Tiến hành chấm bài"}
                </button>
              </>
            )}
            {error && (
              <div className="p-3 bg-destructive/10 border border-destructive/30 rounded-lg text-destructive text-sm">
                {error}
              </div>
            )}
          </div>
        </PaperCard>
      )}
    </LecturerPageShell>
  );
}
