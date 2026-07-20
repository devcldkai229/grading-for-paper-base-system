import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { isAxiosError } from "axios";
import { ShieldAlert, Loader2 } from "lucide-react";
import { gradingService } from "@/services/gradingService";
import type { AuditLogEntry, GradingSession } from "@/types/grading";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { StatusBadge } from "@/components/ui/status-badge";

const auditActionLabels: Record<string, string> = {
  ScoreUpdated: "Lưu điểm",
  FormSubmitted: "Nộp bài",
  ScoreOverridden: "Override điểm",
};

interface MarkDraft {
  score: string;
  questionComment: string;
}

function apiErrorMessage(err: unknown, fallback: string): string {
  if (isAxiosError(err)) {
    if (err.response?.status === 403) return "Chỉ Admin mới được dùng công cụ này.";
    if (err.response?.status === 404) return "Không tìm thấy assignment với ID này.";
    const msg = (err.response?.data as { message?: string } | undefined)?.message;
    if (msg) return msg;
  }
  return fallback;
}

export function AdminOverridePage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [assignmentIdInput, setAssignmentIdInput] = useState(
    searchParams.get("assignmentId") ?? ""
  );

  const [session, setSession] = useState<GradingSession | null>(null);
  const [marks, setMarks] = useState<Record<string, MarkDraft>>({});
  const [paperComment, setPaperComment] = useState("");
  const [internalComment, setInternalComment] = useState("");
  const [reason, setReason] = useState("");

  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [overriding, setOverriding] = useState(false);
  const [overrideError, setOverrideError] = useState<string | null>(null);
  const [overrideSuccess, setOverrideSuccess] = useState(false);

  const [showAuditLog, setShowAuditLog] = useState(false);
  const [auditLog, setAuditLog] = useState<AuditLogEntry[] | null>(null);
  const [auditLoading, setAuditLoading] = useState(false);
  const [auditError, setAuditError] = useState<string | null>(null);

  const applySession = (data: GradingSession) => {
    setSession(data);
    setPaperComment(data.paperComment ?? "");
    setInternalComment(data.internalComment ?? "");
    const draft: Record<string, MarkDraft> = {};
    for (const q of data.questions) {
      draft[q.questionNumber] = {
        score: q.score != null ? String(q.score) : "",
        questionComment: q.questionComment ?? "",
      };
    }
    setMarks(draft);
  };

  const loadAssignment = async (id: string) => {
    if (!id.trim()) return;
    setLoading(true);
    setLoadError(null);
    setSession(null);
    setOverrideSuccess(false);
    setShowAuditLog(false);
    setAuditLog(null);
    try {
      const data = await gradingService.getAssignmentForOverride(id.trim());
      applySession(data);
      setReason("");
    } catch (err) {
      setLoadError(apiErrorMessage(err, "Không tải được assignment."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const fromQuery = searchParams.get("assignmentId");
    if (fromQuery) void loadAssignment(fromQuery);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleLoadClick = (e: React.FormEvent) => {
    e.preventDefault();
    setSearchParams(assignmentIdInput.trim() ? { assignmentId: assignmentIdInput.trim() } : {});
    void loadAssignment(assignmentIdInput);
  };

  const loadAuditLog = async () => {
    if (!session) return;
    setAuditLoading(true);
    setAuditError(null);
    try {
      const entries = await gradingService.getAuditLog(session.assignmentId);
      setAuditLog(entries);
    } catch {
      setAuditError("Không tải được lịch sử chỉnh sửa");
    } finally {
      setAuditLoading(false);
    }
  };

  const toggleAuditLog = () => {
    setShowAuditLog((prev) => {
      const next = !prev;
      if (next && auditLog === null) void loadAuditLog();
      return next;
    });
  };

  const handleScoreChange = (questionNumber: string, maxScore: number, value: string) => {
    let next = value;
    if (next !== "" && !Number.isNaN(Number(next))) {
      const n = Number(next);
      if (n > maxScore) next = String(maxScore);
      if (n < 0) next = "0";
    }
    setMarks((prev) => ({
      ...prev,
      [questionNumber]: {
        score: next,
        questionComment: prev[questionNumber]?.questionComment ?? "",
      },
    }));
  };

  const handleConfirmOverride = async () => {
    if (!session || reason.trim() === "") return;
    setOverriding(true);
    setOverrideError(null);
    setOverrideSuccess(false);
    try {
      await gradingService.overrideMarks(session.assignmentId, {
        reason: reason.trim(),
        paperComment,
        internalComment,
        questions: session.questions.map((q) => {
          const raw = marks[q.questionNumber]?.score?.trim() ?? "";
          const score = raw === "" ? 0 : Math.min(Math.max(0, Number(raw)), q.maxScore);
          return {
            questionNumber: q.questionNumber,
            score: Number.isNaN(score) ? 0 : score,
            questionComment: marks[q.questionNumber]?.questionComment ?? "",
          };
        }),
      });
      setOverrideSuccess(true);
      setReason("");
      await loadAssignment(session.assignmentId);
      if (showAuditLog) await loadAuditLog();
    } catch (err) {
      setOverrideError(apiErrorMessage(err, "Override thất bại"));
    } finally {
      setOverriding(false);
    }
  };

  const totalScore = session
    ? session.questions.reduce((sum, q) => {
        const raw = marks[q.questionNumber]?.score?.trim();
        if (!raw) return sum;
        const n = Number(raw);
        if (Number.isNaN(n)) return sum;
        return sum + Math.min(Math.max(0, n), q.maxScore);
      }, 0)
    : 0;

  const inputClass =
    "w-full px-3 py-2 bg-card border border-line rounded-lg text-sm text-ink focus:border-primary focus:ring-1 focus:ring-primary/30 outline-none";

  return (
    <div className="space-y-6 p-1">
      <div>
        <h1 className="font-display text-2xl font-bold tracking-tight text-ink flex items-center gap-2">
          <ShieldAlert className="h-6 w-6 text-brand-red" />
          Override điểm (Admin)
        </h1>
        <p className="text-sm text-ink-soft mt-1">
          Sửa điểm bất kỳ bài nào (kể cả không phải do bạn chấm), yêu cầu bắt buộc lý do — mọi
          thay đổi đều được ghi audit log.
        </p>
      </div>

      <Card className="shadow-sm border-line">
        <CardHeader className="py-4">
          <CardTitle className="text-sm font-medium">Tra cứu assignment</CardTitle>
        </CardHeader>
        <CardContent className="pb-4">
          <form onSubmit={handleLoadClick} className="flex flex-wrap items-end gap-3">
            <div className="flex-1 min-w-[280px] space-y-1.5">
              <Label htmlFor="assignmentId" className="text-xs">
                Assignment ID
              </Label>
              <Input
                id="assignmentId"
                value={assignmentIdInput}
                onChange={(e) => setAssignmentIdInput(e.target.value)}
                placeholder="vd: 1581a7a1-bbd0-46cb-9b34-4930daa8c81e"
              />
            </div>
            <Button type="submit" disabled={loading || !assignmentIdInput.trim()} className="h-9">
              {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : "Tải bài"}
            </Button>
          </form>
          {loadError && <p className="text-sm text-destructive mt-3">{loadError}</p>}
        </CardContent>
      </Card>

      {session && (
        <Card className="shadow-sm border-line">
          <CardHeader className="py-4">
            <CardTitle className="text-sm font-medium flex flex-wrap items-center gap-2">
              Bài của {session.studentAlias ?? `#${session.aliasNumber}`}
              <StatusBadge variant={session.status === "Submitted" ? "done" : "default"}>
                {session.status}
              </StatusBadge>
              {session.isFlagged && (
                <StatusBadge variant="error">Đã gắn cờ</StatusBadge>
              )}
              <span className="text-xs text-ink-soft font-normal ml-auto">
                Tổng điểm hiện tại: {totalScore.toFixed(2)} / {session.subjectMaxScore.toFixed(2)}
              </span>
            </CardTitle>
          </CardHeader>
          <CardContent className="pb-4 space-y-4">
            <div className="space-y-2">
              {session.questions.map((q) => (
                <div key={q.questionNumber} className="grid grid-cols-12 gap-2 items-center">
                  <div className="col-span-3 text-sm font-medium text-ink">
                    {q.label?.trim() || q.questionNumber}
                    <div className="text-xs text-ink-soft font-score">
                      {q.label?.trim() ? q.questionNumber + " · " : ""}/ {q.maxScore}
                    </div>
                  </div>
                  <div className="col-span-3">
                    <input
                      type="number"
                      min={0}
                      max={q.maxScore}
                      step={0.25}
                      value={marks[q.questionNumber]?.score ?? ""}
                      onChange={(e) =>
                        handleScoreChange(q.questionNumber, q.maxScore, e.target.value)
                      }
                      className={`${inputClass} font-score text-brand-red`}
                      placeholder="0"
                      aria-label={`Điểm câu ${q.questionNumber}`}
                    />
                  </div>
                  <div className="col-span-6">
                    <input
                      type="text"
                      value={marks[q.questionNumber]?.questionComment ?? ""}
                      onChange={(e) =>
                        setMarks((prev) => ({
                          ...prev,
                          [q.questionNumber]: {
                            score: prev[q.questionNumber]?.score ?? "",
                            questionComment: e.target.value,
                          },
                        }))
                      }
                      className={inputClass}
                      placeholder="Nhận xét"
                    />
                  </div>
                </div>
              ))}
            </div>

            <div className="grid sm:grid-cols-2 gap-3">
              <div>
                <Label className="text-xs mb-1 block">Nhận xét bài</Label>
                <textarea
                  value={paperComment}
                  onChange={(e) => setPaperComment(e.target.value)}
                  rows={2}
                  className={`${inputClass} resize-none`}
                />
              </div>
              <div>
                <Label className="text-xs mb-1 block">Ghi chú nội bộ</Label>
                <textarea
                  value={internalComment}
                  onChange={(e) => setInternalComment(e.target.value)}
                  rows={2}
                  className={`${inputClass} resize-none`}
                />
              </div>
            </div>

            <div>
              <Label className="text-xs mb-1 block">
                Lý do override <span className="text-destructive">*</span>
              </Label>
              <textarea
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                rows={2}
                placeholder="Vd: Học sinh khiếu nại, chấm lại theo yêu cầu phòng đào tạo"
                className={`${inputClass} resize-none`}
              />
            </div>

            {overrideError && <p className="text-sm text-destructive">{overrideError}</p>}
            {overrideSuccess && (
              <p className="text-sm text-done">Override thành công.</p>
            )}

            <div className="flex flex-wrap gap-3">
              <Button
                type="button"
                onClick={() => void handleConfirmOverride()}
                disabled={overriding || reason.trim() === ""}
                className="bg-brand-red text-white hover:bg-brand-red/90"
              >
                {overriding ? "Đang override..." : "Xác nhận override"}
              </Button>
              <Button type="button" variant="outline" onClick={toggleAuditLog}>
                {showAuditLog ? "Ẩn lịch sử chỉnh sửa" : "Xem lịch sử chỉnh sửa"}
              </Button>
            </div>

            {showAuditLog && (
              <div className="p-3 border border-line rounded-lg max-h-64 overflow-y-auto">
                <p className="text-xs text-ink-soft mb-2 uppercase tracking-wide">
                  Lịch sử chỉnh sửa
                </p>
                {auditLoading ? (
                  <p className="text-sm text-ink-soft">Đang tải...</p>
                ) : auditError ? (
                  <p className="text-sm text-destructive">{auditError}</p>
                ) : auditLog && auditLog.length > 0 ? (
                  <ul className="space-y-2">
                    {auditLog.map((entry) => (
                      <li key={entry.id} className="text-sm border-b border-line pb-2 last:border-0">
                        <div className="flex items-center justify-between gap-2">
                          <span className="font-medium text-ink">
                            {auditActionLabels[entry.action] ?? entry.action}
                          </span>
                          <span className="text-xs text-ink-soft">
                            {new Date(entry.createdAt).toLocaleString("vi-VN")}
                          </span>
                        </div>
                        {entry.reason && (
                          <p className="text-xs text-ink-soft mt-1">Lý do: {entry.reason}</p>
                        )}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="text-sm text-ink-soft">Chưa có lịch sử chỉnh sửa</p>
                )}
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
