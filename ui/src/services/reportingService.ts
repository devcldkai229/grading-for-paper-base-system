import { isAxiosError } from "axios";
import api from "@/lib/api";
import type { ApiResponse } from "@/types/auth";
import type { GradingProgressDashboard } from "@/types/reporting";

export const reportingService = {
  async getGradingProgressDashboard(
    semesterId?: string,
    examId?: string
  ): Promise<GradingProgressDashboard> {
    const params: Record<string, string> = {};
    if (semesterId) params.semesterId = semesterId;
    if (examId) params.examId = examId;

    const res = await api.get<ApiResponse<GradingProgressDashboard>>(
      "/reports/grading-progress",
      { params }
    );
    return res.data.data;
  },

  async exportFeedback(subjectId: string, mappingFile: File): Promise<Blob> {
    const formData = new FormData();
    formData.append("aliasMapping", mappingFile);

    try {
      const res = await api.post<Blob>(
        `/reports/subjects/${subjectId}/feedback-export`,
        formData,
        { responseType: "blob" }
      );
      return res.data;
    } catch (err) {
      if (isAxiosError(err) && err.response?.data instanceof Blob) {
        const text = await err.response.data.text();
        let message = "Xuất báo cáo thất bại";
        try {
          const parsed = JSON.parse(text) as { message?: string };
          if (parsed.message) message = parsed.message;
        } catch {
          // Response wasn't JSON (e.g. validation error) — keep the default message.
        }
        throw new Error(message, { cause: err });
      }
      throw err;
    }
  },
};
