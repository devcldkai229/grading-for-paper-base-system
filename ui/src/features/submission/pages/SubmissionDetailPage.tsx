import { useEffect, useState, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { isAxiosError } from "axios";
import { submissionService } from "@/services/submissionService";
import type { StudentPaperDetail, FileUrlResponse } from "@/types/submission";
import { FileViewer } from "@/components/FileViewer";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import { PaperCard } from "@/components/ui/paper-card";
import { ContentBlock } from "@/components/ui/content-block";

export function SubmissionDetailPage() {
  const { paperId } = useParams<{ paperId: string }>();
  const [detail, setDetail] = useState<StudentPaperDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [fileView, setFileView] = useState<FileUrlResponse | null>(null);
  const [fileLoading, setFileLoading] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    if (!paperId) return;
    setLoading(true);
    submissionService
      .getSubmissionDetail(paperId)
      .then(setDetail)
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [paperId]);

  const openFile = useCallback(
    async (fileId: string) => {
      if (!paperId) return;
      setFileLoading(true);
      try {
        const data = await submissionService.getFileUrl(paperId, fileId);
        setFileView(data);
      } catch {
        alert("Không thể tải file. Thử lại sau.");
      } finally {
        setFileLoading(false);
      }
    },
    [paperId]
  );

  const handleDelete = async () => {
    if (!paperId || !detail) return;
    const label = detail.studentAlias || `#${detail.aliasNumber}`;
    if (!window.confirm(`Xóa bài làm "${label}"? Hành động này không thể hoàn tác.`)) {
      return;
    }
    setDeleteError(null);
    setDeleting(true);
    try {
      await submissionService.deleteSubmission(paperId);
      navigate(-1);
    } catch (err) {
      setDeleteError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Không thể xóa bài làm."
          : "Không thể xóa bài làm."
      );
    } finally {
      setDeleting(false);
    }
  };

  if (loading) {
    return (
      <div className="p-6 lg:p-8 flex items-center justify-center min-h-[40vh]">
        <div className="animate-spin w-8 h-8 border-2 border-line border-t-brand-red rounded-full" />
      </div>
    );
  }

  if (!detail) {
    return (
      <div className="p-6 lg:p-8 flex items-center justify-center min-h-[40vh] text-ink-soft">
        Không tìm thấy bài làm
      </div>
    );
  }

  return (
    <LecturerPageShell maxWidth="7xl">
      <nav className="flex items-center gap-2 text-sm text-ink-soft mb-6 pb-4 border-b border-line">
        <button
          onClick={() => navigate(-1)}
          className="hover:text-brand-red transition-colors"
        >
          ← Bài làm
        </button>
        <span>/</span>
        <span className="text-ink font-medium">
          {detail.studentAlias || `#${detail.aliasNumber}`}
        </span>
      </nav>

      <div className="mb-8 flex items-start justify-between gap-4">
        <div>
          <h1 className="font-display text-3xl font-semibold text-ink">
            {detail.studentAlias || `Bài làm #${detail.aliasNumber}`}
          </h1>
          <div className="flex gap-4 mt-2 text-sm text-ink-soft">
            <span>
              Trạng thái: <strong className="text-ink">{detail.status}</strong>
            </span>
            <span>{detail.files.length} file</span>
          </div>
          {deleteError && (
            <div className="mt-3 p-3 bg-destructive/10 border border-destructive/30 rounded-lg text-destructive text-sm">
              {deleteError}
            </div>
          )}
        </div>
        {detail.status === "ReadyToAssign" && (
          <button
            type="button"
            onClick={() => void handleDelete()}
            disabled={deleting}
            className="shrink-0 px-4 py-2 rounded-lg text-sm font-medium border border-destructive/30 text-destructive hover:bg-destructive/10 disabled:opacity-50 transition-colors"
          >
            {deleting ? "Đang xóa..." : "Xóa bài làm"}
          </button>
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        <div>
          <h2 className="font-display text-lg font-semibold mb-4 text-ink">
            Danh sách file
          </h2>
          <div className="space-y-2">
            {detail.files.map((f) => (
              <ContentBlock
                key={f.id}
                interactive
                className={`p-3 cursor-pointer ${
                  fileView?.fileName === f.fileName
                    ? "border-brand-red/50 bg-brand-red/5"
                    : ""
                }`}
                onClick={() => openFile(f.id)}
              >
                <div className="flex items-center justify-between">
                  <span className="font-medium text-sm truncate">
                    {f.fileName || `File ${f.orderIndex + 1}`}
                  </span>
                  <span className="text-xs text-ink-soft ml-2 shrink-0">
                    {f.sizeBytes ? `${(f.sizeBytes / 1024).toFixed(0)} KB` : ""}
                  </span>
                </div>
                <span className="text-xs text-ink-soft">{f.contentType}</span>
              </ContentBlock>
            ))}
          </div>
        </div>

        <div className="lg:col-span-2">
          {fileLoading && (
            <div className="flex justify-center py-20 rounded-xl border border-dashed border-line bg-secondary/30">
              <div className="animate-spin w-8 h-8 border-2 border-line border-t-brand-red rounded-full" />
            </div>
          )}
          {fileView && !fileLoading && (
            <PaperCard className="p-2 overflow-hidden" variant="solid">
              <FileViewer
                url={fileView.url}
                contentType={fileView.contentType}
                fileName={fileView.fileName}
              />
            </PaperCard>
          )}
          {!fileView && !fileLoading && (
            <div className="flex flex-col items-center justify-center py-20 text-ink-soft rounded-xl border border-dashed border-line bg-secondary/30">
              <p>Chọn file bên trái để xem</p>
            </div>
          )}
        </div>
      </div>
    </LecturerPageShell>
  );
}
