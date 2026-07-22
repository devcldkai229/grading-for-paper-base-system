export interface AssignmentSummary {
  assignmentId: string;
  paperId: string;
  aliasNumber: number | null;
  status: string;
}

export interface StartBatchResult {
  firstAssignmentId: string;
  assignmentIds: string[];
  subjectId: string;
  batchId: string;
  assignments: AssignmentSummary[];
}

export interface GradingQuestionMark {
  questionNumber: string;
  groupLabel: string | null;
  label: string | null;
  maxScore: number;
  score: number | null;
  questionComment: string;
  orderIndex: number;
  aiDrafted?: boolean;
  aiReviewStatus?: string | null;
}

export interface AiSuggestion {
  questionNumber: string;
  score: number;
  questionComment: string | null;
  confidence: number;
  isManualOnly: boolean;
}

export interface GradingSession {
  assignmentId: string;
  studentPaperId: string;
  subjectId: string;
  batchId: string;
  subjectMaxScore: number;
  rubricVersion: number;
  status: string;
  aiStatus?: string;
  rowVersion: number;
  paperComment: string;
  internalComment: string;
  studentAlias: string | null;
  aliasNumber: number | null;
  questions: GradingQuestionMark[];
  isFlagged: boolean;
  aiSuggestions: AiSuggestion[] | null;
  aiPaperComment: string | null;
}

export interface SaveMarksPayload {
  rowVersion: number;
  paperComment: string;
  internalComment: string;
  questions: {
    questionNumber: string;
    score: number;
    questionComment: string;
  }[];
}

export interface SaveMarksResult {
  rowVersion: number;
}

export interface SubmitResult {
  nextAssignmentId: string | null;
}

export interface OverrideMarksPayload {
  reason: string;
  paperComment: string;
  internalComment: string;
  questions: {
    questionNumber: string;
    score: number;
    questionComment: string;
  }[];
}

export interface OverrideMarksResult {
  rowVersion: number;
  totalScore: number;
}

export interface AuditLogEntry {
  id: string;
  userId: string;
  action: string;
  oldValue: string | null;
  newValue: string | null;
  reason: string | null;
  createdAt: string;
}

export interface UpcomingDeadline {
  subjectId: string;
  subjectCode: string;
  examName: string;
  deadline: string;
  totalCount: number;
  submittedCount: number;
  remainingCount: number;
  nextAssignmentId: string | null;
}

export interface MyProgress {
  totalAssignments: number;
  submittedCount: number;
  remainingCount: number;
  nextAssignmentId: string | null;
  upcomingDeadlines: UpcomingDeadline[];
}

export interface MarkerAssignment {
  id: string;
  subjectId: string;
  teacherId: string;
  aliasStart: number;
  aliasEnd: number;
  assignedBy: string;
  assignedAt: string;
  batchId?: string | null;
  zipFileName?: string | null;
}

export interface CreateFolderAssignmentPayload {
  teacherId: string;
  batchId: string;
  zipFileName?: string;
}

export interface CreateMarkerAssignmentPayload {
  teacherId: string;
  aliasStart?: number;
  aliasEnd?: number;
  quota?: number;
}

export interface ReassignMarkerAssignmentPayload {
  teacherId: string;
  aliasStart: number;
  aliasEnd: number;
}

export interface MarkerAssignmentResult {
  assignment: MarkerAssignment;
  materializedCount: number;
  skippedInProgressCount: number;
  warnings: string[];
}

export interface GradingQueueRow {
  assignmentId: string;
  studentAlias: string | null;
  aliasNumber: number | null;
  subjectId: string;
  status: string;
  isFlagged: boolean;
  totalScore: number | null;
  submittedAt: string | null;
  batchId?: string | null;
  zipFileName?: string | null;
}

export interface GradingQueueFolder {
  batchId: string;
  zipFileName: string | null;
  subjectId: string;
  totalPapers: number;
  notStarted: number;
  drafting: number;
  submitted: number;
  assignedAt: string;
}

export interface GradingQueuePage {
  items: GradingQueueRow[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
