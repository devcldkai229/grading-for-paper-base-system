import type { QuestionInput } from "@/types/catalog";
import { PaperCard } from "@/components/ui/paper-card";

export interface ScoreGridRow extends QuestionInput {
  confidence?: number;
  warning?: string;
}

/// Extracts a declared budget percentage from a group label, e.g. "Request 1 (20%)" -> 20.
/// Mirrors SubjectAdminService.ValidateQuestions' GroupPercentPattern on the backend.
function parseGroupPercent(label: string): number | null {
  const match = label.match(/(\d+(?:\.\d+)?)\s*%/);
  return match ? Number(match[1]) : null;
}

interface GroupBudgetSummary {
  label: string;
  sum: number;
  expected: number;
  mismatch: boolean;
}

function computeGroupBudgets(rows: ScoreGridRow[], subjectMaxScore: number): GroupBudgetSummary[] {
  const sumsByLabel = new Map<string, number>();
  for (const row of rows) {
    const label = row.groupLabel?.trim();
    if (!label) continue;
    sumsByLabel.set(label, (sumsByLabel.get(label) ?? 0) + (Number(row.maxScore) || 0));
  }

  const summaries: GroupBudgetSummary[] = [];
  for (const [label, sum] of sumsByLabel) {
    const percent = parseGroupPercent(label);
    if (percent === null) continue;
    const expected = Math.round(((subjectMaxScore * percent) / 100) * 100) / 100;
    summaries.push({ label, sum, expected, mismatch: Math.abs(sum - expected) > 0.01 });
  }
  return summaries;
}

interface ScoreGridEditorProps {
  rows: ScoreGridRow[];
  subjectMaxScore: number;
  warnings?: string[];
  onChange: (rows: ScoreGridRow[]) => void;
  readOnly?: boolean;
}

export function ScoreGridEditor({
  rows,
  subjectMaxScore,
  warnings = [],
  onChange,
  readOnly = false,
}: ScoreGridEditorProps) {
  const total = rows.reduce((s, r) => s + (Number(r.maxScore) || 0), 0);
  const totalMismatch = Math.abs(total - subjectMaxScore) > 0.01;
  const groupBudgets = computeGroupBudgets(rows, subjectMaxScore);

  const updateRow = (index: number, patch: Partial<ScoreGridRow>) => {
    const next = rows.map((r, i) => (i === index ? { ...r, ...patch } : r));
    onChange(next);
  };

  const addRow = () => {
    onChange([
      ...rows,
      {
        groupLabel: null,
        questionNumber: String(rows.length + 1),
        label: "",
        maxScore: 0,
        orderIndex: rows.length,
      },
    ]);
  };

  const removeRow = (index: number) => {
    onChange(
      rows
        .filter((_, i) => i !== index)
        .map((r, i) => ({ ...r, orderIndex: i }))
    );
  };

  const inputClass =
    "w-full px-2 py-1.5 bg-card border border-line rounded text-sm text-ink focus:border-primary outline-none disabled:opacity-60";

  return (
    <div className="space-y-4">
      {(warnings.length > 0 || totalMismatch) && (
        <div className="rounded-lg border border-brand-orange/30 bg-brand-orange/5 p-3 text-sm text-brand-orange space-y-1">
          {totalMismatch && (
            <p>
              Tổng điểm lá ({total}) khác điểm tối đa môn ({subjectMaxScore}).
            </p>
          )}
          {warnings.map((w) => (
            <p key={w}>{w}</p>
          ))}
        </div>
      )}

      {groupBudgets.length > 0 && (
        <div className="rounded-lg border border-line bg-secondary/40 p-3 text-sm space-y-1">
          <p className="text-xs uppercase tracking-wide text-ink-soft">Ngân sách theo nhóm</p>
          {groupBudgets.map((g) => (
            <p
              key={g.label}
              className={g.mismatch ? "text-brand-orange" : "text-ink-soft"}
            >
              {g.label}: {g.sum} / {g.expected}
              {g.mismatch ? " — lệch ngân sách đã khai báo" : ""}
            </p>
          ))}
        </div>
      )}

      <PaperCard className="overflow-x-auto">
        <table className="w-full text-sm min-w-[640px]">
          <thead>
            <tr className="bg-secondary border-b border-line">
              <th className="text-left px-3 py-2 font-medium text-ink-soft">Nhóm</th>
              <th className="text-left px-3 py-2 font-medium text-ink-soft">Câu</th>
              <th className="text-left px-3 py-2 font-medium text-ink-soft">Mô tả</th>
              <th className="text-right px-3 py-2 font-medium text-ink-soft">Điểm</th>
              {!readOnly && <th className="px-3 py-2" />}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, i) => (
              <tr
                key={`${row.questionNumber}-${i}`}
                className={`border-t border-line ${row.warning || (row.confidence != null && row.confidence < 0.6) ? "bg-brand-orange/5" : ""}`}
              >
                <td className="px-3 py-2">
                  <input
                    disabled={readOnly}
                    className={inputClass}
                    value={row.groupLabel ?? ""}
                    onChange={(e) =>
                      updateRow(i, {
                        groupLabel: e.target.value || null,
                      })
                    }
                    placeholder="Yêu cầu 1"
                  />
                </td>
                <td className="px-3 py-2">
                  <input
                    disabled={readOnly}
                    className={`${inputClass} font-mono`}
                    value={row.questionNumber}
                    onChange={(e) => updateRow(i, { questionNumber: e.target.value })}
                  />
                </td>
                <td className="px-3 py-2">
                  <input
                    disabled={readOnly}
                    className={inputClass}
                    value={row.label ?? ""}
                    onChange={(e) => updateRow(i, { label: e.target.value || null })}
                  />
                </td>
                <td className="px-3 py-2">
                  <input
                    disabled={readOnly}
                    type="number"
                    min={0}
                    step={0.25}
                    className={`${inputClass} font-score text-right text-brand-red`}
                    value={row.maxScore}
                    onChange={(e) =>
                      updateRow(i, { maxScore: Number(e.target.value) || 0 })
                    }
                  />
                </td>
                {!readOnly && (
                  <td className="px-3 py-2 text-right">
                    <button
                      type="button"
                      onClick={() => removeRow(i)}
                      className="text-xs text-destructive hover:underline"
                    >
                      Xóa
                    </button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t border-line bg-secondary">
              <td colSpan={3} className="px-3 py-2 font-semibold text-ink">
                Tổng
              </td>
              <td className="px-3 py-2 text-right font-score font-bold text-brand-red">
                {total}
              </td>
              {!readOnly && <td />}
            </tr>
          </tfoot>
        </table>
      </PaperCard>

      {!readOnly && (
        <button
          type="button"
          onClick={addRow}
          className="text-sm text-brand-red hover:underline"
        >
          + Thêm dòng
        </button>
      )}
    </div>
  );
}
