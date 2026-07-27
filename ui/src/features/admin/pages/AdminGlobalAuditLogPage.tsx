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

const actionLabels: Record<string, string> = {
  LoginSuccess: "Đăng nhập thành công",
  LoginFailed: "Đăng nhập thất bại",
  Logout: "Đăng xuất",
  Create: "Tạo mới",
  Update: "Cập nhật",
  Delete: "Xóa",
  DeleteBatch: "Xóa batch",
  DeletePaper: "Xóa bài",
  ScoreUpdated: "Cập nhật điểm",
  ScoreOverridden: "Ghi đè điểm",
  FormSubmitted: "Nộp phiếu chấm",
  AiSuggested: "Gợi ý AI",
  ForgotPassword: "Quên mật khẩu",
  ResetPassword: "Đặt lại mật khẩu",
  UpdateProfile: "Cập nhật hồ sơ",
};

const fieldLabels: Record<string, string> = {
  Email: "Email",
  IP: "IP",
  Device: "Thiết bị",
  Reason: "Lý do",
  ExpiresAt: "Hết hạn",
  FullName: "Họ tên",
  PhoneNumber: "SĐT",
  MarkerCode: "Mã GV",
  Role: "Vai trò",
  Status: "Trạng thái",
  AvatarUrl: "Avatar",
  TotalScore: "Tổng điểm",
  PaperComment: "Nhận xét bài",
  InternalComment: "Ghi chú nội bộ",
  RowVersion: "Phiên bản",
  Questions: "Câu hỏi",
  QuestionNumber: "Câu",
  Score: "Điểm",
  QuestionComment: "Nhận xét câu",
  ModelUsed: "Model",
  PromptVersion: "Prompt",
  SuggestionCount: "Số gợi ý",
};

function simplifyDevice(ua: string): string {
  if (ua.includes("Chrome")) return "Chrome";
  if (ua.includes("Firefox")) return "Firefox";
  if (ua.includes("Safari")) return "Safari";
  if (ua.includes("Edge")) return "Edge";
  return ua.length > 40 ? `${ua.slice(0, 37)}…` : ua;
}

function formatJsonObject(obj: Record<string, unknown>): string {
  const parts: string[] = [];

  if (typeof obj.Email === "string") parts.push(`Email: ${obj.Email}`);
  if (typeof obj.IP === "string") parts.push(`IP: ${obj.IP}`);
  if (typeof obj.Device === "string") parts.push(`Thiết bị: ${simplifyDevice(obj.Device)}`);
  if (typeof obj.Reason === "string" && obj.Reason) parts.push(`Lý do: ${obj.Reason}`);
  if (typeof obj.ExpiresAt === "string") {
    parts.push(`Hết hạn: ${new Date(obj.ExpiresAt).toLocaleString("vi-VN")}`);
  }
  if (typeof obj.FullName === "string") parts.push(`Họ tên: ${obj.FullName}`);
  if (typeof obj.PhoneNumber === "string") parts.push(`SĐT: ${obj.PhoneNumber}`);
  if (typeof obj.MarkerCode === "string") parts.push(`Mã GV: ${obj.MarkerCode}`);
  if (typeof obj.Role === "string") parts.push(`Vai trò: ${obj.Role}`);
  if (typeof obj.Status === "string") parts.push(`Trạng thái: ${obj.Status}`);
  if (typeof obj.TotalScore === "number") parts.push(`Tổng điểm: ${obj.TotalScore}`);
  if (typeof obj.PaperComment === "string" && obj.PaperComment) {
    parts.push(`Nhận xét: ${obj.PaperComment}`);
  }
  if (typeof obj.InternalComment === "string" && obj.InternalComment) {
    parts.push(`Ghi chú nội bộ: ${obj.InternalComment}`);
  }
  if (typeof obj.ModelUsed === "string") parts.push(`Model: ${obj.ModelUsed}`);
  if (typeof obj.SuggestionCount === "number") {
    parts.push(`Số gợi ý: ${obj.SuggestionCount}`);
  }
  if (Array.isArray(obj.Questions)) {
    const qParts = (obj.Questions as Record<string, unknown>[])
      .map((q) => {
        const num = String(q.QuestionNumber ?? q.questionNumber ?? "?");
        const rawScore = q.Score ?? q.score;
        const score =
          typeof rawScore === "number" || typeof rawScore === "string"
            ? String(rawScore)
            : null;
        return score !== null ? `Câu ${num}: ${score}` : `Câu ${num}`;
      })
      .filter(Boolean);
    if (qParts.length) parts.push(qParts.join("; "));
  }

  if (parts.length === 0) {
    for (const [key, val] of Object.entries(obj)) {
      if (val === null || val === undefined || typeof val === "object") continue;
      const label = fieldLabels[key] ?? key;
      parts.push(`${label}: ${String(val)}`);
    }
  }

  return parts.join(" · ");
}

