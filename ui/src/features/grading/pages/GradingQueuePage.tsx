import { useEffect, useState, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { isAxiosError } from "axios";
import { PageHeader } from "@/components/layout/PageHeader";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import {
  CatalogEmptyState,
  CatalogErrorState,
  CatalogListSkeleton,
} from "@/components/catalog/CatalogPageShell";
import { FilterBar, ContentBlockButton } from "@/components/ui/content-block";
import { ListPagination } from "@/components/catalog/ListPagination";
import { gradingService } from "@/services/gradingService";
import type { GradingQueueRow } from "@/types/grading";

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

  const [filter, setFilter] = useState<QueueFilter>("all");
  const [aliasInput, setAliasInput] = useState("");
  const [debouncedAlias, setDebouncedAlias] = useState("");

  const [rows, setRows] = useState<GradingQueueRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  // Debounce the free-text alias search so we don't fire a request on every keystroke.
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedAlias(aliasInput.trim()), DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [aliasInput]);

  // Reset to page 1 whenever a filter changes.
  useEffect(() => {
    setPage(1);
  }, [filter, debouncedAlias]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await gradingService.listQueue({
        status: filter === "all" || filter === "flagged" ? undefined : filter,
        flagged: filter === "flagged" ? true : undefined,
        alias: debouncedAlias || undefined,
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
  }, [filter, debouncedAlias, page]);

  useEffect(() => {
    load();
  }, [load]);

  const filterBtn = (active: boolean) =>
    active
      ? "bg-primary text-primary-foreground border-primary"
      : "bg-card text-ink-soft hover:bg-secondary border-line";

  return (
    <LecturerPageShell>
      <PageHeader
        title="Hàng chờ chấm bài"
        subtitle="Danh sách bài được giao cho bạn — lọc theo trạng thái hoặc tìm theo alias."
      />

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
