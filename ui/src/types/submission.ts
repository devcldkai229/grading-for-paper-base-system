// Types mapping SubmissionService DTOs

export interface BatchStatus {
  id: string;
  status: string;
  totalPapers: number;
  errorMessage: string | null;
}

export interface StudentPaper {
  id: string;
  batchId: string;
  subjectId: string;
  studentAlias: string | null;
  aliasNumber: number | null;
  status: string;
  fileCount: number;
  createdAt: string;
}

export interface PaperFile {
  id: string;
  fileName: string | null;
  contentType: string;
  sizeBytes: number | null;
  orderIndex: number;
}

export interface StudentPaperDetail {
  id: string;
  batchId: string;
  subjectId: string;
  studentAlias: string | null;
  aliasNumber: number | null;
  status: string;
  files: PaperFile[];
  createdAt: string;
}

export interface FileUrlResponse {
  url: string;
  contentType: string;
  fileName: string;
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
