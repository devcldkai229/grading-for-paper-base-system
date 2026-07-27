import { useEffect, useState, useCallback } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { isAxiosError } from "axios";
import { Download, FolderOpen } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import {
  CatalogEmptyState,
  CatalogErrorState,
  CatalogListSkeleton,
} from "@/components/catalog/CatalogPageShell";
import { FilterBar, ContentBlockButton } from "@/components/ui/content-block";
import { ListPagination } from "@/components/catalog/ListPagination";
import { Button } from "@/components/ui/button";
import { gradingService } from "@/services/gradingService";
import type { GradingQueueFolder, GradingQueueRow } from "@/types/grading";

const PAGE_SIZE = 20;
const DEBOUNCE_MS = 350;

type QueueFilter = "all" | "NotStarted" | "Drafting" | "Submitted" | "flagged";

const filterTabs: { key: QueueFilter; label: string }[] = [
  { key: "all", label: "Tất cả" },
  { key: "NotStarted", label: "Chờ chấm" },
  { key: "Drafting", label: "Đang chấm" },
  { key: "Submitted", label: "Đã chấm" },
  { key: "flagged", label: "Đã gắn cờ" },
];

const statusLabels: Record<string, string> = {
  NotStarted: "Chờ chấm",
  Drafting: "Đang chấm",
  Submitted: "Đã chấm",
};

const statusColors: Record<string, string> = {
  NotStarted: "bg-secondary text-ink-soft border-line",
  Drafting: "bg-brand-orange/10 text-brand-orange border-brand-orange/25",
  Submitted: "bg-done/10 text-done border-done/25",
};

