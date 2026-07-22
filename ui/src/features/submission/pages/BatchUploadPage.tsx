import { useState, useEffect, useCallback } from "react";
import { useSearchParams, useNavigate, Link } from "react-router-dom";
import { isAxiosError } from "axios";
import { submissionService } from "@/services/submissionService";
import { catalogService } from "@/services/catalogService";
import { startGradingFlow } from "@/lib/startGradingFlow";
import type { BatchStatus } from "@/types/submission";
import type { SubjectSummary } from "@/types/catalog";
import { PageHeader } from "@/components/layout/PageHeader";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import { PaperCard } from "@/components/ui/paper-card";
import { ContentBlock } from "@/components/ui/content-block";

interface BatchUploadPageProps {
  showStartGrading?: boolean;
  assignmentPath?: string;
}

export function BatchUploadPage({
  showStartGrading = true,
  assignmentPath = "/admin/grading/marker-assignments",
}: BatchUploadPageProps) {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const querySubjectId = searchParams.get("subjectId") || "";
  const queryExamId = searchParams.get("examId") || "";

  const [subjectId, setSubjectId] = useState(querySubjectId);
  const [subjectLabel, setSubjectLabel] = useState<string | null>(null);
  const [examSubjects, setExamSubjects] = useState<SubjectSummary[]>([]);
  const [loadingSubjects, setLoadingSubjects] = useState(false);
  const [uploadMode, setUploadMode] = useState<"zip" | "files">("zip");
  const [file, setFile] = useState<File | null>(null);
  const [looseFiles, setLooseFiles] = useState<File[]>([]);
  const [uploading, setUploading] = useState(false);
  const [batchId, setBatchId] = useState<string | null>(null);
  const [batchStatus, setBatchStatus] = useState<BatchStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [startingGrading, setStartingGrading] = useState(false);
  const [retrying, setRetrying] = useState(false);
  const [pollKey, setPollKey] = useState(0);
  const [deleting, setDeleting] = useState(false);

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
  }, [batchId, pollKey]);

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

  const handleUploadFiles = useCallback(async () => {
    if (!subjectId) {
      setError("Chọn môn thi trước khi upload.");
      return;
    }
    if (looseFiles.length === 0) {
      setError("Chọn ít nhất một file trước khi upload.");
      return;
    }
    setError(null);
    setUploading(true);
    try {
      const result = await submissionService.uploadFiles(subjectId, looseFiles);
      setBatchId(result.batchId);
    } catch (err: unknown) {
      const msg = isAxiosError(err)
        ? err.response?.data?.message ?? "Upload thất bại."
        : "Upload thất bại.";
      setError(msg);
    } finally {
      setUploading(false);
    }
  }, [subjectId, looseFiles]);

  const handleRetry = useCallback(async () => {
    if (!batchId) return;
    setError(null);
    setRetrying(true);
    try {
      await submissionService.retryBatch(batchId);
      setBatchStatus((prev) =>
        prev ? { ...prev, status: "Uploaded", errorMessage: null } : prev
      );
      setPollKey((k) => k + 1);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Không thể thử lại batch.";
      setError(msg);
    } finally {
      setRetrying(false);
    }
  }, [batchId]);

  const handleDeleteBatch = useCallback(async () => {
    if (!batchId) return;
    if (!window.confirm("Xóa batch này cùng toàn bộ bài làm đã tải lên? Hành động này không thể hoàn tác.")) {
      return;
    }
    setError(null);
    setDeleting(true);
    try {
      await submissionService.deleteBatch(batchId);
      setBatchId(null);
      setBatchStatus(null);
      setFile(null);
      setLooseFiles([]);
    } catch (err: unknown) {
      const msg = isAxiosError(err)
        ? err.response?.data?.message ?? "Không thể xóa batch."
        : "Không thể xóa batch.";
      setError(msg);
    } finally {
      setDeleting(false);
    }
  }, [batchId]);

  const handleStartGrading = useCallback(async () => {
    if (!batchId) return;
    setStartingGrading(true);
    setError(null);
    try {
      await startGradingFlow(batchId, navigate);
    } catch (err: unknown) {
      const msg = isAxiosError(err)
        ? err.response?.data?.message ?? "Không thể bắt đầu chấm bài."
        : "Không thể bắt đầu chấm bài.";
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
  const canUploadFiles = Boolean(subjectId && looseFiles.length > 0 && !uploading);
  const looseFilesAccept =
    ".pdf,.png,.jpg,.jpeg,.gif,.bmp,.tiff,.tif,.webp,.txt,.docx";

  const removeLooseFile = (index: number) => {
    setLooseFiles((prev) => prev.filter((_, i) => i !== index));
  };

  return (
    <LecturerPageShell maxWidth="2xl">
      <PageHeader
        title="Upload bài làm"
        subtitle="Upload file ZIP chứa nhiều bài, hoặc các file rời hợp thành một bài. Môn thi được chọn từ danh mục — không cần nhập mã ID."
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

          <div className="flex rounded-lg border border-line bg-secondary/40 p-1 gap-1">
            <button
              type="button"
              onClick={() => setUploadMode("zip")}
              className={`flex-1 py-2 rounded-md text-sm font-medium transition-colors ${
                uploadMode === "zip"
                  ? "bg-card text-ink shadow-sm border border-line"
                  : "text-ink-soft hover:text-ink"
              }`}
            >
              File ZIP (nhiều bài)
            </button>
            <button
              type="button"
              onClick={() => setUploadMode("files")}
              className={`flex-1 py-2 rounded-md text-sm font-medium transition-colors ${
                uploadMode === "files"
                  ? "bg-card text-ink shadow-sm border border-line"
                  : "text-ink-soft hover:text-ink"
              }`}
            >
              File lẻ (1 bài)
            </button>
          </div>

          {uploadMode === "zip" ? (
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
          ) : (
            <div>
              <label className="block text-sm font-medium text-ink-soft mb-2">
                Các file rời của một bài làm
              </label>
              <div
                className={`relative rounded-xl border border-dashed transition-colors ${
                  looseFiles.length > 0
                    ? "border-done/50 bg-done/5"
                    : "border-line bg-secondary/40 hover:border-brand-red/30"
                } ${!subjectId ? "opacity-50 pointer-events-none" : ""}`}
              >
                <input
                  type="file"
                  multiple
                  accept={looseFilesAccept}
                  disabled={!subjectId}
                  onChange={(e) =>
                    setLooseFiles(Array.from(e.target.files ?? []))
                  }
                  className="absolute inset-0 w-full h-full opacity-0 cursor-pointer disabled:cursor-not-allowed"
                />
                <div className="px-6 py-10 text-center pointer-events-none">
                  {looseFiles.length > 0 ? (
                    <p className="font-medium text-ink">
                      Đã chọn {looseFiles.length} file
                    </p>
                  ) : (
                    <>
                      <p className="font-medium text-ink">Chọn hoặc kéo thả các file</p>
                      <p className="text-sm text-ink-soft mt-1">
                        Nhiều file rời (PDF/ảnh/docx/txt) hợp thành một bài làm — sắp xếp
                        theo thứ tự chọn, xử lý ngay lập tức
                      </p>
                    </>
                  )}
                </div>
              </div>

              {looseFiles.length > 0 && (
                <ul className="mt-3 space-y-1.5">
                  {looseFiles.map((f, index) => (
                    <li
                      key={`${f.name}-${index}`}
                      className="flex items-center justify-between gap-3 px-3 py-2 bg-secondary/40 border border-line rounded-lg text-sm"
                    >
                      <span className="text-ink-soft shrink-0 font-mono">
                        #{index + 1}
                      </span>
                      <span className="text-ink truncate flex-1">{f.name}</span>
                      <span className="text-ink-soft text-xs shrink-0">
                        {(f.size / 1024 / 1024).toFixed(2)} MB
                      </span>
                      <button
                        type="button"
                        onClick={() => removeLooseFile(index)}
                        className="text-destructive hover:underline text-xs shrink-0"
                      >
                        Xóa
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}

          {error && (
            <div className="p-3 bg-destructive/10 border border-destructive/30 rounded-lg text-destructive text-sm">
              {error}
            </div>
          )}

          <button
            type="button"
            onClick={() => void (uploadMode === "zip" ? handleUpload() : handleUploadFiles())}
            disabled={uploadMode === "zip" ? !canUpload : !canUploadFiles}
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
            {batchStatus && batchStatus.duplicateWarnings.length > 0 && (
              <div className="p-3 bg-brand-orange/10 border border-brand-orange/30 rounded-lg text-sm space-y-2">
                <p className="font-medium text-brand-orange">
                  Phát hiện {batchStatus.duplicateWarnings.length} nhóm file trùng nội dung
                </p>
                <ul className="space-y-1 text-ink-soft">
                  {batchStatus.duplicateWarnings.map((warning) => (
                    <li key={warning.contentHash}>
                      {warning.files
                        .map((f) => f.studentAlias ?? f.fileName ?? "?")
                        .join(", ")}{" "}
                      có nội dung giống hệt nhau
                    </li>
                  ))}
                </ul>
              </div>
            )}
            {batchStatus?.status === "Failed" && (
              <button
                type="button"
                onClick={() => void handleRetry()}
                disabled={retrying}
                className="w-full py-3 mt-2 bg-primary text-primary-foreground hover:opacity-90 disabled:opacity-50 rounded-lg font-medium transition-opacity"
              >
                {retrying ? (
                  <span className="flex items-center justify-center gap-2">
                    <div className="animate-spin w-4 h-4 border-2 border-line border-t-white rounded-full" />
                    Đang thử lại...
                  </span>
                ) : (
                  "Thử lại"
                )}
              </button>
            )}
            {batchStatus?.status === "Ready" && (
              <>
                <div className="p-3 bg-done/10 border border-done/25 rounded-lg text-done text-sm">
                  Xử lý thành công. {batchStatus.totalPapers} bài đã được nạp.
                </div>
                {showStartGrading ? <button
                  type="button"
                  onClick={() => void handleStartGrading()}
                  disabled={startingGrading}
                  className="w-full py-3 mt-2 bg-primary text-primary-foreground hover:opacity-90 disabled:opacity-50 rounded-lg font-medium transition-opacity"
                >
                  {startingGrading ? "Đang khởi tạo..." : "Tiến hành chấm bài"}
                </button> : <Link
                  to={`${assignmentPath}?subjectId=${subjectId}&batchId=${batchId}`}
                  className="block w-full py-3 mt-2 bg-primary text-primary-foreground hover:opacity-90 rounded-lg font-medium text-center transition-opacity"
                >
                  Phân phối bài chấm
                </Link>}
              </>
            )}
            {(batchStatus?.status === "Ready" || batchStatus?.status === "Failed") && (
              <button
                type="button"
                onClick={() => void handleDeleteBatch()}
                disabled={deleting}
                className="w-full py-3 mt-2 border border-destructive/30 text-destructive hover:bg-destructive/10 disabled:opacity-50 rounded-lg font-medium transition-colors"
              >
                {deleting ? "Đang xóa..." : "Xóa batch"}
              </button>
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
