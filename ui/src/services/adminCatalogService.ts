import api from "@/lib/api";
import type { ApiResponse } from "@/types/auth";
import type {
  Semester,
  Exam,
  SubjectDetail,
  QuestionInput,
  ExtractGridResult,
  ReplaceQuestionsResult,
  ScoreGridTemplateSummary,
  ScoreGridTemplateDetail,
} from "@/types/catalog";

export const adminCatalogService = {
  async createSemester(payload: {
    code: string;
    name: string;
    description?: string;
    startDate: string;
    endDate: string;
    isActive: boolean;
  }): Promise<Semester> {
    const res = await api.post<ApiResponse<Semester>>("/semesters", payload);
    return res.data.data;
  },

  async updateSemester(
    id: string,
    payload: {
      code: string;
      name: string;
      description?: string;
      startDate: string;
      endDate: string;
      isActive: boolean;
    }
  ): Promise<Semester> {
    const res = await api.put<ApiResponse<Semester>>(`/semesters/${id}`, payload);
    return res.data.data;
  },

  async deleteSemester(id: string): Promise<void> {
    await api.delete(`/semesters/${id}`);
  },

  async createExam(payload: {
    semesterId: string;
    name: string;
    examType: string;
    startDate?: string | null;
    endDate?: string | null;
  }): Promise<Exam> {
    const res = await api.post<ApiResponse<Exam>>("/exams", payload);
    return res.data.data;
  },

  async updateExam(
    id: string,
    payload: {
      name: string;
      examType: string;
      startDate?: string | null;
      endDate?: string | null;
    }
  ): Promise<Exam> {
    const res = await api.put<ApiResponse<Exam>>(`/exams/${id}`, payload);
    return res.data.data;
  },

  async createSubject(payload: {
    examId: string;
    subjectCode: string;
    title?: string;
    maxScore: number;
    status?: string;
    passScore?: number | null;
  }): Promise<SubjectDetail> {
    const res = await api.post<ApiResponse<SubjectDetail>>("/subjects", payload);
    return res.data.data;
  },

  async updateSubject(
    id: string,
    payload: {
      subjectCode: string;
      title?: string;
      maxScore: number;
      status?: string;
      passScore?: number | null;
      gradingDeadline?: string | null;
    }
  ): Promise<SubjectDetail> {
    const res = await api.put<ApiResponse<SubjectDetail>>(`/subjects/${id}`, payload);
    return res.data.data;
  },

  async uploadExamPaper(subjectId: string, file: File): Promise<void> {
    const form = new FormData();
    form.append("file", file);
    await api.post(`/subjects/${subjectId}/exam-paper`, form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },

  async uploadRubric(
    subjectId: string,
    file: File
  ): Promise<{ rubricVersion: number; fileName: string }> {
    const form = new FormData();
    form.append("file", file);
    const res = await api.post<
      ApiResponse<{ rubricVersion: number; fileName: string; contentType: string }>
    >(`/subjects/${subjectId}/rubric`, form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
    return res.data.data;
  },

  /** Best-effort compile of barem → grading contract (auto-approved). */
  async recompileGradingContract(subjectId: string): Promise<void> {
    await api.post(`/admin/subjects/${subjectId}/grading-contracts/recompile`);
  },

  async extractRubricGrid(subjectId: string): Promise<ExtractGridResult> {
    const res = await api.post<ApiResponse<ExtractGridResult>>(
      `/subjects/${subjectId}/rubric/extract-grid`
    );
    return res.data.data;
  },

  async saveQuestions(
    subjectId: string,
    questions: QuestionInput[]
  ): Promise<ReplaceQuestionsResult> {
    const res = await api.put<ApiResponse<ReplaceQuestionsResult>>(
      `/subjects/${subjectId}/questions`,
      { questions }
    );
    return res.data.data;
  },

  async listScoreGridTemplates(): Promise<ScoreGridTemplateSummary[]> {
    const res = await api.get<ApiResponse<ScoreGridTemplateSummary[]>>(
      "/score-grid-templates"
    );
    return res.data.data;
  },

  async getScoreGridTemplate(id: string): Promise<ScoreGridTemplateDetail> {
    const res = await api.get<ApiResponse<ScoreGridTemplateDetail>>(
      `/score-grid-templates/${id}`
    );
    return res.data.data;
  },

  async createScoreGridTemplate(
    name: string,
    questions: QuestionInput[]
  ): Promise<ScoreGridTemplateSummary> {
    const res = await api.post<ApiResponse<ScoreGridTemplateSummary>>(
      "/score-grid-templates",
      { name, questions }
    );
    return res.data.data;
  },

  async deleteScoreGridTemplate(id: string): Promise<void> {
    await api.delete(`/score-grid-templates/${id}`);
  },
};
