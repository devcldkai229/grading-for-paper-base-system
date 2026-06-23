import { gradingService } from "@/services/gradingService";
import { saveGradingQueue } from "@/lib/gradingQueue";
import type { NavigateFunction } from "react-router-dom";

export async function startGradingFlow(
  batchId: string,
  navigate: NavigateFunction
): Promise<void> {
  const result = await gradingService.startBatch(batchId);
  saveGradingQueue({
    assignmentIds: result.assignmentIds,
    assignments: result.assignments.map((a) => ({
      assignmentId: a.assignmentId,
      aliasNumber: a.aliasNumber,
      status: a.status,
    })),
    subjectId: result.subjectId,
    batchId: result.batchId,
    currentAssignmentId: result.firstAssignmentId,
  });
  navigate(`/grading/${result.firstAssignmentId}`);
}
