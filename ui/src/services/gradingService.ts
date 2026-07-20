import api from "@/lib/api";
import type { ApiResponse } from "@/types/auth";
import type {
  AuditLogEntry,
  CreateMarkerAssignmentPayload,
  GradingQueuePage,
  GradingSession,
  MarkerAssignment,
  MyProgress,
  OverrideMarksPayload,
  OverrideMarksResult,
  ReassignMarkerAssignmentPayload,
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

  async recordHeartbeat(assignmentId: string, seconds: number): Promise<void> {
    await api.post<ApiResponse<null>>(`/grading/sessions/${assignmentId}/heartbeat`, {
      seconds,
    });
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

  async listMarkerAssignments(subjectId: string): Promise<MarkerAssignment[]> {
    const res = await api.get<ApiResponse<MarkerAssignment[]>>(
      `/grading/subjects/${subjectId}/marker-assignments`
    );
    return res.data.data;
  },

  async createMarkerAssignment(
    subjectId: string,
    payload: CreateMarkerAssignmentPayload
  ): Promise<MarkerAssignment> {
    const res = await api.post<ApiResponse<MarkerAssignment>>(
      `/grading/subjects/${subjectId}/marker-assignments`,
      payload
    );
    return res.data.data;
  },

  async reassignMarkerAssignment(
    id: string,
    payload: ReassignMarkerAssignmentPayload
  ): Promise<MarkerAssignment> {
    const res = await api.put<ApiResponse<MarkerAssignment>>(
      `/grading/marker-assignments/${id}`,
      payload
    );
    return res.data.data;
  },

  async deleteMarkerAssignment(id: string): Promise<void> {
    await api.delete<ApiResponse<null>>(`/grading/marker-assignments/${id}`);
  },

  async listQueue(params: {
    status?: string;
    flagged?: boolean;
    alias?: string;
    page: number;
    pageSize: number;
  }): Promise<GradingQueuePage> {
    const res = await api.get<ApiResponse<GradingQueuePage>>("/grading/queue", {
      params,
    });
    return res.data.data;
  },

  async setFlag(assignmentId: string, isFlagged: boolean): Promise<void> {
    await api.put<ApiResponse<null>>(`/grading/sessions/${assignmentId}/flag`, {
      isFlagged,
    });
  },
};

