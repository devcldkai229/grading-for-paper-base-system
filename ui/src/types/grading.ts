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
