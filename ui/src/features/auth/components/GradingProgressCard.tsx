import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ArrowRight, CalendarClock } from "lucide-react";
import { gradingService } from "@/services/gradingService";
import { PaperCard } from "@/components/ui/paper-card";
import { cn } from "@/lib/utils";
import type { MyProgress, UpcomingDeadline } from "@/types/grading";

function daysUntil(dateStr: string): number {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const target = new Date(dateStr);
  target.setHours(0, 0, 0, 0);
  return Math.round((target.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
}

function urgencyClass(days: number): string {
  if (days < 0) return "bg-destructive/10 border-destructive/30 text-destructive";
  if (days <= 2) return "bg-destructive/10 border-destructive/30 text-destructive";
  if (days <= 7) return "bg-brand-orange/10 border-brand-orange/30 text-brand-orange";
  return "bg-card border-line text-ink-soft";
}

function deadlineLabel(days: number): string {
  if (days < 0) return `Quá hạn ${Math.abs(days)} ngày`;
  if (days === 0) return "Hạn hôm nay";
  if (days === 1) return "Còn 1 ngày";
  return `Còn ${days} ngày`;
}

function DeadlineRow({
  deadline,
  onJump,
}: {
  deadline: UpcomingDeadline;
  onJump: (assignmentId: string) => void;
}) {
  const days = daysUntil(deadline.examEndDate);
  return (
    <div
      className={cn(
        "flex items-center justify-between gap-3 p-3 rounded-lg border",
        urgencyClass(days)
      )}
    >
      <div className="min-w-0">
        <p className="text-sm font-semibold truncate">
          {deadline.subjectCode}
          <span className="font-normal text-ink-soft"> — {deadline.examName}</span>
        </p>
        <p className="text-xs mt-0.5 flex items-center gap-1.5">
          <CalendarClock className="h-3.5 w-3.5 shrink-0" />
          {deadlineLabel(days)} · còn {deadline.remainingCount}/{deadline.totalCount} bài
        </p>
      </div>
      {deadline.nextAssignmentId && (
        <button
          type="button"
          onClick={() => onJump(deadline.nextAssignmentId!)}
          className="shrink-0 px-3 py-1.5 rounded-lg text-xs font-medium border border-current hover:opacity-80 transition-opacity flex items-center gap-1"
        >
          Chấm ngay <ArrowRight className="h-3 w-3" />
        </button>
      )}
    </div>
  );
}

export function GradingProgressCard() {
  const navigate = useNavigate();
  const [progress, setProgress] = useState<MyProgress | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    gradingService
      .getMyProgress()
      .then(setProgress)
      .catch(() => setProgress(null))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <PaperCard className="p-5 mb-6">
        <div className="h-24 flex items-center justify-center">
          <div className="animate-spin w-6 h-6 border-2 border-line border-t-brand-red rounded-full" />
        </div>
      </PaperCard>
    );
  }

  if (!progress || progress.totalAssignments === 0) {
    return null;
  }

  const percent = Math.round((progress.submittedCount / progress.totalAssignments) * 100);

  return (
    <PaperCard className="p-5 mb-6">
      <div className="flex flex-wrap items-center justify-between gap-3 mb-4">
        <div>
          <h2 className="font-display text-lg font-semibold text-ink">
            Tiến độ chấm bài của bạn
          </h2>
          <p className="text-sm text-ink-soft mt-0.5">
            <span className="font-score font-semibold text-brand-red">
              {progress.submittedCount}
            </span>{" "}
            / <span className="font-score">{progress.totalAssignments}</span> đã chấm
            {progress.remainingCount > 0 && (
              <> · còn {progress.remainingCount} bài</>
            )}
          </p>
        </div>
        {progress.nextAssignmentId ? (
          <button
            type="button"
            onClick={() => navigate(`/grading/${progress.nextAssignmentId}`)}
            className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm font-medium hover:opacity-90 flex items-center gap-1.5 shrink-0"
          >
            Tiếp tục chấm bài <ArrowRight className="h-4 w-4" />
          </button>
        ) : (
          <span className="text-sm text-done font-medium">Đã chấm xong tất cả!</span>
        )}
      </div>

      <div className="h-2 rounded-full bg-secondary overflow-hidden mb-4">
        <div
          className="h-full bg-brand-red transition-all"
          style={{ width: `${percent}%` }}
        />
      </div>

      {progress.upcomingDeadlines.length > 0 && (
        <div className="space-y-2">
          <p className="text-xs text-ink-soft uppercase tracking-wide">
            Deadline sắp tới
          </p>
          {progress.upcomingDeadlines.map((d) => (
            <DeadlineRow
              key={d.subjectId}
              deadline={d}
              onJump={(assignmentId) => navigate(`/grading/${assignmentId}`)}
            />
          ))}
        </div>
      )}
    </PaperCard>
  );
}
