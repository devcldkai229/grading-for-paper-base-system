import type { GradingQuestionMark } from "@/types/grading";
import { cn } from "@/lib/utils";

interface QuestionJumpListProps {
  questions: GradingQuestionMark[];
  activeQuestionNumber: string | null;
  isAnswered: (questionNumber: string) => boolean;
  onJump: (questionNumber: string) => void;
}

/**
 * Sticky chip strip for jumping directly to a question's row in the score panel.
 * Alt+↑ / Alt+↓ move through the same order (see GradingPage's keydown handler).
 */
export function QuestionJumpList({
  questions,
  activeQuestionNumber,
  isAnswered,
  onJump,
}: QuestionJumpListProps) {
  if (questions.length === 0) return null;

  return (
    <div
      className="flex flex-wrap gap-1.5 mb-3 pb-3 border-b border-line"
      role="tablist"
      aria-label="Nhảy tới câu hỏi"
    >
      {questions.map((q) => {
        const active = q.questionNumber === activeQuestionNumber;
        const answered = isAnswered(q.questionNumber);
        return (
          <button
            key={q.questionNumber}
            type="button"
            role="tab"
            aria-selected={active}
            title={q.label?.trim() || q.questionNumber}
            onClick={() => onJump(q.questionNumber)}
            className={cn(
              "px-2.5 py-1 rounded-lg text-xs font-score border transition-colors",
              active
                ? "bg-brand-red/10 border-brand-red/50 text-brand-red font-semibold"
                : answered
                  ? "bg-done/10 border-done/30 text-done hover:border-brand-red/30"
                  : "bg-card border-line text-ink-soft hover:border-brand-red/30"
            )}
          >
            {q.label?.trim() || q.questionNumber}
          </button>
        );
      })}
    </div>
  );
}
