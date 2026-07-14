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

export interface ScoreHistogramBucket {
  rangeStart: number;
  rangeEnd: number;
  count: number;
}

export interface SubjectScoreDistribution {
  subjectId: string;
  subjectCode: string;
  title: string | null;
  examName: string;
  semesterCode: string;
  maxScore: number;
  submittedCount: number;
  scoreAvg: number | null;
  scoreMin: number | null;
  scoreMax: number | null;
  histogram: ScoreHistogramBucket[];
}

export interface ScoreDistributionReport {
  subjects: SubjectScoreDistribution[];
}

export interface SubjectPassFail {
  subjectId: string;
  subjectCode: string;
  title: string | null;
  examName: string;
  semesterCode: string;
  maxScore: number;
  passScore: number | null;
  submittedCount: number;
  passCount: number | null;
  failCount: number | null;
  passRatePercent: number | null;
}

export interface PassFailReport {
  subjects: SubjectPassFail[];
}
