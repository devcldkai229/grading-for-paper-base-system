import { Routes, Route, Navigate } from "react-router-dom";
import { GoogleOAuthProvider } from "@react-oauth/google";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { RoleRoute } from "@/components/auth/RoleRoute";
import { AppLayout } from "@/components/layout/AppLayout";
import { AdminLayout } from "@/components/layout/AdminLayout";
import { LoginPage } from "@/features/auth/pages/LoginPage";
import { DashboardPage } from "@/features/auth/pages/DashboardPage";
import { AdminDashboardPage } from "@/features/admin/pages/AdminDashboardPage";
import { AdminUsersPage } from "@/features/admin/pages/AdminUsersPage";
import { SemestersPage } from "@/features/catalog/pages/SemestersPage";
import { ExamsPage } from "@/features/catalog/pages/ExamsPage";
import { SubjectsPage } from "@/features/catalog/pages/SubjectsPage";
import { SubjectSearchPage } from "@/features/catalog/pages/SubjectSearchPage";
import { SubjectDetailPage } from "@/features/catalog/pages/SubjectDetailPage";
import { AdminSubjectDetailPage } from "@/features/admin/pages/AdminSubjectDetailPage";
import { BatchUploadPage } from "@/features/submission/pages/BatchUploadPage";
import { SubmissionsPage } from "@/features/submission/pages/SubmissionsPage";
import { SubmissionDetailPage } from "@/features/submission/pages/SubmissionDetailPage";
import { GradingPage } from "@/features/grading/pages/GradingPage";
import { ProfilePage } from "@/features/auth/pages/ProfilePage";
import { ForgotPasswordPage } from "@/features/auth/pages/ForgotPasswordPage";
import { ResetPasswordPage } from "@/features/auth/pages/ResetPasswordPage";
import { AdminAuditLogsPage } from "@/features/admin/pages/AdminAuditLogsPage";
import { AdminOverridePage } from "@/features/admin/pages/AdminOverridePage";
import { AdminFeedbackExportPage } from "@/features/admin/pages/AdminFeedbackExportPage";
import { authService } from "@/services/authService";
import { getHomePath } from "@/lib/roles";

const GOOGLE_CLIENT_ID =
  "757455269498-qnor5filnkt0njp5l2piu6pcqcphjf19.apps.googleusercontent.com";

function HomeRedirect() {
  const role = authService.decodeToken()?.role;
  return (
    <Navigate
      to={authService.isAuthenticated() ? getHomePath(role) : "/login"}
      replace
    />
  );
}

function App() {
  return (
    <GoogleOAuthProvider clientId={GOOGLE_CLIENT_ID}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route path="/" element={<HomeRedirect />} />

        <Route element={<ProtectedRoute />}>
          <Route element={<RoleRoute allowed="admin" />}>
            <Route element={<AdminLayout />}>
              <Route path="/admin/dashboard" element={<AdminDashboardPage />} />
              <Route path="/admin/users" element={<AdminUsersPage />} />
              <Route path="/admin/audit-logs" element={<AdminAuditLogsPage />} />
              <Route path="/admin/grading/override" element={<AdminOverridePage />} />
              <Route path="/admin/reports/feedback-export" element={<AdminFeedbackExportPage />} />
              <Route path="/admin/catalog/semesters" element={<SemestersPage />} />
              <Route
                path="/admin/catalog/semesters/:semesterId/exams"
                element={<ExamsPage />}
              />
              <Route
                path="/admin/catalog/subjects/search"
                element={<SubjectSearchPage />}
              />
              <Route
                path="/admin/catalog/semesters/:semesterId/exams/:examId/subjects"
                element={<SubjectsPage />}
              />
              <Route
                path="/admin/catalog/exams/:examId/subjects"
                element={<SubjectsPage />}
              />
              <Route
                path="/admin/catalog/subjects/:subjectId"
                element={<AdminSubjectDetailPage />}
              />
              <Route path="/admin/profile" element={<ProfilePage />} />
            </Route>
          </Route>

          <Route element={<RoleRoute allowed="lecturer" />}>
            <Route element={<AppLayout />}>
              <Route path="/dashboard" element={<DashboardPage />} />
              <Route path="/catalog/semesters" element={<SemestersPage />} />
              <Route
                path="/catalog/semesters/:semesterId/exams"
                element={<ExamsPage />}
              />
              <Route
                path="/catalog/subjects/search"
                element={<SubjectSearchPage />}
              />
              <Route
                path="/catalog/semesters/:semesterId/exams/:examId/subjects"
                element={<SubjectsPage />}
              />
              <Route
                path="/catalog/exams/:examId/subjects"
                element={<SubjectsPage />}
              />
              <Route
                path="/catalog/subjects/:subjectId"
                element={<SubjectDetailPage />}
              />
              <Route path="/batches/upload" element={<BatchUploadPage />} />
              <Route path="/submissions" element={<SubmissionsPage />} />
              <Route
                path="/submissions/:paperId"
                element={<SubmissionDetailPage />}
              />
              <Route path="/profile" element={<ProfilePage />} />
            </Route>
          </Route>

          <Route path="/grading/:assignmentId" element={<GradingPage />} />
        </Route>

        <Route path="*" element={<HomeRedirect />} />
      </Routes>
    </GoogleOAuthProvider>
  );
}

export default App;
