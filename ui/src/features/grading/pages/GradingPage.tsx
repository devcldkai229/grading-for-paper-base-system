import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { isAxiosError } from "axios";
import { FileViewer } from "@/components/FileViewer";
import { QuestionJumpList } from "@/components/grading/QuestionJumpList";
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
import type { AuditLogEntry, GradingSession, SaveMarksPayload, AssignmentSummary } from "@/types/grading";
import type { PaperFile, StudentPaperDetail } from "@/types/submission";

const auditActionLabels: Record<string, string> = {
  ScoreUpdated: "Lưu điểm",
  FormSubmitted: "Nộp bài",
  ScoreOverridden: "Override điểm",
};

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

// Background "still actively grading" heartbeat (feeds GradingService's per-paper time
// tracking / throughput analytics) — sent only while the tab is visible and the lecturer has
// interacted within the idle threshold; no visible UI, purely instrumentation.
const HEARTBEAT_INTERVAL_MS = 15_000;
const IDLE_THRESHOLD_MS = 30_000;

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
  const [isFlagged, setIsFlagged] = useState(false);
  const [togglingFlag, setTogglingFlag] = useState(false);

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

  const [overrideMode, setOverrideMode] = useState(false);
  const [overrideReason, setOverrideReason] = useState("");
  const [overriding, setOverriding] = useState(false);
  const [overrideError, setOverrideError] = useState<string | null>(null);
  const [showAuditLog, setShowAuditLog] = useState(false);
  const [auditLog, setAuditLog] = useState<AuditLogEntry[] | null>(null);
  const [auditLoading, setAuditLoading] = useState(false);
  const [auditError, setAuditError] = useState<string | null>(null);
  const [aiSuggesting, setAiSuggesting] = useState(false);
  const [aiError, setAiError] = useState<string | null>(null);

  const urlCacheRef = useRef<Record<string, { data: FileUrlResponse; expiresAt: number }>>({});
  const saveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const lastSavedPayloadRef = useRef<string>("");
  const lastActivityRef = useRef<number>(0);
  const isReadOnly = session?.status === "Submitted";
  const inputsLocked = isReadOnly && !overrideMode;

  const [activeQuestionNumber, setActiveQuestionNumber] = useState<string | null>(null);
  const questionRowRefs = useRef<Record<string, HTMLDivElement | null>>({});
  const questionInputRefs = useRef<Record<string, HTMLInputElement | null>>({});

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
    setIsFlagged(data.isFlagged);
    const draft: Record<string, MarkDraft> = {};
    for (const q of data.questions) {
      draft[q.questionNumber] = {
        score: q.score != null ? String(q.score) : "",
        questionComment: q.questionComment ?? "",
      };
    }
    setMarks(draft);

    const initialPayload = JSON.stringify({
      paperComment: data.paperComment ?? "",
      internalComment: data.internalComment ?? "",
      questions: data.questions.map((q) => {
        const raw = draft[q.questionNumber]?.score?.trim() ?? "";
        const score = raw === "" ? 0 : Math.min(Math.max(0, Number(raw)), q.maxScore);
        return {
          questionNumber: q.questionNumber,
          score: Number.isNaN(score) ? 0 : score,
          questionComment: draft[q.questionNumber]?.questionComment ?? "",
        };
      })
    });
    lastSavedPayloadRef.current = initialPayload;
  }, []);

  const loadAll = useCallback(async () => {
    if (!assignmentId) return;
    setLoading(true);
    setError(null);
    setFileView(null);
    setSelectedFileId(null);
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

  const refreshSession = useCallback(async () => {
    if (!assignmentId) return;
    try {
      const data = await gradingService.getSession(assignmentId);
      applySession(data);
      updateQueueAssignmentStatus(assignmentId, data.status);
    } catch {
      /* silent poll failure */
    }
  }, [assignmentId, applySession]);

  useEffect(() => {
    const aiStatus = session?.aiStatus ?? "NotRequested";
    if (aiStatus !== "Queued" && aiStatus !== "Processing") return;

    const timer = setInterval(() => {
      void refreshSession();
    }, 2500);

    return () => clearInterval(timer);
  }, [session?.aiStatus, refreshSession]);

  const handleAiSuggest = useCallback(async () => {
    if (!assignmentId || isReadOnly) return;
    setAiSuggesting(true);
    setAiError(null);
    try {
      await gradingService.requestAiSuggest(assignmentId);
      await refreshSession();
    } catch (err) {
      if (isAxiosStatus(err, 409)) {
        setAiError("AI đang xử lý bài này — vui lòng đợi.");
      } else {
        setAiError("Không gọi được AI gợi ý điểm.");
      }
    } finally {
      setAiSuggesting(false);
    }
  }, [assignmentId, isReadOnly, refreshSession]);

  const aiStatusLabel = useMemo(() => {
    const s = session?.aiStatus ?? "NotRequested";
    switch (s) {
      case "Queued":
        return "AI: đang chờ";
      case "Processing":
        return "AI: đang chấm";
      case "Completed":
        return "AI: xong";
      case "Failed":
        return "AI: lỗi";
      default:
        return null;
    }
  }, [session?.aiStatus]);

  const orderedQuestions = useMemo(
    () => (session ? buildQuestionGroups(session.questions).flatMap((g) => g.items) : []),
    [session]
  );

  // Default to the first question once a session loads, and re-clamp if the
  // active question no longer exists (e.g. after switching to another paper).
  useEffect(() => {
    if (orderedQuestions.length === 0) {
      setActiveQuestionNumber(null);
      return;
    }
    setActiveQuestionNumber((prev) =>
      prev && orderedQuestions.some((q) => q.questionNumber === prev)
        ? prev
        : orderedQuestions[0].questionNumber
    );
  }, [orderedQuestions]);

  const jumpToQuestion = useCallback((questionNumber: string) => {
    setActiveQuestionNumber(questionNumber);
    // Wait a frame so the row/input refs are attached before we scroll/focus.
    requestAnimationFrame(() => {
      questionRowRefs.current[questionNumber]?.scrollIntoView({
        behavior: "smooth",
        block: "nearest",
      });
      questionInputRefs.current[questionNumber]?.focus();
    });
  }, []);

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

      const serialized = JSON.stringify({
        paperComment: payload.paperComment,
        internalComment: payload.internalComment,
        questions: payload.questions,
      });

      if (serialized === lastSavedPayloadRef.current) {
        return;
      }

      if (!silent) setSaveStatus("saving");
      try {
        const result = await gradingService.saveMarks(assignmentId, payload);
        setRowVersion(result.rowVersion);
        setSaveStatus("saved");
        setConflictMsg(null);
        lastSavedPayloadRef.current = serialized;
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
        setError(null);
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
    if (inputsLocked) return;
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

  const handleToggleFlag = useCallback(async () => {
    if (!assignmentId) return;
    const next = !isFlagged;
    setTogglingFlag(true);
    try {
      await gradingService.setFlag(assignmentId, next);
      setIsFlagged(next);
    } catch {
      setError("Không thể cập nhật cờ đánh dấu");
    } finally {
      setTogglingFlag(false);
    }
  }, [assignmentId, isFlagged]);

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

  const handleBackToDashboard = useCallback(async () => {
    try {
      if (!isReadOnly) await persistMarks(true);
    } catch (e) {
      console.error("Failed to save progress before leaving:", e);
    }
    navigate("/dashboard");
  }, [isReadOnly, persistMarks, navigate]);

  const handleStartOverride = useCallback(() => {
    setOverrideMode(true);
    setOverrideReason("");
    setOverrideError(null);
  }, []);

  const handleCancelOverride = useCallback(() => {
    setOverrideMode(false);
    setOverrideReason("");
    setOverrideError(null);
    if (session) applySession(session);
  }, [session, applySession]);

  const loadAuditLog = useCallback(async () => {
    if (!assignmentId) return;
    setAuditLoading(true);
    setAuditError(null);
    try {
      const entries = await gradingService.getAuditLog(assignmentId);
      setAuditLog(entries);
    } catch {
      setAuditError("Không tải được lịch sử chỉnh sửa");
    } finally {
      setAuditLoading(false);
    }
  }, [assignmentId]);

  const handleConfirmOverride = useCallback(async () => {
    if (!assignmentId || !session || overrideReason.trim() === "") return;
    setOverriding(true);
    setOverrideError(null);
    try {
      await gradingService.overrideMarks(assignmentId, {
        reason: overrideReason.trim(),
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
      setOverrideMode(false);
      setOverrideReason("");
      await loadAll();
      if (showAuditLog) await loadAuditLog();
    } catch (err) {
      const msg =
        (isAxiosError(err) && (err.response?.data as { message?: string })?.message) ||
        "Override thất bại";
      setOverrideError(msg);
    } finally {
      setOverriding(false);
    }
  }, [assignmentId, session, overrideReason, paperComment, internalComment, marks, loadAll, showAuditLog, loadAuditLog]);

  const toggleAuditLog = useCallback(() => {
    setShowAuditLog((prev) => {
      const next = !prev;
      if (next && auditLog === null) void loadAuditLog();
      return next;
    });
  }, [auditLog, loadAuditLog]);

  const assignmentStatusClass = (status: string, active: boolean) => {
    if (active) return "bg-brand-red/10 border-brand-red/50 text-brand-red font-semibold";
    if (status === "Submitted") return "bg-done/10 border-done/30 text-done";
    if (status === "Drafting") return "bg-brand-orange/10 border-brand-orange/30 text-brand-orange";
    return "bg-card border-line text-ink-soft hover:border-brand-red/30";
  };

  const handleSubmitRef = useRef(handleSubmit);
  const persistMarksRef = useRef(persistMarks);
  const jumpToQuestionRef = useRef(jumpToQuestion);
  const orderedQuestionsRef = useRef(orderedQuestions);
  const activeQuestionNumberRef = useRef(activeQuestionNumber);

  useEffect(() => {
    handleSubmitRef.current = handleSubmit;
    persistMarksRef.current = persistMarks;
    jumpToQuestionRef.current = jumpToQuestion;
    orderedQuestionsRef.current = orderedQuestions;
    activeQuestionNumberRef.current = activeQuestionNumber;
  }, [handleSubmit, persistMarks, jumpToQuestion, orderedQuestions, activeQuestionNumber]);

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
      if (e.altKey && (e.key === "ArrowDown" || e.key === "ArrowUp")) {
        const list = orderedQuestionsRef.current;
        if (list.length === 0) return;
        e.preventDefault();
        const currentIndex = list.findIndex(
          (q) => q.questionNumber === activeQuestionNumberRef.current
        );
        const delta = e.key === "ArrowDown" ? 1 : -1;
        const nextIndex = Math.min(
          Math.max((currentIndex === -1 ? 0 : currentIndex) + delta, 0),
          list.length - 1
        );
        jumpToQuestionRef.current(list[nextIndex].questionNumber);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  useEffect(() => {
    const markActive = () => {
      lastActivityRef.current = Date.now();
    };
    markActive();
    window.addEventListener("mousemove", markActive, { passive: true });
    window.addEventListener("mousedown", markActive, { passive: true });
    window.addEventListener("keydown", markActive, { passive: true });
    window.addEventListener("scroll", markActive, { passive: true });
    return () => {
      window.removeEventListener("mousemove", markActive);
      window.removeEventListener("mousedown", markActive);
      window.removeEventListener("keydown", markActive);
      window.removeEventListener("scroll", markActive);
    };
  }, []);

  useEffect(() => {
    if (!assignmentId || isReadOnly) return;
    const interval = setInterval(() => {
      const activeRecently = Date.now() - lastActivityRef.current < IDLE_THRESHOLD_MS;
      if (document.visibilityState === "visible" && activeRecently) {
        void gradingService.recordHeartbeat(assignmentId, HEARTBEAT_INTERVAL_MS / 1000).catch(() => {});
      }
    }, HEARTBEAT_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [assignmentId, isReadOnly]);

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
              {aiStatusLabel && (
                <StatusBadge variant={session?.aiStatus === "Failed" ? "error" : "ai"}>
                  {aiStatusLabel}
                </StatusBadge>
              )}
              {isReadOnly && <StatusBadge variant="done">Đã nộp</StatusBadge>}
            </p>
          </div>
          <div className="flex flex-col gap-2">
            <button
              type="button"
              onClick={() => void handleBackToDashboard()}
              className="inline-flex items-center gap-2 text-sm text-ink-soft hover:text-brand-red font-medium transition-colors mb-1 cursor-pointer w-fit"
            >
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 19l-7-7m0 0l7-7m-7 7h18" />
              </svg>
              Quay lại Dashboard
            </button>
          </div>
          <div className="flex items-center gap-3 text-sm text-ink-soft">
            {saveLabel && (
              <span className={saveStatus === "saved" ? "text-done" : ""}>
                {saveLabel}
              </span>
            )}
            {conflictMsg && <span className="text-brand-orange">{conflictMsg}</span>}
            {aiError && <span className="text-brand-orange">{aiError}</span>}
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

            <QuestionJumpList
              questions={orderedQuestions}
              activeQuestionNumber={activeQuestionNumber}
              isAnswered={(qn) => {
                const q = orderedQuestions.find((item) => item.questionNumber === qn);
                return q ? isValidScore(marks[qn]?.score, q.maxScore) : false;
              }}
              onJump={jumpToQuestion}
            />

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
                      const isActive = q.questionNumber === activeQuestionNumber;
                      return (
                        <div
                          key={q.questionNumber}
                          ref={(el) => {
                            questionRowRefs.current[q.questionNumber] = el;
                          }}
                          className={`grid grid-cols-12 gap-2 items-center p-2 rounded-lg transition-colors ${
                            isActive
                              ? "bg-brand-red/5 ring-1 ring-brand-red/30"
                              : "hover:bg-paper"
                          }`}
                        >
                          <div className="col-span-3 text-sm font-medium text-ink">
                            <span className="inline-flex items-center gap-1.5 flex-wrap">
                              {q.label?.trim() || q.questionNumber}
                              {q.aiDrafted && (
                                <span className="text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded bg-brand-red/10 text-brand-red font-semibold">
                                  AI
                                </span>
                              )}
                            </span>
                            <div className="text-xs text-ink-soft font-score">
                              {q.label?.trim() ? q.questionNumber + " · " : ""}/ {q.maxScore}
                            </div>
                          </div>
                          <div className="col-span-3 flex items-center gap-1">
                            <input
                              ref={(el) => {
                                questionInputRefs.current[q.questionNumber] = el;
                              }}
                              type="number"
                              min={0}
                              max={q.maxScore}
                              step={0.25}
                              disabled={inputsLocked}
                              value={scoreRaw ?? ""}
                              onChange={(e) =>
                                handleScoreChange(
                                  q.questionNumber,
                                  q.maxScore,
                                  e.target.value
                                )
                              }
                              onFocus={() => setActiveQuestionNumber(q.questionNumber)}
                              className={`${inputClass} font-score text-brand-red`}
                              placeholder="0"
                              aria-label={`Điểm câu ${q.questionNumber}`}
                            />
                            <ValidScoreTick show={valid} />
                          </div>
                          <div className="col-span-6">
                            <input
                              type="text"
                              disabled={inputsLocked}
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
                  disabled={inputsLocked}
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
                  disabled={inputsLocked}
                  value={internalComment}
                  onChange={(e) => setInternalComment(e.target.value)}
                  rows={2}
                  className={`${inputClass} resize-none`}
                />
              </div>
            </div>

            <div className="flex flex-wrap gap-3">
              <button
                type="button"
                onClick={() => void handleToggleFlag()}
                disabled={togglingFlag}
                className={`px-4 py-2.5 rounded-lg text-sm font-medium border disabled:opacity-50 ${
                  isFlagged
                    ? "bg-brand-red/10 text-brand-red border-brand-red/30 hover:bg-brand-red/15"
                    : "border-line hover:bg-secondary text-ink"
                }`}
              >
                {isFlagged ? "Đã gắn cờ" : "Đánh dấu cần xem lại"}
              </button>
            </div>

            {!isReadOnly && (
              <div className="flex flex-wrap gap-3">
                <button
                  type="button"
                  onClick={() => void handleAiSuggest()}
                  disabled={
                    aiSuggesting ||
                    session.aiStatus === "Queued" ||
                    session.aiStatus === "Processing"
                  }
                  className="px-4 py-2.5 border border-brand-red/40 text-brand-red hover:bg-brand-red/5 disabled:opacity-50 rounded-lg text-sm font-medium"
                >
                  {aiSuggesting ||
                  session.aiStatus === "Queued" ||
                  session.aiStatus === "Processing"
                    ? "AI đang xử lý..."
                    : "Gợi ý AI"}
                </button>
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

            {isReadOnly && (
              <div className="space-y-3">
                {!overrideMode ? (
                  <div className="flex flex-wrap gap-3">
                    <button
                      type="button"
                      onClick={handleStartOverride}
                      className="px-4 py-2.5 border border-brand-red/40 text-brand-red hover:bg-brand-red/5 rounded-lg text-sm font-medium"
                    >
                      Sửa điểm (Override)
                    </button>
                    <button
                      type="button"
                      onClick={toggleAuditLog}
                      className="px-4 py-2.5 border border-line hover:bg-secondary rounded-lg text-sm font-medium text-ink"
                    >
                      {showAuditLog ? "Ẩn lịch sử chỉnh sửa" : "Xem lịch sử chỉnh sửa"}
                    </button>
                  </div>
                ) : (
                  <div className="p-4 border border-brand-red/30 bg-brand-red/5 rounded-lg space-y-3">
                    <p className="text-sm font-medium text-ink">
                      Đang sửa điểm bài đã nộp — bắt buộc nhập lý do
                    </p>
                    <div>
                      <label className="text-xs text-ink-soft mb-1 block">
                        Lý do override <span className="text-destructive">*</span>
                      </label>
                      <textarea
                        value={overrideReason}
                        onChange={(e) => setOverrideReason(e.target.value)}
                        rows={2}
                        placeholder="Vd: Học sinh khiếu nại, chấm lại theo yêu cầu phòng đào tạo"
                        className={`${inputClass} resize-none`}
                      />
                    </div>
                    {overrideError && (
                      <p className="text-sm text-destructive">{overrideError}</p>
                    )}
                    <div className="flex flex-wrap gap-3">
                      <button
                        type="button"
                        onClick={handleCancelOverride}
                        disabled={overriding}
                        className="px-4 py-2.5 border border-line hover:bg-secondary rounded-lg text-sm font-medium text-ink disabled:opacity-50"
                      >
                        Huỷ
                      </button>
                      <button
                        type="button"
                        onClick={() => void handleConfirmOverride()}
                        disabled={overriding || overrideReason.trim() === ""}
                        className="flex-1 min-w-[160px] px-4 py-2.5 bg-brand-red text-white hover:bg-brand-red/90 disabled:opacity-50 rounded-lg text-sm font-medium"
                      >
                        {overriding ? "Đang override..." : "Xác nhận override"}
                      </button>
                    </div>
                  </div>
                )}

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
              </div>
            )}
          </PaperCard>
        </div>
      </div>
    </div>
  );
}
