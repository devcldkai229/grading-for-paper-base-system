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
}

export interface GradingSession {
  assignmentId: string;
  studentPaperId: string;
  subjectId: string;
  batchId: string;
  subjectMaxScore: number;
  rubricVersion: number;
  status: string;
  rowVersion: number;
  paperComment: string;
  internalComment: string;
  studentAlias: string | null;
  aliasNumber: number | null;
  questions: GradingQuestionMark[];
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
