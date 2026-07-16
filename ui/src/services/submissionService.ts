import api from "@/lib/api";
import type { ApiResponse } from "@/types/auth";
import type {
  BatchStatus,
  StudentPaper,
  StudentPaperDetail,
  FileUrlResponse,
  PagedResult,
} from "@/types/submission";

export const submissionService = {
  async uploadBatch(subjectId: string, file: File): Promise<{ batchId: string }> {
    const formData = new FormData();
    formData.append("subjectId", subjectId);
    formData.append("file", file);
    const res = await api.post<ApiResponse<{ batchId: string }>>(
      "/batches",
      formData,
      { headers: { "Content-Type": "multipart/form-data" } }
    );
    return res.data.data;
  },

  async uploadFiles(
    subjectId: string,
    files: File[]
  ): Promise<{ batchId: string; paperId: string }> {
    const formData = new FormData();
    formData.append("subjectId", subjectId);
    files.forEach((file) => formData.append("files", file));
    const res = await api.post<ApiResponse<{ batchId: string; paperId: string }>>(
      "/batches/files",
      formData,
      { headers: { "Content-Type": "multipart/form-data" } }
    );
    return res.data.data;
  },

  async getBatchStatus(batchId: string): Promise<BatchStatus> {
    const res = await api.get<ApiResponse<BatchStatus>>(
      `/batches/${batchId}`
    );
    return res.data.data;
  },

  async retryBatch(batchId: string): Promise<{ batchId: string }> {
    const res = await api.post<ApiResponse<{ batchId: string }>>(
      `/batches/${batchId}/retry`
    );
    return res.data.data;
  },

  async getSubmissions(
    subjectId: string,
    status?: string,
    page = 1,
    pageSize = 20,
    aliasStart?: number,
    aliasEnd?: number
  ): Promise<PagedResult<StudentPaper>> {
    const params: Record<string, string | number> = { subjectId, page, pageSize };
    if (status) params.status = status;
    if (aliasStart !== undefined) params.aliasStart = aliasStart;
    if (aliasEnd !== undefined) params.aliasEnd = aliasEnd;
    const res = await api.get<ApiResponse<PagedResult<StudentPaper>>>(
      "/submissions",
      { params }
    );
    return res.data.data;
  },

  async searchPapers(
    keyword: string,
    page = 1,
    pageSize = 8
  ): Promise<PagedResult<StudentPaper>> {
    const res = await api.get<ApiResponse<PagedResult<StudentPaper>>>(
      "/submissions/search",
      { params: { keyword, page, pageSize } }
    );
    return res.data.data;
  },

  async getSubmissionDetail(paperId: string): Promise<StudentPaperDetail> {
    const res = await api.get<ApiResponse<StudentPaperDetail>>(
      `/submissions/${paperId}`
    );
    return res.data.data;
  },

  async getFileUrl(paperId: string, fileId: string): Promise<FileUrlResponse> {
    const res = await api.get<ApiResponse<FileUrlResponse>>(
      `/submissions/${paperId}/files/${fileId}/url`
    );
    return res.data.data;
  },

  async deleteBatch(batchId: string): Promise<void> {
    await api.delete<ApiResponse<null>>(`/batches/${batchId}`);
  },

  async deleteSubmission(paperId: string): Promise<void> {
    await api.delete<ApiResponse<null>>(`/submissions/${paperId}`);
  },
};
