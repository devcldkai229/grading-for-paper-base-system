import { useEffect, useState } from "react";
import { isAxiosError } from "axios";
import type { QuestionInput, ScoreGridTemplateSummary } from "@/types/catalog";
import { PaperCard } from "@/components/ui/paper-card";
import { adminCatalogService } from "@/services/adminCatalogService";

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
  for (const [label, rawSum] of sumsByLabel) {
    const percent = parseGroupPercent(label);
    if (percent === null) continue;
    const sum = Math.round(rawSum * 100) / 100;
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
  const total = Math.round(rows.reduce((s, r) => s + (Number(r.maxScore) || 0), 0) * 100) / 100;
  const totalMismatch = Math.abs(total - subjectMaxScore) > 0.01;
  const groupBudgets = computeGroupBudgets(rows, subjectMaxScore);

  const [templates, setTemplates] = useState<ScoreGridTemplateSummary[]>([]);
  const [applyingTemplateId, setApplyingTemplateId] = useState("");
  const [templateError, setTemplateError] = useState<string | null>(null);

  const [showSaveForm, setShowSaveForm] = useState(false);
  const [templateName, setTemplateName] = useState("");
  const [savingTemplate, setSavingTemplate] = useState(false);

  useEffect(() => {
    if (readOnly) return;
    adminCatalogService
      .listScoreGridTemplates()
      .then(setTemplates)
      .catch(() => setTemplates([]));
  }, [readOnly]);

  const handleApplyTemplate = async (templateId: string) => {
    setApplyingTemplateId(templateId);
    setTemplateError(null);
    try {
      const detail = await adminCatalogService.getScoreGridTemplate(templateId);
      onChange(
        detail.questions.map((q, i) => ({
          groupLabel: q.groupLabel,
          questionNumber: q.questionNumber,
          label: q.label,
          maxScore: q.maxScore,
          orderIndex: i,
        }))
      );
    } catch (err) {
      setTemplateError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Không áp dụng được mẫu."
          : "Không áp dụng được mẫu."
      );
    } finally {
      setApplyingTemplateId("");
    }
  };

  const handleSaveAsTemplate = async () => {
    if (!templateName.trim()) {
      setTemplateError("Vui lòng nhập tên mẫu.");
      return;
    }
    setSavingTemplate(true);
    setTemplateError(null);
    try {
      const created = await adminCatalogService.createScoreGridTemplate(
        templateName.trim(),
        rows.map((r) => ({
          groupLabel: r.groupLabel,
          questionNumber: r.questionNumber,
          label: r.label,
          maxScore: r.maxScore,
          orderIndex: r.orderIndex,
        }))
      );
      setTemplates((prev) => [created, ...prev]);
      setShowSaveForm(false);
      setTemplateName("");
    } catch (err) {
      setTemplateError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Không lưu được mẫu."
          : "Không lưu được mẫu."
      );
    } finally {
      setSavingTemplate(false);
    }
  };

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
      {!readOnly && (
        <div className="flex flex-wrap items-center gap-3">
          <select
            className="px-2 py-1.5 bg-card border border-line rounded text-sm text-ink outline-none disabled:opacity-60"
            value={applyingTemplateId}
            disabled={applyingTemplateId !== ""}
            onChange={(e) => {
              const id = e.target.value;
              if (id) void handleApplyTemplate(id);
            }}
          >
            <option value="">Áp dụng mẫu có sẵn…</option>
            {templates.map((t) => (
              <option key={t.id} value={t.id}>
                {t.name} ({t.questionCount} câu)
              </option>
            ))}
          </select>

          <button
            type="button"
            onClick={() => {
              setShowSaveForm((prev) => !prev);
              setTemplateError(null);
            }}
            className="text-sm text-brand-red hover:underline"
          >
            {showSaveForm ? "Huỷ lưu mẫu" : "Lưu làm mẫu"}
          </button>

          {showSaveForm && (
            <div className="flex items-center gap-2">
              <input
                autoFocus
                className={inputClass}
                placeholder="Tên mẫu, vd: PRN232 chuẩn"
                value={templateName}
                onChange={(e) => setTemplateName(e.target.value)}
              />
              <button
                type="button"
                onClick={() => void handleSaveAsTemplate()}
                disabled={savingTemplate}
                className="px-3 py-1.5 bg-primary text-primary-foreground hover:opacity-90 disabled:opacity-50 rounded-lg text-sm font-medium whitespace-nowrap"
              >
                {savingTemplate ? "Đang lưu..." : "Lưu"}
              </button>
            </div>
          )}
        </div>
      )}

      {templateError && (
        <p className="text-sm text-destructive">{templateError}</p>
      )}

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
