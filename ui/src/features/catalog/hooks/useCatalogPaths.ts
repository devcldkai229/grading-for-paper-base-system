import { useMemo } from "react";
import { useLocation } from "react-router-dom";

export function useCatalogPaths() {
  const location = useLocation();
  const isAdminMode = location.pathname.startsWith("/admin/catalog");
  const base = isAdminMode ? "/admin/catalog" : "/catalog";

  return useMemo(
    () => ({
      isAdminMode,
      base,
      semesters: `${base}/semesters`,
      semesterExams: (semesterId: string) => `${base}/semesters/${semesterId}/exams`,
      examSubjects: (semesterId: string, examId: string) =>
        `${base}/semesters/${semesterId}/exams/${examId}/subjects`,
      examSubjectsShort: (examId: string) => `${base}/exams/${examId}/subjects`,
      subject: (subjectId: string) => `${base}/subjects/${subjectId}`,
    }),
    [base, isAdminMode]
  );
}