export function GradingQueuePage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const [filter, setFilter] = useState<QueueFilter>("all");
  const [aliasInput, setAliasInput] = useState("");
  const [debouncedAlias, setDebouncedAlias] = useState("");
  const [selectedBatchId, setSelectedBatchId] = useState<string | null>(
    searchParams.get("batchId")
  );

  const [folders, setFolders] = useState<GradingQueueFolder[]>([]);
  const [foldersLoading, setFoldersLoading] = useState(true);

  const [rows, setRows] = useState<GradingQueueRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedAlias(aliasInput.trim()), DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [aliasInput]);

  useEffect(() => {
    setPage(1);
  }, [filter, debouncedAlias, selectedBatchId]);

  const loadFolders = useCallback(async () => {
    setFoldersLoading(true);
    try {
      const data = await gradingService.listQueueFolders();
      setFolders(data);
    } catch {
      setFolders([]);
    } finally {
      setFoldersLoading(false);
    }
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await gradingService.listQueue({
        status: filter === "all" || filter === "flagged" ? undefined : filter,
        flagged: filter === "flagged" ? true : undefined,
        alias: debouncedAlias || undefined,
        batchId: selectedBatchId ?? undefined,
        page,
        pageSize: PAGE_SIZE,
      });
      setRows(result.items);
      setTotalPages(result.totalPages);
      setTotalCount(result.totalCount);
    } catch (err) {
      setError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Không tải được hàng chờ chấm bài."
          : "Không tải được hàng chờ chấm bài."
      );
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [filter, debouncedAlias, selectedBatchId, page]);

  useEffect(() => {
    void loadFolders();
  }, [loadFolders]);

  useEffect(() => {
    load();
  }, [load]);

  const selectFolder = (batchId: string | null) => {
    setSelectedBatchId(batchId);
    if (batchId) {
      setSearchParams({ batchId });
    } else {
      setSearchParams({});
    }
  };

  const filterBtn = (active: boolean) =>
    active
      ? "bg-primary text-primary-foreground border-primary"
      : "bg-card text-ink-soft hover:bg-secondary border-line";

  const folderLabel = (f: GradingQueueFolder) =>
    f.zipFileName ?? `Folder ${f.batchId.slice(0, 8)}…`;

  const subjectLabel = (code?: string | null, name?: string | null) => {
    if (code && name) return `[${code}] ${name}`;
    return code || name || null;
  };

  const handleExportCsv = async () => {
    setExporting(true);
    setExportError(null);
    try {
      const { blob, fileName } = await gradingService.exportQueueCsv(
        selectedBatchId ?? undefined
      );
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
    } catch (err) {
      setExportError(
        isAxiosError(err)
          ? err.response?.status === 404
            ? "Không có điểm để xuất trong phạm vi đang chọn."
            : err.response?.data?.message ?? "Xuất CSV thất bại."
          : "Xuất CSV thất bại."
      );
    } finally {
      setExporting(false);
    }
  };

  return (
    <LecturerPageShell>
      <div className="flex flex-wrap items-start justify-between gap-3 mb-2">
        <PageHeader
          title="Hàng chờ chấm bài"
          subtitle="Folder được admin giao và danh sách bài trong từng folder."
        />
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => void handleExportCsv()}
          disabled={exporting || foldersLoading}
          className="gap-1.5"
        >
          <Download className="h-4 w-4" />
          {exporting ? "Đang xuất..." : "Xuất CSV"}
        </Button>
      </div>
      {exportError && (
        <p className="mb-4 text-sm text-brand-red">{exportError}</p>
      )}

      <section className="mb-8">
        <h2 className="text-sm font-semibold text-ink mb-3 flex items-center gap-2">
          <FolderOpen className="h-4 w-4" />
          Folder được giao
        </h2>
        {foldersLoading ? (
          <CatalogListSkeleton />
        ) : folders.length === 0 ? (
          <p className="text-sm text-ink-soft">Chưa có folder nào được giao cho bạn.</p>
        ) : (
          <div className="grid gap-3 sm:grid-cols-2">
            <button
              type="button"
              onClick={() => selectFolder(null)}
              className={`text-left rounded-xl border px-4 py-3 transition-colors ${
                selectedBatchId === null
                  ? "border-primary bg-primary/5"
                  : "border-line bg-card hover:bg-secondary"
              }`}
            >
              <p className="font-medium text-ink">Tất cả folder</p>
              <p className="text-xs text-ink-soft mt-1">Xem mọi bài được giao</p>
            </button>
            {folders.map((f) => (
              <button
                key={f.batchId}
                type="button"
                onClick={() => selectFolder(f.batchId)}
                className={`text-left rounded-xl border px-4 py-3 transition-colors ${
                  selectedBatchId === f.batchId
                    ? "border-primary bg-primary/5"
                    : "border-line bg-card hover:bg-secondary"
                }`}
              >
                <p className="font-medium text-ink truncate">{folderLabel(f)}</p>
                {subjectLabel(f.subjectCode, f.subjectName) && (
                  <p className="text-xs text-ink mt-0.5 truncate">
                    {subjectLabel(f.subjectCode, f.subjectName)}
                  </p>
                )}
                <p className="text-xs text-ink-soft mt-1">
                  {f.totalPapers} bài · Chờ {f.notStarted} · Đang {f.drafting} · Đã {f.submitted}
                </p>
                <p className="text-xs text-ink-soft/70 mt-1">
                  Giao {new Date(f.assignedAt).toLocaleString("vi-VN")}
                </p>
              </button>
            ))}
          </div>
        )}
      </section>

      <FilterBar>
        {filterTabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            onClick={() => setFilter(tab.key)}
            className={`px-4 py-2 rounded-lg text-sm font-medium transition-all border ${filterBtn(filter === tab.key)}`}
          >
            {tab.label}
          </button>
        ))}
      </FilterBar>

      <div className="mb-6">
        <input
          type="text"
          value={aliasInput}
          onChange={(e) => setAliasInput(e.target.value)}
          placeholder="Tìm theo alias, vd: Student_0023"
          className="w-full sm:w-80 px-3 py-2 bg-card border border-line rounded-lg text-sm text-ink outline-none focus:border-primary"
        />
      </div>

      {loading ? (
        <CatalogListSkeleton />
      ) : error ? (
        <CatalogErrorState message={error} onRetry={load} />
      ) : rows.length === 0 ? (
        <CatalogEmptyState message="Không có bài nào khớp bộ lọc." />
      ) : (
        <div className="grid gap-3">
          {rows.map((row) => (
            <ContentBlockButton
              key={row.assignmentId}
              onClick={() => navigate(`/grading/${row.assignmentId}`)}
              className="flex items-center justify-between"
            >
              <div className="flex items-center gap-4">
                <div className="w-10 h-10 rounded-lg bg-secondary flex items-center justify-center text-sm font-score font-bold text-brand-red">
                  {row.aliasNumber ?? "?"}
                </div>
                <div>
                  <h3 className="font-medium text-ink">
                    {row.studentAlias ?? `Paper #${row.aliasNumber ?? "?"}`}
                  </h3>
                  {subjectLabel(row.subjectCode, row.subjectName) && (
                    <p className="text-xs text-ink-soft">
                      {subjectLabel(row.subjectCode, row.subjectName)}
                    </p>
                  )}
                  {row.zipFileName && (
                    <p className="text-xs text-ink-soft">{row.zipFileName}</p>
                  )}
                  {row.totalScore !== null && (
                    <span className="text-xs text-ink-soft">Điểm: {row.totalScore}</span>
                  )}
                </div>
              </div>
              <div className="flex items-center gap-2 shrink-0">
                {row.isFlagged && (
                  <span className="px-2.5 py-1 rounded-full text-xs font-medium border bg-brand-red/10 text-brand-red border-brand-red/25">
                    Đã gắn cờ
                  </span>
                )}
                <span
                  className={`px-2.5 py-1 rounded-full text-xs font-medium border ${statusColors[row.status] || "bg-secondary text-ink-soft border-line"}`}
                >
                  {statusLabels[row.status] || row.status}
                </span>
              </div>
            </ContentBlockButton>
          ))}
        </div>
      )}

      <ListPagination
        page={page}
        totalPages={totalPages}
        totalCount={totalCount}
        pageSize={PAGE_SIZE}
        onPageChange={setPage}
      />
    </LecturerPageShell>
  );
}
