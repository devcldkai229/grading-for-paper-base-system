import api from "@/lib/api";
import type { ApiResponse } from "@/types/auth";
import type {
  AuditLogEntry,
  GradingSession,
  MyProgress,
  OverrideMarksPayload,
  OverrideMarksResult,
  SaveMarksPayload,
  SaveMarksResult,
  StartBatchResult,
  SubmitResult,
} from "@/types/grading";

export const gradingService = {
  async getMyProgress(): Promise<MyProgress> {
    const res = await api.get<ApiResponse<MyProgress>>("/grading/my-progress");
    return res.data.data;
  },

  async startBatch(batchId: string): Promise<StartBatchResult> {
    const res = await api.post<ApiResponse<StartBatchResult>>(
      `/grading/batches/${batchId}/start`
    );
    return res.data.data;
  },

  async getSession(assignmentId: string): Promise<GradingSession> {
    const res = await api.get<ApiResponse<GradingSession>>(
      `/grading/sessions/${assignmentId}`
    );
    return res.data.data;
  },

  async saveMarks(
    assignmentId: string,
    payload: SaveMarksPayload
  ): Promise<SaveMarksResult> {
    const res = await api.put<ApiResponse<SaveMarksResult>>(
      `/grading/sessions/${assignmentId}/marks`,
      payload
    );
    return res.data.data;
  },

  async submit(assignmentId: string): Promise<SubmitResult> {
    const res = await api.post<ApiResponse<SubmitResult>>(
      `/grading/sessions/${assignmentId}/submit`
    );
    return res.data.data;
  },

  async getAssignmentForOverride(assignmentId: string): Promise<GradingSession> {
    const res = await api.get<ApiResponse<GradingSession>>(
      `/grading/assignments/${assignmentId}`
    );
    return res.data.data;
  },

  async overrideMarks(
    assignmentId: string,
    payload: OverrideMarksPayload
  ): Promise<OverrideMarksResult> {
    const res = await api.put<ApiResponse<OverrideMarksResult>>(
      `/grading/assignments/${assignmentId}/override`,
      payload
    );
    return res.data.data;
  },

  async getAuditLog(assignmentId: string): Promise<AuditLogEntry[]> {
    const res = await api.get<ApiResponse<AuditLogEntry[]>>(
      `/grading/assignments/${assignmentId}/audit-log`
    );
    return res.data.data;
  },

  async exportGrades(subjectId: string): Promise<Blob> {
    const res = await api.get(`/grading/subjects/${subjectId}/export`, {
      responseType: "blob",
    });
    return res.data;
  },
};

