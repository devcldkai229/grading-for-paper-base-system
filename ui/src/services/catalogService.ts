import api from "@/lib/api";
import type { ApiResponse } from "@/types/auth";
import type {
  Semester,
  Exam,
  SubjectSummary,
  SubjectSearchResult,
  SubjectDetail,
  FileUrlResponse,
  PagedResult,
} from "@/types/catalog";

export interface SubjectSearchParams {
  code?: string;
  semesterId?: string;
  examId?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export const catalogService = {
  async getSemesters(
    active?: boolean,
    page = 1,
    pageSize = 10
  ): Promise<PagedResult<Semester>> {
    const params: Record<string, string | number> = { page, pageSize };
    if (active !== undefined) params.active = String(active);
    const res = await api.get<ApiResponse<PagedResult<Semester>>>(
      "/semesters",
      { params }
    );
    return res.data.data;
  },

  async getSemester(semesterId: string): Promise<Semester> {
    const res = await api.get<ApiResponse<Semester>>(`/semesters/${semesterId}`);
    return res.data.data;
  },

  async getExamsBySemester(
    semesterId: string,
    page = 1,
    pageSize = 10
  ): Promise<PagedResult<Exam>> {
    const res = await api.get<ApiResponse<PagedResult<Exam>>>(
      `/semesters/${semesterId}/exams`,
      { params: { page, pageSize } }
    );
    return res.data.data;
  },

  async getExam(examId: string): Promise<Exam> {
    const res = await api.get<ApiResponse<Exam>>(`/exams/${examId}`);
    return res.data.data;
  },

  async getSubjectsByExam(
    examId: string,
    page = 1,
    pageSize = 10
  ): Promise<PagedResult<SubjectSummary>> {
    const res = await api.get<ApiResponse<PagedResult<SubjectSummary>>>(
      `/exams/${examId}/subjects`,
      { params: { page, pageSize } }
    );
    return res.data.data;
  },

  async searchSubjects(
    params: SubjectSearchParams
  ): Promise<PagedResult<SubjectSearchResult>> {
    const { page = 1, pageSize = 10, code, semesterId, examId, status } = params;
    const query: Record<string, string | number> = { page, pageSize };
    if (code) query.code = code;
    if (semesterId) query.semesterId = semesterId;
    if (examId) query.examId = examId;
    if (status) query.status = status;
    const res = await api.get<ApiResponse<PagedResult<SubjectSearchResult>>>(
      "/subjects",
      { params: query }
    );
    return res.data.data;
  },

  async getSubjectDetail(subjectId: string): Promise<SubjectDetail> {
    const res = await api.get<ApiResponse<SubjectDetail>>(
      `/subjects/${subjectId}`
    );
    return res.data.data;
  },

  async getExamPaperUrl(subjectId: string): Promise<FileUrlResponse> {
    const res = await api.get<ApiResponse<FileUrlResponse>>(
      `/subjects/${subjectId}/exam-paper/url`
    );
    return res.data.data;
  },

  async getRubricUrl(subjectId: string): Promise<FileUrlResponse> {
    const res = await api.get<ApiResponse<FileUrlResponse>>(
      `/subjects/${subjectId}/rubric/url`
    );
    return res.data.data;
  },

  async getExamPaperOriginalUrl(subjectId: string): Promise<FileUrlResponse> {
    const res = await api.get<ApiResponse<FileUrlResponse>>(
      `/subjects/${subjectId}/exam-paper/original`
    );
    return res.data.data;
  },

  async getRubricOriginalUrl(subjectId: string): Promise<FileUrlResponse> {
    const res = await api.get<ApiResponse<FileUrlResponse>>(
      `/subjects/${subjectId}/rubric/original`
    );
    return res.data.data;
  },
};
