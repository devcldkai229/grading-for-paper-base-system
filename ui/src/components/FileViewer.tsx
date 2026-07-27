import { useEffect, useState } from "react";
import { useBlobUrl } from "@/hooks/useBlobUrl";

interface FileViewerProps {
  url: string;
  contentType: string;
  fileName: string;
  className?: string;
}

const btnClass =
  "px-3 py-1.5 bg-secondary hover:bg-line text-ink rounded-md text-sm font-medium transition-colors border border-line";

export function FileViewer({
  url,
  contentType,
  fileName,
  className = "",
}: FileViewerProps) {
  const { blobUrl, loading, error } = useBlobUrl(url, contentType);
  const [scale, setScale] = useState(1);

  if (loading) {
    return (
      <div className={`flex items-center justify-center py-24 ${className}`}>
        <div className="animate-spin w-10 h-10 border-2 border-line border-t-brand-red rounded-full" />
      </div>
    );
  }

  if (error || !blobUrl) {
    return (
      <div className={`rounded-xl border border-destructive/30 bg-destructive/5 px-5 py-8 text-center text-destructive text-sm ${className}`}>
        {error ?? "Không hiển thị được file."}
      </div>
    );
  }

  if (contentType === "application/pdf") {
    return (
      <div className={`file-viewer file-viewer--pdf ${className}`}>
        <embed
          src={blobUrl}
          type="application/pdf"
          title={fileName}
          className="w-full min-h-[600px] h-[75vh] rounded-lg border border-line bg-card"
        />
        <p className="mt-3 text-center text-sm text-ink-soft">
          Không hiển thị được?{" "}
          <a
            href={blobUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="text-brand-red hover:underline"
          >
            Mở PDF trong tab mới
          </a>
        </p>
      </div>
    );
  }

  if (contentType.startsWith("image/")) {
    return (
      <div className={`file-viewer file-viewer--image ${className}`}>
        <div className="flex items-center gap-2 mb-3">
          <button
            type="button"
            onClick={() => setScale((s) => Math.max(0.25, s - 0.25))}
            className={btnClass}
          >
            −
          </button>
          <span className="text-sm text-ink-soft min-w-[4rem] text-center font-score">
            {Math.round(scale * 100)}%
          </span>
          <button
            type="button"
            onClick={() => setScale((s) => Math.min(4, s + 0.25))}
            className={btnClass}
          >
            +
          </button>
          <button type="button" onClick={() => setScale(1)} className={btnClass}>
            Reset
          </button>
        </div>
        <div className="overflow-auto max-h-[700px] rounded-lg border border-line bg-card p-2">
          <img
            src={blobUrl}
            alt={fileName}
            draggable={false}
            onContextMenu={(e) => e.preventDefault()}
            style={{ transform: `scale(${scale})`, transformOrigin: "top left" }}
            className="transition-transform duration-200 select-none"
          />
        </div>
      </div>
    );
  }

  if (contentType === "text/plain") {
    return (
      <div className={`file-viewer file-viewer--text ${className}`}>
        <TextFileViewer blobUrl={blobUrl} />
      </div>
    );
  }

  return (
    <div className={`file-viewer file-viewer--unsupported ${className}`}>
      <div className="flex flex-col items-center justify-center gap-4 p-8 rounded-lg border border-line bg-card text-center">
        <p className="text-ink-soft text-sm">{fileName}</p>
        <p className="text-ink-soft text-sm">
          Định dạng này chưa hỗ trợ xem trực tiếp. Vui lòng upload PDF hoặc ảnh.
        </p>
      </div>
    </div>
  );
}

function TextFileViewer({ blobUrl }: { blobUrl: string }) {
  const [content, setContent] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch(blobUrl)
      .then((r) => r.text())
      .then((text) => {
        setContent(text);
        setLoading(false);
      })
      .catch(() => {
        setContent("Không tải được nội dung file.");
        setLoading(false);
      });
  }, [blobUrl]);

  if (loading) {
    return (
      <div className="flex items-center justify-center p-8">
        <div className="animate-spin w-6 h-6 border-2 border-line border-t-brand-red rounded-full" />
      </div>
    );
  }

  return (
    <pre className="p-4 rounded-lg border border-line bg-card text-ink text-sm overflow-auto max-h-[600px] whitespace-pre-wrap font-mono">
      {content}
    </pre>
  );
}
