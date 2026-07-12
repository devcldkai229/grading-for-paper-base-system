// Types mapping ReportingService grading-progress dashboard DTOs

export interface LecturerProgress {
  teacherId: string;
  assignedCount: number;
  completedCount: number;
  draftingCount: number;
  notStartedCount: number;
  avgScore: number | null;
  throughputPerHour: number | null;
  lastActivityAt: string | null;
  estimatedFinish: string | null;
}

export interface SubjectGradingProgress {
  subjectId: string;
  subjectCode: string;
  title: string | null;
  examName: string;
  semesterCode: string;
  totalPapers: number;
  completedPapers: number;
  draftingPapers: number;
  notStartedPapers: number;
  completionPercent: number;
  scoreAvg: number | null;
  scoreMin: number | null;
  scoreMax: number | null;
  throughputPerHour: number | null;
  estimatedFinish: string | null;
  lecturers: LecturerProgress[];
}

export interface GradingProgressDashboard {
  subjects: SubjectGradingProgress[];
}
