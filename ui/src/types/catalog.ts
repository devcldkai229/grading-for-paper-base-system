// Types mapping ExamCatalogService DTOs

export interface Semester {
  id: string;
  code: string;
  name: string;
  description: string | null;
  startDate: string;
  endDate: string;
  isActive: boolean;
  createdAt: string;
}

export interface Exam {
  id: string;
  semesterId: string;
  name: string;
  examType: string;
  startDate: string | null;
  endDate: string | null;
  subjectCount: number;
  createdAt: string;
}

export interface SubjectSummary {
  id: string;
  examId: string;
  subjectCode: string;
  title: string | null;
  maxScore: number;
  passScore: number | null;
  status: string;
  hasExamPaper: boolean;
  hasRubric: boolean;
  questionCount: number;
  createdAt: string;
}

export interface SubjectSearchResult {
  id: string;
  examId: string;
  examName: string;
  semesterId: string;
  semesterCode: string;
  subjectCode: string;
  title: string | null;
  maxScore: number;
  passScore: number | null;
  status: string;
  hasExamPaper: boolean;
  hasRubric: boolean;
  questionCount: number;
  createdAt: string;
}

export interface Question {
  id: string;
  questionNumber: string;
  groupLabel: string | null;
  label: string | null;
  maxScore: number;
  orderIndex: number;
}

export interface QuestionInput {
  groupLabel: string | null;
  questionNumber: string;
  label: string | null;
  maxScore: number;
  orderIndex: number;
}

export interface ExtractedQuestion {
  groupLabel: string | null;
  questionNumber: string;
  label: string | null;
  maxScore: number;
  confidence: number;
}

export interface ExtractGridResult {
  questions: ExtractedQuestion[];
  totalMax: number;
  warnings: string[];
}

export interface ReplaceQuestionsResult {
  questions: Question[];
  warnings: string[];
}

export interface SubjectDetail {
  id: string;
  examId: string;
  subjectCode: string;
  title: string | null;
  maxScore: number;
  passScore: number | null;
  status: string;
  hasExamPaper: boolean;
  examPaperFileName: string | null;
  examPaperContentType: string | null;
  hasRubric: boolean;
  rubricFileName: string | null;
  rubricContentType: string | null;
  rubricVersion: number;
  questions: Question[];
  createdAt: string;
}

export interface FileUrlResponse {
  url: string;
  contentType: string;
  fileName: string;
  rubricVersion?: number;
  hasOriginalDownload?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}
