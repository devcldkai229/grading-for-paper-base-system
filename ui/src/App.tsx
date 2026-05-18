import { Routes, Route, Navigate } from "react-router-dom";
import { GoogleOAuthProvider } from "@react-oauth/google";
import { LoginPage } from "@/features/auth/pages/LoginPage";
import { DashboardPage } from "@/features/auth/pages/DashboardPage";

const GOOGLE_CLIENT_ID =
  "757455269498-qnor5filnkt0njp5l2piu6pcqcphjf19.apps.googleusercontent.com";

function App() {
  return (
    <GoogleOAuthProvider clientId={GOOGLE_CLIENT_ID}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </GoogleOAuthProvider>
  );
}

export default App;