function tryParseJsonObject(raw: string): Record<string, unknown> | null {
  const trimmed = raw.trim();
  if (!trimmed.startsWith("{") && !trimmed.startsWith("[")) return null;
  try {
    const parsed = JSON.parse(trimmed);
    if (typeof parsed === "object" && parsed !== null && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
  } catch {
    /* ignore */
  }
  return null;
}

function formatValueSide(raw: string): string {
  const cleaned = raw.trim();
  if (!cleaned || cleaned === "—" || cleaned === "null") return "—";
  const obj = tryParseJsonObject(cleaned);
  if (obj) {
    const formatted = formatJsonObject(obj);
    return formatted || "—";
  }
  return cleaned;
}

/** Turn backend `old → new | Reason: …` (often with JSON blobs) into Vietnamese text. */
function formatDetails(value?: string | null, action?: string): string {
  if (!value) return "—";

  let reasonSuffix = "";
  let body = value;
  const reasonMatch = body.match(/\s*\|\s*Reason:\s*(.+)$/i);
  if (reasonMatch) {
    reasonSuffix = reasonMatch[1].trim();
    body = body.slice(0, reasonMatch.index).trim();
  }

  const arrowIdx = body.indexOf(" → ");
  let summary: string;
  if (arrowIdx >= 0) {
    const before = formatValueSide(body.slice(0, arrowIdx));
    const after = formatValueSide(body.slice(arrowIdx + 3));
    if (before === "—" && after !== "—") {
      summary = after;
    } else if (before !== "—" && after === "—") {
      summary = before;
    } else if (before === after) {
      summary = after;
    } else {
      summary = `${before} → ${after}`;
    }
  } else {
    summary = formatValueSide(body);
  }

  if (reasonSuffix) {
    summary = summary === "—" ? `Lý do: ${reasonSuffix}` : `${summary} · Lý do: ${reasonSuffix}`;
  }

  if (summary.startsWith("{") || summary.includes('{"')) {
    const obj = tryParseJsonObject(value);
    if (obj) summary = formatJsonObject(obj) || summary;
  }

  if (action && summary === "—" && actionLabels[action]) {
    return actionLabels[action];
  }

  return summary || "—";
}

function formatAction(action: string): string {
  return actionLabels[action] ?? action;
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
      formatAction(entry.action),
      entry.entityType,
      entry.entityId ?? "",
      entry.userId ?? "",
      formatDetails(entry.details, entry.action).replace(/"/g, '""'),
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
              {items.map((entry, index) => {
                const detailText = formatDetails(entry.details, entry.action);
                return (
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
                    <td className="px-4 py-3 text-ink font-medium">{formatAction(entry.action)}</td>
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
                    <td className="px-4 py-3 text-ink-soft max-w-md truncate" title={detailText}>
                      {detailText}
                    </td>
                  </tr>
                );
              })}
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
