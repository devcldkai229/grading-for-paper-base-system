import { authService } from "@/services/authService";

export interface GradingAssignmentRef {
  assignmentId: string;
  aliasNumber: number | null;
  status: string;
}

export interface GradingQueueState {
  assignmentIds: string[];
  assignments: GradingAssignmentRef[];
  subjectId: string;
  batchId: string;
  currentAssignmentId?: string;
}

function storageKey(): string {
  const teacherId = authService.decodeToken()?.sub ?? "anonymous";
  return `grading-queue:${teacherId}`;
}

export function saveGradingQueue(state: GradingQueueState): void {
  localStorage.setItem(storageKey(), JSON.stringify(state));
}

export function loadGradingQueue(): GradingQueueState | null {
  const raw = localStorage.getItem(storageKey());
  if (!raw) return null;
  try {
    return JSON.parse(raw) as GradingQueueState;
  } catch {
    return null;
  }
}

export function clearGradingQueue(): void {
  localStorage.removeItem(storageKey());
}

export function updateQueueCurrentAssignment(assignmentId: string): void {
  const queue = loadGradingQueue();
  if (!queue) return;
  saveGradingQueue({ ...queue, currentAssignmentId: assignmentId });
}

export function updateQueueAssignmentStatus(
  assignmentId: string,
  status: string
): void {
  const queue = loadGradingQueue();
  if (!queue) return;
  saveGradingQueue({
    ...queue,
    assignments: queue.assignments.map((a) =>
      a.assignmentId === assignmentId ? { ...a, status } : a
    ),
  });
}
