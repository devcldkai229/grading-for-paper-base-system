import { Navigate, Outlet } from "react-router-dom";
import { authService } from "@/services/authService";
import { getHomePath, isAdmin, isLecturer } from "@/lib/roles";

interface RoleRouteProps {
  allowed: "admin" | "lecturer";
}

export function RoleRoute({ allowed }: RoleRouteProps) {
  const user = authService.decodeToken();
  const role = user?.role;

  if (allowed === "admin" && !isAdmin(role)) {
    return <Navigate to={getHomePath(role)} replace />;
  }

  if (allowed === "lecturer" && !isLecturer(role)) {
    return <Navigate to={getHomePath(role)} replace />;
  }

  return <Outlet />;
}
