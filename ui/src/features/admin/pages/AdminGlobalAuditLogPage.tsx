import { useEffect, useState, useCallback } from "react";
import { isAxiosError } from "axios";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import { PageHeader } from "@/components/layout/PageHeader";
import {
  CatalogEmptyState,
  CatalogErrorState,
  CatalogListSkeleton,
} from "@/components/catalog/CatalogPageShell";
import { ListPagination } from "@/components/catalog/ListPagination";
import { AdminField, AdminTextInput } from "@/features/admin/components/AdminModal";
import { Button } from "@/components/ui/button";
import { Download } from "lucide-react";
import { reportingService } from "@/services/reportingService";
import type { AuditLogEntry } from "@/types/reporting";

// Parse a JSON `details` payload into a compact, human-readable summary (ported from the former
// account-only audit page). Falls back to the raw string when it isn't JSON.
function formatDetails(value?: string | null): string {
  if (!value) return "—";
  try {
    const parsed = JSON.parse(value);
    if (typeof parsed === "object" && parsed !== null) {
      const parts: string[] = [];
      if (parsed.Email) parts.push(`Email: ${parsed.Email}`);
      if (parsed.IP) parts.push(`IP: ${parsed.IP}`);
      if (parsed.Device) {
        const ua: string = parsed.Device;
        let simpleUa = ua;
        if (ua.includes("Chrome")) simpleUa = "Chrome Browser";
        else if (ua.includes("Firefox")) simpleUa = "Firefox Browser";
        else if (ua.includes("Safari")) simpleUa = "Safari Browser";
        parts.push(`Device: ${simpleUa}`);
      }
      if (parsed.Reason) parts.push(`Lý do: ${parsed.Reason}`);
      if (parsed.ExpiresAt)
        parts.push(`Hết hạn: ${new Date(parsed.ExpiresAt).toLocaleTimeString("vi-VN")}`);
      return parts.length > 0 ? parts.join(" | ") : value;
    }
  } catch {
    /* not JSON — return raw */
  }
  return value;
}

const sourceLabels: Record<string, string> = {
  Iam: "Tài khoản",
  Grading: "Chấm bài",
  Submission: "Nộp bài",
};

const sourceColors: Record<string, string> = {
  Iam: "bg-brand-orange/10 text-brand-orange border-brand-orange/25",
  Grading: "bg-brand-red/10 text-brand-red border-brand-red/25",
  Submission: "bg-done/10 text-done border-done/25",
};

const PAGE_SIZE = 20;

