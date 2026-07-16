import { useEffect, useState } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { isAxiosError } from "axios";
import { submissionService } from "@/services/submissionService";
import { gradingService } from "@/services/gradingService";
import { startGradingFlow } from "@/lib/startGradingFlow";
import { loadGradingQueue } from "@/lib/gradingQueue";
import type { StudentPaper } from "@/types/submission";
import { PageHeader } from "@/components/layout/PageHeader";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import { ListPagination } from "@/components/catalog/ListPagination";
import { FilterBar, ContentBlockButton } from "@/components/ui/content-block";

const statusColors: Record<string, string> = {
  ReadyToAssign: "bg-brand-orange/10 text-brand-orange border-brand-orange/25",
  Assigned: "bg-secondary text-ink border-line",
  Completed: "bg-done/10 text-done border-done/25",
  ReGrading: "bg-brand-red/10 text-brand-red border-brand-red/25",
};

const statusLabels: Record<string, string> = {
  ReadyToAssign: "Chờ phân công",
  Assigned: "Đã phân công",
  Completed: "Hoàn thành",
  ReGrading: "Chấm lại",
};

const PAGE_SIZE = 20;

export function SubmissionsPage() {
  const [searchParams] = useSearchParams();
  const subjectId = searchParams.get("subjectId") || "";
  const [papers, setPapers] = useState<StudentPaper[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [statusFilter, setStatusFilter] = useState<string | undefined>(undefined);
  const [startingGrading, setStartingGrading] = useState(false);
  const [gradingError, setGradingError] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const navigate = useNavigate();

  const reloadPapers = () => {
    if (!subjectId) return;
    submissionService
      .getSubmissions(subjectId, statusFilter, page)
      .then((result) => {
        setPapers(result.items);
        setTotalPages(result.totalPages);
        setTotalCount(result.totalCount ?? result.items.length);
      })
      .catch(console.error);
  };

  const handleDeletePaper = async (paper: StudentPaper) => {
    const label = paper.studentAlias || `Paper #${paper.aliasNumber}`;
    if (!window.confirm(`Xóa bài làm "${label}"? Hành động này không thể hoàn tác.`)) {
      return;
    }
    setDeleteError(null);
    setDeletingId(paper.id);
    try {
      await submissionService.deleteSubmission(paper.id);
      reloadPapers();
    } catch (err) {
      setDeleteError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Không thể xóa bài làm."
          : "Không thể xóa bài làm."
      );
    } finally {
      setDeletingId(null);
    }
  };

  const handleExport = async () => {
    setExporting(true);
    try {
      const blob = await gradingService.exportGrades(subjectId);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.setAttribute("download", `Subject_Grades_${subjectId}.xlsx`);
      document.body.appendChild(link);
      link.click();
      link.parentNode?.removeChild(link);
    } catch {
      alert("Xuất điểm Excel thất bại.");
    } finally {
      setExporting(false);
    }
  };

  useEffect(() => {
    if (!subjectId) {
      setLoading(false);
      return;
    }
    setLoading(true);
    submissionService
      .getSubmissions(subjectId, statusFilter, page)
      .then((result) => {
        setPapers(result.items);
        setTotalPages(result.totalPages);
        setTotalCount(result.totalCount ?? result.items.length);
      })
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [subjectId, statusFilter, page]);

  const batchIdForGrading = papers[0]?.batchId;
  const savedQueue = loadGradingQueue();
  const canResumeGrading =
    Boolean(batchIdForGrading) &&
    savedQueue?.batchId === batchIdForGrading &&
    savedQueue.assignments.some(
      (a) => a.status === "Drafting" || a.status === "NotStarted"
    );

  const handleStartGrading = async () => {
    if (!batchIdForGrading) return;
    setStartingGrading(true);
    setGradingError(null);
    try {
      await startGradingFlow(batchIdForGrading, navigate);
    } catch (err) {
      setGradingError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Không thể bắt đầu chấm bài. Kiểm tra API Grading."
          : "Không thể bắt đầu chấm bài. Kiểm tra API Grading."
      );
    } finally {
      setStartingGrading(false);
    }
  };

  const filterBtn = (active: boolean) =>
    active
      ? "bg-primary text-primary-foreground border-primary"
      : "bg-card text-ink-soft hover:bg-secondary border-line";

  return (
    <LecturerPageShell>
      <PageHeader
        title="Bài đã nộp"
        subtitle={
          subjectId
            ? `Môn thi: ${subjectId}`
            : "Chọn môn từ Danh mục thi hoặc thêm ?subjectId= vào URL"
        }
      />

      {papers.length > 0 && batchIdForGrading && (
        <div className="mb-6 flex flex-wrap items-center gap-3">
          <button
            onClick={() => void handleStartGrading()}
            disabled={startingGrading}
            className="px-5 py-2.5 bg-primary text-primary-foreground hover:opacity-90 disabled:opacity-50 rounded-lg text-sm font-medium"
          >
            {startingGrading
              ? "Đang khởi tạo..."
              : canResumeGrading
                ? "Tiếp tục chấm bài"
                : "Tiến hành chấm bài"}
          </button>
          <button
            onClick={() => void handleExport()}
            disabled={exporting}
            className="px-5 py-2.5 bg-secondary text-ink border border-line hover:bg-paper/80 disabled:opacity-50 rounded-lg text-sm font-medium"
          >
            {exporting ? "Đang xuất..." : "Xuất điểm Excel"}
          </button>
          {gradingError && (
            <span className="text-sm text-destructive">{gradingError}</span>
          )}
        </div>
      )}

      <FilterBar>
        <button
          onClick={() => {
            setStatusFilter(undefined);
            setPage(1);
          }}
          className={`px-4 py-2 rounded-lg text-sm font-medium transition-all border ${filterBtn(!statusFilter)}`}
        >
          Tất cả
        </button>
        {Object.entries(statusLabels).map(([key, label]) => (
          <button
            key={key}
            onClick={() => {
              setStatusFilter(key);
              setPage(1);
            }}
            className={`px-4 py-2 rounded-lg text-sm font-medium transition-all border ${filterBtn(statusFilter === key)}`}
          >
            {label}
          </button>
        ))}
      </FilterBar>

      {deleteError && (
        <div className="mb-4 p-3 bg-destructive/10 border border-destructive/30 rounded-lg text-destructive text-sm">
          {deleteError}
        </div>
      )}

      {loading ? (
        <div className="flex justify-center py-20">
          <div className="animate-spin w-8 h-8 border-2 border-line border-t-brand-red rounded-full" />
        </div>
      ) : !subjectId ? (
        <div className="text-center py-20 text-ink-soft">
          Chưa có môn thi. Vào Danh mục thi để chọn môn.
        </div>
      ) : papers.length === 0 ? (
        <div className="text-center py-20 text-ink-soft">
          Chưa có bài nào. Tải bài lên để bắt đầu chấm.
        </div>
      ) : (
        <div className="grid gap-3">
          {papers.map((p) => {
            const sc = statusColors[p.status] || "bg-secondary text-ink-soft border-line";
            const canDelete = p.status === "ReadyToAssign";
            return (
              <div key={p.id} className="relative group">
                <ContentBlockButton
                  onClick={() => navigate(`/submissions/${p.id}`)}
                  className={`flex items-center justify-between ${canDelete ? "pr-16" : ""}`}
                >
                  <div className="flex items-center gap-4">
                    <div className="w-10 h-10 rounded-lg bg-secondary flex items-center justify-center text-sm font-score font-bold text-brand-red">
                      {p.aliasNumber || "?"}
                    </div>
                    <div>
                      <h3 className="font-medium group-hover:text-brand-red transition-colors text-ink">
                        {p.studentAlias || `Paper #${p.aliasNumber}`}
                      </h3>
                      <span className="text-xs text-ink-soft">{p.fileCount} files</span>
                    </div>
                  </div>
                  <span className={`px-2.5 py-1 rounded-full text-xs font-medium border ${sc}`}>
                    {statusLabels[p.status] || p.status}
                  </span>
                </ContentBlockButton>
                {canDelete && (
                  <button
                    type="button"
                    title="Xóa bài làm"
                    onClick={(e) => {
                      e.stopPropagation();
                      void handleDeletePaper(p);
                    }}
                    disabled={deletingId === p.id}
                    className="absolute right-4 top-1/2 -translate-y-1/2 px-2.5 py-1 rounded-lg text-xs font-medium text-destructive hover:bg-destructive/10 disabled:opacity-50 transition-colors"
                  >
                    {deletingId === p.id ? "Đang xóa..." : "Xóa"}
                  </button>
                )}
              </div>
            );
          })}
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
