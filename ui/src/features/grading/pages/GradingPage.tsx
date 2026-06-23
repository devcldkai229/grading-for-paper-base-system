import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { FileViewer } from "@/components/FileViewer";
import { RedGutter } from "@/components/grading/RedGutter";
import { ScoreStamp } from "@/components/grading/ScoreStamp";
import { ValidScoreTick } from "@/components/grading/ValidScoreTick";
import { StatusBadge } from "@/components/ui/status-badge";
import { PaperCard } from "@/components/ui/paper-card";
import { authService } from "@/services/authService";
import { catalogService } from "@/services/catalogService";
import { gradingService } from "@/services/gradingService";
import { submissionService } from "@/services/submissionService";
import { clearGradingQueue, loadGradingQueue, saveGradingQueue, updateQueueAssignmentStatus, updateQueueCurrentAssignment } from "@/lib/gradingQueue";
import type { FileUrlResponse } from "@/types/catalog";
import type { GradingSession, SaveMarksPayload, AssignmentSummary } from "@/types/grading";
import type { PaperFile, StudentPaperDetail } from "@/types/submission";

type LeftMode = "answer" | "exam" | "rubric";
type SaveStatus = "idle" | "saving" | "saved" | "error";

interface MarkDraft {
  score: string;
  questionComment: string;
}

function isValidScore(raw: string | undefined, max: number): boolean {
  const t = raw?.trim() ?? "";
  if (t === "") return false;
  const n = Number(t);
  return !Number.isNaN(n) && n >= 0 && n <= max;
}

function scoreFromDraft(raw: string | undefined, max: number): number {
  const t = raw?.trim() ?? "";
  if (!t) return 0;
  const n = Number(t);
  if (Number.isNaN(n)) return 0;
  return Math.min(Math.max(0, n), max);
}

interface QuestionGroup {
  key: string;
  label: string | null;
  items: GradingSession["questions"];
}

function buildQuestionGroups(questions: GradingSession["questions"]): QuestionGroup[] {
  const sorted = [...questions].sort((a, b) => {
    if (a.orderIndex !== b.orderIndex) return a.orderIndex - b.orderIndex;
    return a.questionNumber.localeCompare(b.questionNumber, undefined, { numeric: true });
  });

  const groups: QuestionGroup[] = [];
  for (const q of sorted) {
    const groupLabel = q.groupLabel?.trim() || null;
    if (!groupLabel) {
      groups.push({ key: q.questionNumber, label: null, items: [q] });
      continue;
    }

    const existing = groups.find((g) => g.label === groupLabel);
    if (existing) {
      existing.items.push(q);
    } else {
      groups.push({ key: groupLabel, label: groupLabel, items: [q] });
    }
  }
  return groups;
}

function isAxiosStatus(err: unknown, status: number): boolean {
  return (
    typeof err === "object" &&
    err !== null &&
    "response" in err &&
    (err as { response?: { status?: number } }).response?.status === status
  );
}

