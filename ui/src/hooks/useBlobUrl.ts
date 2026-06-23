import { useEffect, useState } from "react";

export function useBlobUrl(sourceUrl: string | null, mimeType?: string) {
  const [blobUrl, setBlobUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!sourceUrl) {
      setBlobUrl(null);
      setLoading(false);
      setError(null);
      return;
    }

    let cancelled = false;
    let objectUrl: string | null = null;

    setLoading(true);
    setError(null);
    setBlobUrl(null);

    fetch(sourceUrl)
      .then((res) => {
        if (!res.ok) throw new Error("Không tải được file để xem.");
        return res.blob();
      })
      .then((blob) => {
        if (cancelled) return;
        const typedBlob =
          mimeType && blob.type !== mimeType
            ? new Blob([blob], { type: mimeType })
            : blob;
        objectUrl = URL.createObjectURL(typedBlob);
        setBlobUrl(objectUrl);
        setLoading(false);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setError(err instanceof Error ? err.message : "Không tải được file để xem.");
        setLoading(false);
      });

    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [sourceUrl, mimeType]);

  return { blobUrl, loading, error };
}
