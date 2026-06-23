import { useEffect } from "react";
import { X } from "lucide-react";
import { FileViewer } from "@/components/FileViewer";
import type { FileUrlResponse } from "@/types/catalog";

interface FilePreviewOverlayProps {
  open: boolean;
  title: string;
  loading?: boolean;
  error?: string | null;
  file?: FileUrlResponse | null;
  onClose: () => void;
  onDownloadOriginal?: () => void;
  originalDownloadLoading?: boolean;
}

export function FilePreviewOverlay({
  open,
  title,
  loading = false,
  error = null,
  file = null,
  onClose,
  onDownloadOriginal,
  originalDownloadLoading = false,
}: FilePreviewOverlayProps) {
  useEffect(() => {
    if (!open) return;

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };

    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", onKeyDown);

    return () => {
      document.body.style.overflow = "";
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6"
      role="dialog"
      aria-modal="true"
      aria-label={title}
    >
      <button
        type="button"
        className="absolute inset-0 bg-ink/40 backdrop-blur-[2px]"
        onClick={onClose}
        aria-label="Đóng"
      />

      <div className="relative z-10 flex flex-col w-full max-w-5xl max-h-[92vh] rounded-2xl border border-line bg-card shadow-xl overflow-hidden">
        <div className="flex items-center justify-between gap-4 px-5 py-4 border-b border-line bg-paper shrink-0">
          <div className="min-w-0">
            <h2 className="font-display text-lg font-semibold text-ink truncate">
              {title}
            </h2>
            {file?.fileName && (
              <p className="text-sm text-ink-soft truncate mt-0.5">
                {file.fileName}
                {file.rubricVersion != null && ` · v${file.rubricVersion}`}
              </p>
            )}
          </div>
          <div className="flex items-center gap-2 shrink-0">
            {file?.hasOriginalDownload && onDownloadOriginal && (
              <button
                type="button"
                disabled={originalDownloadLoading}
                onClick={onDownloadOriginal}
                className="px-3 py-1.5 text-xs border border-line rounded-lg hover:bg-secondary text-ink-soft hover:text-ink disabled:opacity-50"
              >
                {originalDownloadLoading ? "Đang tải…" : "Tải bản gốc (.docx)"}
              </button>
            )}
            <button
              type="button"
              onClick={onClose}
              className="p-2 rounded-lg border border-line hover:bg-secondary text-ink-soft hover:text-ink transition-colors"
              aria-label="Đóng xem trước"
            >
              <X className="h-5 w-5" />
            </button>
          </div>
        </div>

        <div className="flex-1 overflow-auto p-4 sm:p-5 bg-paper min-h-[50vh]">
          {loading && (
            <div className="flex flex-col items-center justify-center py-24 gap-3">
              <div className="animate-spin w-10 h-10 border-2 border-line border-t-brand-red rounded-full" />
              <p className="text-sm text-ink-soft">Đang lấy link xem từ S3...</p>
            </div>
          )}

          {!loading && error && (
            <div className="rounded-xl border border-destructive/30 bg-destructive/5 px-5 py-8 text-center text-destructive text-sm">
              {error}
            </div>
          )}

          {!loading && !error && file && (
            <FileViewer
              url={file.url}
              contentType={file.contentType}
              fileName={file.fileName}
            />
          )}
        </div>
      </div>
    </div>
  );
}
