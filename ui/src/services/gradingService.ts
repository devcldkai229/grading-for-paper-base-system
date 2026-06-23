import api from "@/lib/api";
import type { ApiResponse } from "@/types/auth";
import type {
  GradingSession,
  SaveMarksPayload,
  SaveMarksResult,
  StartBatchResult,
  SubmitResult,
} from "@/types/grading";

export const gradingService = {
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
};