export function GradingPage() {
  const { assignmentId } = useParams<{ assignmentId: string }>();
  const navigate = useNavigate();

  const [session, setSession] = useState<GradingSession | null>(null);
  const [paperDetail, setPaperDetail] = useState<StudentPaperDetail | null>(null);
  const [marks, setMarks] = useState<Record<string, MarkDraft>>({});
  const [paperComment, setPaperComment] = useState("");
  const [internalComment, setInternalComment] = useState("");
  const [rowVersion, setRowVersion] = useState(0);

  const [leftMode, setLeftMode] = useState<LeftMode>("answer");
  const [selectedFileId, setSelectedFileId] = useState<string | null>(null);
  const [fileView, setFileView] = useState<FileUrlResponse | null>(null);
  const [fileLoading, setFileLoading] = useState(false);

  const [loading, setLoading] = useState(true);
  const [saveStatus, setSaveStatus] = useState<SaveStatus>("idle");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflictMsg, setConflictMsg] = useState<string | null>(null);
  const [batchAssignments, setBatchAssignments] = useState<AssignmentSummary[]>([]);
  const [switchingAssignment, setSwitchingAssignment] = useState(false);

  const urlCacheRef = useRef<Record<string, { data: FileUrlResponse; expiresAt: number }>>({});
  const saveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isReadOnly = session?.status === "Submitted";

  const queue = loadGradingQueue();
  const queueIndex = batchAssignments.findIndex((a) => a.assignmentId === assignmentId);
  const queueLabel =
    batchAssignments.length > 0 && queueIndex >= 0
      ? `Bài ${queueIndex + 1} / ${batchAssignments.length}`
      : null;

  const syncQueueFromStartBatch = useCallback(
    (assignments: AssignmentSummary[], batchId: string, subjectId: string) => {
      setBatchAssignments(assignments);
      saveGradingQueue({
        assignmentIds: assignments.map((a) => a.assignmentId),
        assignments: assignments.map((a) => ({
          assignmentId: a.assignmentId,
          aliasNumber: a.aliasNumber,
          status: a.status,
        })),
        subjectId,
        batchId,
        currentAssignmentId: assignmentId,
      });
    },
    [assignmentId]
  );

  const ensureBatchQueue = useCallback(
    async (data: GradingSession) => {
      const existing = loadGradingQueue();
      if (existing?.batchId === data.batchId && existing.assignments.length > 0) {
        setBatchAssignments(
          existing.assignments.map((a) => ({
            assignmentId: a.assignmentId,
            paperId: "",
            aliasNumber: a.aliasNumber,
            status: a.status,
          }))
        );
        updateQueueCurrentAssignment(data.assignmentId);
        updateQueueAssignmentStatus(data.assignmentId, data.status);
        return;
      }

      const batch = await gradingService.startBatch(data.batchId);
      syncQueueFromStartBatch(batch.assignments, batch.batchId, batch.subjectId);
    },
    [syncQueueFromStartBatch]
  );

  const applySession = useCallback((data: GradingSession) => {
    setSession(data);
    setRowVersion(data.rowVersion);
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
  }, []);

  const loadAll = useCallback(async () => {
    if (!assignmentId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await gradingService.getSession(assignmentId);
      applySession(data);
      await ensureBatchQueue(data);
      updateQueueCurrentAssignment(assignmentId);
      updateQueueAssignmentStatus(assignmentId, data.status);
      const detail = await submissionService.getSubmissionDetail(data.studentPaperId);
      setPaperDetail(detail);
      if (detail.files.length > 0) {
        setSelectedFileId(detail.files[0].id);
      }
    } catch {
      setError("Không tải được phiên chấm bài. Kiểm tra đăng nhập và API Grading.");
    } finally {
      setLoading(false);
    }
  }, [assignmentId, applySession, ensureBatchQueue]);

  useEffect(() => {
    if (!authService.decodeToken()) {
      navigate("/login", { replace: true });
      return;
    }
    loadAll();
  }, [loadAll, navigate]);

  const totalScore = useMemo(() => {
    if (!session) return 0;
    return session.questions.reduce((sum, q) => {
      const raw = marks[q.questionNumber]?.score?.trim();
      if (!raw) return sum;
      const n = Number(raw);
      if (Number.isNaN(n)) return sum;
      return sum + Math.min(Math.max(0, n), q.maxScore);
    }, 0);
  }, [session, marks]);

  const buildPayload = useCallback((): SaveMarksPayload | null => {
    if (!session) return null;
    return {
      rowVersion,
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
    };
  }, [session, rowVersion, paperComment, internalComment, marks]);

  const persistMarks = useCallback(
    async (silent = true) => {
      if (!assignmentId || !session || isReadOnly) return;
      const payload = buildPayload();
      if (!payload) return;

      if (!silent) setSaveStatus("saving");
      try {
        const result = await gradingService.saveMarks(assignmentId, payload);
        setRowVersion(result.rowVersion);
        setSaveStatus("saved");
        setConflictMsg(null);
      } catch (err) {
        if (isAxiosStatus(err, 409)) {
          setConflictMsg("Dữ liệu đã thay đổi ở nơi khác. Đang tải lại...");
          setSaveStatus("error");
          await loadAll();
          setConflictMsg(null);
          return;
        }
        setSaveStatus("error");
        if (!silent) setError("Lưu điểm thất bại");
      }
    },
    [assignmentId, session, isReadOnly, buildPayload, loadAll]
  );

  useEffect(() => {
    if (!session || isReadOnly || loading) return;
    if (saveTimerRef.current) clearTimeout(saveTimerRef.current);
    saveTimerRef.current = setTimeout(() => {
      void persistMarks(true);
    }, 800);
    return () => {
      if (saveTimerRef.current) clearTimeout(saveTimerRef.current);
    };
  }, [marks, paperComment, internalComment, session, isReadOnly, loading, persistMarks]);

  const openAnswerFile = useCallback(
    async (fileId: string) => {
      if (!session) return;
      setFileLoading(true);
      setSelectedFileId(fileId);
      try {
        const data = await submissionService.getFileUrl(session.studentPaperId, fileId);
        setFileView(data);
      } catch {
        setError("Không tải được file bài làm");
      } finally {
        setFileLoading(false);
      }
    },
    [session]
  );

  useEffect(() => {
    if (leftMode !== "answer" || !selectedFileId || !session) return;
    void openAnswerFile(selectedFileId);
  }, [leftMode, selectedFileId, session, openAnswerFile]);

  const openCatalogFile = useCallback(
    async (type: "exam" | "rubric") => {
      if (!session) return;
      setFileLoading(true);
      const cacheKey =
        type === "exam"
          ? `exam_${session.subjectId}`
          : `rubric_${session.subjectId}_v${session.rubricVersion}`;
      const now = Date.now();
      const cached = urlCacheRef.current[cacheKey];
      if (cached && cached.expiresAt > now) {
        setFileView(cached.data);
        setFileLoading(false);
        return;
      }
      try {
        const data =
          type === "exam"
            ? await catalogService.getExamPaperUrl(session.subjectId)
            : await catalogService.getRubricUrl(session.subjectId);
        urlCacheRef.current[cacheKey] = {
          data,
          expiresAt: now + 9 * 60 * 1000,
        };
        setFileView(data);
      } catch {
        setError(type === "exam" ? "Chưa có đề thi" : "Chưa có barem");
      } finally {
        setFileLoading(false);
      }
    },
    [session]
  );

  const handleScoreChange = (questionNumber: string, maxScore: number, value: string) => {
    if (isReadOnly) return;
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
    setSaveStatus("idle");
  };

  const handleSubmit = useCallback(async () => {
    if (!assignmentId || isReadOnly) return;
    setSubmitting(true);
    setError(null);
    try {
      await persistMarks(false);
      const result = await gradingService.submit(assignmentId);
      updateQueueAssignmentStatus(assignmentId, "Submitted");
      if (result.nextAssignmentId) {
        navigate(`/grading/${result.nextAssignmentId}`);
      } else {
        clearGradingQueue();
        alert("Đã chấm xong batch!");
        navigate(queue ? `/submissions?subjectId=${queue.subjectId}` : "/dashboard");
      }
    } catch (err) {
      if (isAxiosStatus(err, 409)) {
        setConflictMsg("Xung đột khi nộp bài. Đang tải lại...");
        await loadAll();
        setConflictMsg(null);
      } else {
        setError("Nộp bài thất bại");
      }
    } finally {
      setSubmitting(false);
    }
  }, [assignmentId, isReadOnly, persistMarks, navigate, queue, loadAll]);

  const switchAssignment = useCallback(
    async (targetId: string) => {
      if (!assignmentId || targetId === assignmentId || switchingAssignment) return;
      setSwitchingAssignment(true);
      try {
        if (!isReadOnly) await persistMarks(true);
        navigate(`/grading/${targetId}`);
      } finally {
        setSwitchingAssignment(false);
      }
    },
    [assignmentId, switchingAssignment, isReadOnly, persistMarks, navigate]
  );

  const assignmentStatusClass = (status: string, active: boolean) => {
    if (active) return "bg-brand-red/10 border-brand-red/50 text-brand-red font-semibold";
    if (status === "Submitted") return "bg-done/10 border-done/30 text-done";
    if (status === "Drafting") return "bg-brand-orange/10 border-brand-orange/30 text-brand-orange";
    return "bg-card border-line text-ink-soft hover:border-brand-red/30";
  };

  const handleSubmitRef = useRef(handleSubmit);
  const persistMarksRef = useRef(persistMarks);

  useEffect(() => {
    handleSubmitRef.current = handleSubmit;
    persistMarksRef.current = persistMarks;
  }, [handleSubmit, persistMarks]);

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.ctrlKey && e.key.toLowerCase() === "s") {
        e.preventDefault();
        void persistMarksRef.current(false);
      }
      if (e.altKey && e.key === "ArrowRight") {
        e.preventDefault();
        void handleSubmitRef.current();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  if (loading) {
    return (
      <div className="min-h-svh bg-paper flex items-center justify-center">
        <div className="animate-spin w-8 h-8 border-2 border-line border-t-brand-red rounded-full" />
      </div>
    );
  }

  if (!session || !paperDetail) {
    return (
      <div className="min-h-svh bg-paper flex flex-col items-center justify-center gap-4 text-ink-soft">
        <p>{error ?? "Không tìm thấy phiên chấm bài"}</p>
        <button
          onClick={() => navigate(-1)}
          className="px-4 py-2 border border-line hover:bg-secondary rounded-lg text-sm text-ink"
        >
          Quay lại
        </button>
      </div>
    );
  }

  const saveLabel =
    saveStatus === "saving"
      ? "Đang lưu..."
      : saveStatus === "saved"
        ? "Đã lưu"
        : saveStatus === "error"
          ? "Lỗi lưu"
          : "";

  const questionGroups = buildQuestionGroups(session.questions);

  const groupSubtotal = (items: typeof session.questions) =>
    items.reduce((sum, q) => sum + scoreFromDraft(marks[q.questionNumber]?.score, q.maxScore), 0);

  const groupMaxTotal = (items: typeof session.questions) =>
    items.reduce((sum, q) => sum + q.maxScore, 0);

  const inputClass =
    "w-full px-3 py-2 bg-card border border-line rounded-lg text-sm text-ink disabled:opacity-60 focus:border-primary focus:ring-1 focus:ring-primary/30 outline-none";

  return (
    <div className="min-h-svh bg-paper text-ink">
      <div className="max-w-[1600px] mx-auto px-4 py-4 lg:px-6 lg:py-6">
        <header className="flex flex-wrap items-center justify-between gap-3 mb-4">
          <div>
            <h1 className="font-display text-2xl font-semibold text-ink">
              Chấm bài — {session.studentAlias ?? `#${session.aliasNumber}`}
            </h1>
            <p className="text-sm text-ink-soft mt-1 flex flex-wrap items-center gap-2">
              {queueLabel && <span>{queueLabel}</span>}
              {isReadOnly && <StatusBadge variant="done">Đã nộp</StatusBadge>}
            </p>
          </div>
          <div className="flex items-center gap-3 text-sm text-ink-soft">
            {saveLabel && (
              <span className={saveStatus === "saved" ? "text-done" : ""}>
                {saveLabel}
              </span>
            )}
            {conflictMsg && <span className="text-brand-orange">{conflictMsg}</span>}
            {error && <span className="text-destructive">{error}</span>}
          </div>
        </header>

        {batchAssignments.length > 1 && (
          <PaperCard className="mb-4 p-3 max-h-36 overflow-y-auto">
            <p className="text-xs text-ink-soft mb-2 uppercase tracking-wide">
              Danh sách bài làm ({batchAssignments.length} học sinh)
            </p>
            <div className="flex flex-wrap gap-2">
              {batchAssignments.map((a, i) => {
                const active = a.assignmentId === assignmentId;
                return (
                  <button
                    key={a.assignmentId}
                    type="button"
                    disabled={switchingAssignment}
                    onClick={() => void switchAssignment(a.assignmentId)}
                    title={
                      a.aliasNumber != null
                        ? `Student_${String(a.aliasNumber).padStart(4, "0")}`
                        : undefined
                    }
                    className={`px-2.5 py-1 rounded-lg text-xs border font-score transition-colors disabled:opacity-50 ${assignmentStatusClass(a.status, active)}`}
                  >
                    Bài {i + 1}
                  </button>
                );
              })}
            </div>
          </PaperCard>
        )}

        <div className="flex flex-col xl:flex-row gap-0 xl:gap-0">
          <PaperCard className="flex-1 p-4 min-h-[500px] flex flex-col xl:rounded-r-none">
            <div className="flex flex-wrap gap-2 mb-4">
              {leftMode === "answer" ? (
                <>
                  <button
                    type="button"
                    onClick={() => {
                      setLeftMode("exam");
                      void openCatalogFile("exam");
                    }}
                    className="px-3 py-1.5 border border-brand-red/40 text-brand-red hover:bg-brand-red/5 rounded-lg text-sm"
                  >
                    Xem đề
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setLeftMode("rubric");
                      void openCatalogFile("rubric");
                    }}
                    className="px-3 py-1.5 border border-brand-orange/40 text-brand-orange hover:bg-brand-orange/5 rounded-lg text-sm"
                  >
                    Xem barem
                  </button>
                </>
              ) : (
                <button
                  type="button"
                  onClick={() => {
                    setLeftMode("answer");
                    if (selectedFileId) void openAnswerFile(selectedFileId);
                  }}
                  className="px-3 py-1.5 border border-line hover:bg-secondary rounded-lg text-sm"
                >
                  ← Bài làm
                </button>
              )}
              <span className="text-xs text-ink-soft self-center ml-auto">
                {leftMode === "answer" ? "Bài làm" : leftMode === "exam" ? "Đề thi" : "Barem"}
              </span>
            </div>

            {leftMode === "answer" && paperDetail.files.length > 1 && (
              <div className="flex gap-2 flex-wrap mb-3">
                {paperDetail.files.map((f: PaperFile) => (
                  <button
                    key={f.id}
                    type="button"
                    title={f.fileName ?? undefined}
                    onClick={() => {
                      setSelectedFileId(f.id);
                      void openAnswerFile(f.id);
                    }}
                    className={`px-3 py-1 rounded-lg text-xs border font-score ${
                      selectedFileId === f.id
                        ? "bg-brand-red/10 border-brand-red/40 text-brand-red"
                        : "bg-card border-line text-ink-soft hover:border-brand-red/30"
                    }`}
                  >
                    Trang {f.orderIndex + 1}
                  </button>
                ))}
              </div>
            )}

            <div className="flex-1 overflow-auto">
              {fileLoading ? (
                <div className="flex justify-center py-16">
                  <div className="animate-spin w-8 h-8 border-2 border-line border-t-brand-red rounded-full" />
                </div>
              ) : fileView ? (
                <FileViewer
                  url={fileView.url}
                  contentType={fileView.contentType}
                  fileName={fileView.fileName}
                />
              ) : (
                <p className="text-center text-ink-soft py-16">Chọn file để xem</p>
              )}
            </div>
          </PaperCard>

          <RedGutter />

          <PaperCard className="flex-1 p-4 xl:rounded-l-none xl:border-l-0">
            <h2 className="font-display text-lg font-semibold text-ink mb-4">
              Sổ điểm
            </h2>

            <div className="space-y-4 max-h-[50vh] overflow-y-auto pr-1 mb-4">
              {questionGroups.map((group) => (
                <div key={group.key}>
                  {group.label && (
                    <div className="flex items-center justify-between gap-2 mb-2">
                      <h3 className="text-sm font-semibold text-ink">{group.label}</h3>
                      <span className="text-xs font-score text-ink-soft">
                        {groupSubtotal(group.items).toFixed(2)} / {groupMaxTotal(group.items).toFixed(2)}
                      </span>
                    </div>
                  )}
                  <div
                    className={
                      group.label
                        ? "space-y-2 pl-2 border-l-2 border-line"
                        : "space-y-2"
                    }
                  >
                    {group.items.map((q) => {
                      const scoreRaw = marks[q.questionNumber]?.score;
                      const valid = isValidScore(scoreRaw, q.maxScore);
                      return (
                        <div
                          key={q.questionNumber}
                          className="grid grid-cols-12 gap-2 items-center p-2 rounded-lg hover:bg-paper"
                        >
                          <div className="col-span-3 text-sm font-medium text-ink">
                            {q.label?.trim() || q.questionNumber}
                            <div className="text-xs text-ink-soft font-score">
                              {q.label?.trim() ? q.questionNumber + " · " : ""}/ {q.maxScore}
                            </div>
                          </div>
                          <div className="col-span-3 flex items-center gap-1">
                            <input
                              type="number"
                              min={0}
                              max={q.maxScore}
                              step={0.25}
                              disabled={isReadOnly}
                              value={scoreRaw ?? ""}
                              onChange={(e) =>
                                handleScoreChange(
                                  q.questionNumber,
                                  q.maxScore,
                                  e.target.value
                                )
                              }
                              className={`${inputClass} font-score text-brand-red`}
                              placeholder="0"
                              aria-label={`Điểm câu ${q.questionNumber}`}
                            />
                            <ValidScoreTick show={valid} />
                          </div>
                          <div className="col-span-6">
                            <input
                              type="text"
                              disabled={isReadOnly}
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
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>

            <ScoreStamp total={totalScore} max={session.subjectMaxScore} />

            <div className="space-y-3 my-6">
              <div>
                <label className="text-xs text-ink-soft mb-1 block">
                  Nhận xét bài
                </label>
                <textarea
                  disabled={isReadOnly}
                  value={paperComment}
                  onChange={(e) => setPaperComment(e.target.value)}
                  rows={3}
                  className={`${inputClass} resize-none`}
                />
              </div>
              <div>
                <label className="text-xs text-ink-soft mb-1 block">
                  Ghi chú nội bộ
                </label>
                <textarea
                  disabled={isReadOnly}
                  value={internalComment}
                  onChange={(e) => setInternalComment(e.target.value)}
                  rows={2}
                  className={`${inputClass} resize-none`}
                />
              </div>
            </div>

            {!isReadOnly && (
              <div className="flex flex-wrap gap-3">
                <button
                  type="button"
                  onClick={() => void persistMarks(false)}
                  className="px-4 py-2.5 border border-line hover:bg-secondary rounded-lg text-sm font-medium text-ink"
                >
                  Lưu nháp
                </button>
                <button
                  type="button"
                  onClick={() => void handleSubmit()}
                  disabled={submitting}
                  className="flex-1 min-w-[160px] px-4 py-2.5 bg-primary text-primary-foreground hover:opacity-90 disabled:opacity-50 rounded-lg text-sm font-medium"
                >
                  {submitting ? "Đang nộp..." : "Nộp & bài tiếp"}
                </button>
              </div>
            )}
          </PaperCard>
        </div>
      </div>
    </div>
  );
}
