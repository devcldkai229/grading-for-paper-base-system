import { useState, useEffect } from "react";
import { userService } from "@/services/userService";
import type { AuditLog, PagedResult } from "@/types/user";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { 
  Loader2, 
  Search, 
  Download, 
  Calendar, 
  Filter, 
  RefreshCw, 
  ChevronLeft, 
  ChevronRight, 
  Activity 
} from "lucide-react";

export function AdminAuditLogsPage() {
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<PagedResult<AuditLog> | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(15);

  // Filters
  const [userId, setUserId] = useState("");
  const [action, setAction] = useState("");
  const [entityType, setEntityType] = useState("");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");

  const fetchLogs = async (currentPage = page) => {
    setLoading(true);
    try {
      const data = await userService.getAuditLogs(currentPage, pageSize, {
        userId: userId.trim() || undefined,
        action: action.trim() || undefined,
        entityType: entityType.trim() || undefined,
        startDate: startDate ? new Date(startDate).toISOString() : undefined,
        endDate: endDate ? new Date(endDate + "T23:59:59Z").toISOString() : undefined,
      });
      setResult(data);
      setPage(currentPage);
    } catch (err) {
      console.error("Lỗi khi tải nhật ký hoạt động:", err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchLogs(1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    fetchLogs(1);
  };

  const handleReset = () => {
    setUserId("");
    setAction("");
    setEntityType("");
    setStartDate("");
    setEndDate("");
    // We cannot read state immediately because setState is async, so we pass empty object
    setLoading(true);
    userService.getAuditLogs(1, pageSize, {})
      .then(data => {
        setResult(data);
        setPage(1);
      })
      .catch(err => console.error(err))
      .finally(() => setLoading(false));
  };

  const exportToCSV = () => {
    if (!result || result.items.length === 0) return;

    // Build CSV Content
    const headers = ["ID", "User ID", "Hành động", "Loại thực thể", "Entity ID", "Dữ liệu mới (Thông tin đăng nhập/Thay đổi)", "Thời gian"];
    const rows = result.items.map(item => [
      item.id,
      item.userId || "System/Anonymous",
      item.action,
      item.entityType,
      item.entityId,
      item.newValue ? item.newValue.replace(/"/g, '""') : "",
      new Date(item.createdAt).toLocaleString("vi-VN")
    ]);

    const csvContent = "\uFEFF" + [
      headers.join(","),
      ...rows.map(row => row.map(val => `"${val}"`).join(","))
    ].join("\n");

    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.setAttribute("href", url);
    link.setAttribute("download", `Audit_Logs_${new Date().toISOString().slice(0,10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  const getActionBadgeColor = (actionName: string) => {
    switch (actionName) {
      case "LoginSuccess":
        return "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20";
      case "LoginFailed":
        return "bg-rose-500/10 text-rose-600 dark:text-rose-400 border-rose-500/20";
      case "ResetPassword":
      case "ChangePassword":
        return "bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/20";
      case "UpdateProfile":
        return "bg-sky-500/10 text-sky-600 dark:text-sky-400 border-sky-500/20";
      case "ForgotPasswordRequested":
        return "bg-purple-500/10 text-purple-600 dark:text-purple-400 border-purple-500/20";
      default:
        return "bg-zinc-500/10 text-zinc-600 border-zinc-500/20";
    }
  };

  // Helper to parse detail JSON
  const formatNewValue = (value?: string) => {
    if (!value) return "-";
    try {
      const parsed = JSON.parse(value);
      if (typeof parsed === "object" && parsed !== null) {
        // Mapped key translations
        const parts: string[] = [];
        if (parsed.Email) parts.push(`Email: ${parsed.Email}`);
        if (parsed.IP) parts.push(`IP: ${parsed.IP}`);
        if (parsed.Device) {
          // Simplify user agent
          const ua = parsed.Device;
          let simpleUa = ua;
          if (ua.includes("Chrome")) simpleUa = "Chrome Browser";
          else if (ua.includes("Firefox")) simpleUa = "Firefox Browser";
          else if (ua.includes("Safari")) simpleUa = "Safari Browser";
          parts.push(`Device: ${simpleUa}`);
        }
        if (parsed.Reason) parts.push(`Lý do: ${parsed.Reason}`);
        if (parsed.ExpiresAt) parts.push(`Hết hạn: ${new Date(parsed.ExpiresAt).toLocaleTimeString()}`);
        
        return parts.length > 0 ? parts.join(" | ") : value;
      }
    } catch {
      // Return raw if not JSON
    }
    return value;
  };

  return (
    <div className="space-y-6 p-1">
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div>
          <h1 className="font-display text-2xl font-bold tracking-tight text-ink flex items-center gap-2">
            <Activity className="h-6 w-6 text-brand-red" />
            Nhật ký hoạt động hệ thống
          </h1>
          <p className="text-sm text-ink-soft mt-1">
            Theo dõi lịch sử đăng nhập và thay đổi dữ liệu của người dùng trong hệ thống (chỉ xem).
          </p>
        </div>
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => fetchLogs()}
            disabled={loading}
            className="h-9 gap-1.5"
          >
            <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
            Làm mới
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={exportToCSV}
            disabled={loading || !result || result.items.length === 0}
            className="h-9 gap-1.5 border-emerald-500/20 text-emerald-600 hover:text-emerald-700 hover:bg-emerald-50"
          >
            <Download className="h-4 w-4" />
            Xuất CSV
          </Button>
        </div>
      </div>

      {/* Filters Form */}
      <Card className="shadow-sm border-line">
        <CardHeader className="py-4">
          <CardTitle className="text-sm font-medium flex items-center gap-2">
            <Filter className="h-4 w-4 text-ink-soft" />
            Bộ lọc nhật ký
          </CardTitle>
        </CardHeader>
        <CardContent className="pb-4">
          <form onSubmit={handleSearch} className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
            <div className="space-y-1.5">
              <Label htmlFor="action" className="text-xs">Hành động</Label>
              <select
                id="action"
                value={action}
                onChange={(e) => setAction(e.target.value)}
                className="w-full h-9 rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="">Tất cả</option>
                <option value="LoginSuccess">Đăng nhập thành công</option>
                <option value="LoginFailed">Đăng nhập thất bại</option>
                <option value="UpdateProfile">Cập nhật thông tin</option>
                <option value="ChangePassword">Đổi mật khẩu</option>
                <option value="ResetPassword">Khôi phục mật khẩu</option>
                <option value="ForgotPasswordRequested">Yêu cầu quên mật khẩu</option>
                <option value="Create">Tạo tài khoản</option>
                <option value="Update">Chỉnh sửa tài khoản</option>
                <option value="SoftDelete">Xóa mềm tài khoản</option>
              </select>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="entityType" className="text-xs">Loại đối tượng</Label>
              <select
                id="entityType"
                value={entityType}
                onChange={(e) => setEntityType(e.target.value)}
                className="w-full h-9 rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="">Tất cả</option>
                <option value="Auth">Xác thực (Auth)</option>
                <option value="User">Tài khoản (User)</option>
              </select>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="userId" className="text-xs">ID Người dùng</Label>
              <Input
                id="userId"
                placeholder="Nhập GUID..."
                value={userId}
                onChange={(e) => setUserId(e.target.value)}
                className="h-9"
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="startDate" className="text-xs">Từ ngày</Label>
              <div className="relative">
                <Input
                  id="startDate"
                  type="date"
                  value={startDate}
                  onChange={(e) => setStartDate(e.target.value)}
                  className="h-9 pr-8"
                />
                <Calendar className="absolute right-2.5 top-2.5 h-4 w-4 text-ink-soft pointer-events-none" />
              </div>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="endDate" className="text-xs">Đến ngày</Label>
              <div className="relative">
                <Input
                  id="endDate"
                  type="date"
                  value={endDate}
                  onChange={(e) => setEndDate(e.target.value)}
                  className="h-9 pr-8"
                />
                <Calendar className="absolute right-2.5 top-2.5 h-4 w-4 text-ink-soft pointer-events-none" />
              </div>
            </div>

            <div className="lg:col-span-5 flex justify-end gap-2 border-t border-line pt-4">
              <Button type="button" variant="outline" size="sm" onClick={handleReset}>
                Thiết lập lại
              </Button>
              <Button type="submit" size="sm" className="bg-brand-red text-white hover:bg-brand-red/90 gap-1.5">
                <Search className="h-4 w-4" />
                Lọc kết quả
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      {/* Logs Table */}
      <Card className="shadow-sm border-line overflow-hidden">
        <CardContent className="p-0">
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left text-sm">
              <thead className="bg-card border-b border-line text-ink-soft font-medium">
                <tr>
                  <th className="px-5 py-3.5">Thời gian</th>
                  <th className="px-5 py-3.5">Hành động</th>
                  <th className="px-5 py-3.5">Đối tượng</th>
                  <th className="px-5 py-3.5">Thông tin chi tiết</th>
                  <th className="px-5 py-3.5">ID Tài khoản liên quan</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line bg-background">
                {loading ? (
                  <tr>
                    <td colSpan={5} className="px-5 py-12 text-center">
                      <div className="flex flex-col items-center justify-center gap-2">
                        <Loader2 className="h-6 w-6 animate-spin text-brand-red" />
                        <span className="text-xs text-ink-soft font-medium">Đang tải nhật ký...</span>
                      </div>
                    </td>
                  </tr>
                ) : !result || result.items.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="px-5 py-12 text-center text-ink-soft">
                      Không tìm thấy bản ghi nhật ký nào phù hợp.
                    </td>
                  </tr>
                ) : (
                  result.items.map((log) => (
                    <tr key={log.id} className="hover:bg-card/50 transition-colors">
                      <td className="px-5 py-4 text-ink font-mono text-xs whitespace-nowrap">
                        {new Date(log.createdAt).toLocaleString("vi-VN")}
                      </td>
                      <td className="px-5 py-4">
                        <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-semibold ${getActionBadgeColor(log.action)}`}>
                          {log.action}
                        </span>
                      </td>
                      <td className="px-5 py-4 font-medium text-ink-soft text-xs whitespace-nowrap">
                        {log.entityType}
                      </td>
                      <td className="px-5 py-4 text-ink text-xs max-w-xs truncate" title={log.newValue || ""}>
                        {formatNewValue(log.newValue)}
                      </td>
                      <td className="px-5 py-4 font-mono text-[11px] text-ink-soft whitespace-nowrap">
                        {log.entityId === "00000000-0000-0000-0000-000000000000" ? "-" : log.entityId}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {result && result.totalPages > 1 && (
            <div className="flex items-center justify-between border-t border-line px-5 py-4 bg-card/25">
              <div className="text-xs text-ink-soft font-medium">
                Hiển thị trang <span className="text-ink">{result.page}</span> / <span className="text-ink">{result.totalPages}</span> (Tổng <span className="text-ink">{result.totalCount}</span> dòng)
              </div>
              <div className="flex items-center gap-1.5">
                <Button
                  variant="outline"
                  size="icon"
                  className="h-8 w-8"
                  onClick={() => fetchLogs(page - 1)}
                  disabled={page <= 1 || loading}
                >
                  <ChevronLeft className="h-4 w-4" />
                </Button>
                {Array.from({ length: result.totalPages }, (_, i) => i + 1)
                  .filter(p => Math.abs(p - page) <= 2 || p === 1 || p === result.totalPages)
                  .map((p, idx, arr) => {
                    const showEllipsis = idx > 0 && p - arr[idx - 1] > 1;
                    return (
                      <div key={p} className="flex items-center gap-1.5">
                        {showEllipsis && <span className="text-ink-soft px-1">...</span>}
                        <Button
                          variant={p === page ? "default" : "outline"}
                          size="sm"
                          className={`h-8 w-8 text-xs font-semibold ${p === page ? 'bg-brand-red text-white' : ''}`}
                          onClick={() => fetchLogs(p)}
                          disabled={loading}
                        >
                          {p}
                        </Button>
                      </div>
                    );
                  })}
                <Button
                  variant="outline"
                  size="icon"
                  className="h-8 w-8"
                  onClick={() => fetchLogs(page + 1)}
                  disabled={page >= result.totalPages || loading}
                >
                  <ChevronRight className="h-4 w-4" />
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