export function AdminGlobalAuditLogPage() {
  const [userId, setUserId] = useState("");
  const [entityType, setEntityType] = useState("");
  const [action, setAction] = useState("");

  const [items, setItems] = useState<AuditLogEntry[]>([]);
  const [unavailableSources, setUnavailableSources] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await reportingService.getGlobalAuditLogs({
        userId: userId.trim() || undefined,
        entityType: entityType.trim() || undefined,
        action: action.trim() || undefined,
        page,
        pageSize: PAGE_SIZE,
      });
      setItems(result.items);
      setTotalCount(result.totalCount);
      setUnavailableSources(result.unavailableSources);
    } catch (err) {
      setError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Không tải được nhật ký hoạt động."
          : "Không tải được nhật ký hoạt động."
      );
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [userId, entityType, action, page]);

  useEffect(() => {
    setPage(1);
  }, [userId, entityType, action]);

  useEffect(() => {
    load();
  }, [load]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const exportToCSV = () => {
    if (items.length === 0) return;
    const headers = [
      "Thời gian",
      "Nguồn",
      "Hành động",
      "Loại đối tượng",
      "Entity ID",
      "User ID",
      "Chi tiết",
    ];
    const rows = items.map((entry) => [
      new Date(entry.performedAt).toLocaleString("vi-VN"),
      sourceLabels[entry.source] ?? entry.source,
      entry.action,
      entry.entityType,
      entry.entityId ?? "",
      entry.userId ?? "",
      (entry.details ?? "").replace(/"/g, '""'),
    ]);
    const csvContent =
      "\uFEFF" +
      [headers.join(","), ...rows.map((row) => row.map((v) => `"${v}"`).join(","))].join("\n");
    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.setAttribute("href", url);
    link.setAttribute("download", `Lich_su_hoat_dong_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  return (
    <LecturerPageShell maxWidth="6xl">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <PageHeader
          title="Lịch sử hoạt động"
          subtitle="Toàn bộ hoạt động trên hệ thống từ mọi tài khoản (tài khoản, chấm bài, nộp bài) — lọc theo người dùng, đối tượng, hành động."
        />
        <Button
          variant="outline"
          size="sm"
          onClick={exportToCSV}
          disabled={loading || items.length === 0}
          className="gap-1.5 border-done/30 text-done hover:bg-done/5"
        >
          <Download className="h-4 w-4" />
          Xuất CSV
        </Button>
      </div>

      <div className="grid gap-3 sm:grid-cols-3 mb-6">
        <AdminField label="ID người dùng">
          <AdminTextInput
            value={userId}
            onChange={(e) => setUserId(e.target.value)}
            placeholder="GUID"
          />
        </AdminField>
        <AdminField label="Loại đối tượng">
          <AdminTextInput
            value={entityType}
            onChange={(e) => setEntityType(e.target.value)}
            placeholder="vd: User, GradingForm, Batch"
          />
        </AdminField>
        <AdminField label="Hành động">
          <AdminTextInput
            value={action}
            onChange={(e) => setAction(e.target.value)}
            placeholder="vd: Create, ScoreOverridden, DeleteBatch"
          />
        </AdminField>
      </div>

      {unavailableSources.length > 0 && (
        <div className="mb-4 p-3 bg-brand-orange/10 border border-brand-orange/30 rounded-lg text-sm text-brand-orange">
          Không lấy được dữ liệu từ:{" "}
          {unavailableSources.map((s) => sourceLabels[s] ?? s).join(", ")} — kết quả bên dưới có
          thể thiếu.
        </div>
      )}

      {loading ? (
        <CatalogListSkeleton />
      ) : error ? (
        <CatalogErrorState message={error} onRetry={load} />
      ) : items.length === 0 ? (
        <CatalogEmptyState message="Không có bản ghi nhật ký nào khớp bộ lọc." />
      ) : (
        <div className="overflow-x-auto rounded-xl border border-line">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-ink-soft border-b border-line bg-secondary/40">
                <th className="px-4 py-3 font-medium">Thời gian</th>
                <th className="px-4 py-3 font-medium">Nguồn</th>
                <th className="px-4 py-3 font-medium">Hành động</th>
                <th className="px-4 py-3 font-medium">Đối tượng</th>
                <th className="px-4 py-3 font-medium">Người dùng</th>
                <th className="px-4 py-3 font-medium">Chi tiết</th>
              </tr>
            </thead>
            <tbody>
              {items.map((entry, index) => (
                <tr
                  key={`${entry.source}-${index}`}
                  className="border-b border-line/50 last:border-0 hover:bg-secondary/20"
                >
                  <td className="px-4 py-3 font-mono text-xs text-ink-soft whitespace-nowrap">
                    {new Date(entry.performedAt).toLocaleString("vi-VN")}
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className={`px-2 py-0.5 rounded-full text-xs font-medium border ${
                        sourceColors[entry.source] ?? "bg-secondary text-ink-soft border-line"
                      }`}
                    >
                      {sourceLabels[entry.source] ?? entry.source}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-ink font-medium">{entry.action}</td>
                  <td className="px-4 py-3 text-ink-soft">
                    {entry.entityType}
                    {entry.entityId ? (
                      <span className="text-xs font-mono ml-1">
                        ({entry.entityId.slice(0, 8)}…)
                      </span>
                    ) : null}
                  </td>
                  <td className="px-4 py-3 font-mono text-xs text-ink-soft">
                    {entry.userId ? `${entry.userId.slice(0, 8)}…` : "—"}
                  </td>
                  <td className="px-4 py-3 text-ink-soft max-w-xs truncate" title={entry.details ?? ""}>
                    {formatDetails(entry.details)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
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
