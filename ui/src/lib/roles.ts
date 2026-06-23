export type AppRole = "Admin" | "Lecturer";

export function normalizeRole(role: string | undefined | null): AppRole | null {
  if (!role) return null;
  if (role === "Admin") return "Admin";
  if (role === "Lecturer" || role === "Lecture") return "Lecturer";
  return null;
}

export function isAdmin(role: string | undefined | null): boolean {
  return normalizeRole(role) === "Admin";
}

export function isLecturer(role: string | undefined | null): boolean {
  return normalizeRole(role) === "Lecturer";
}

export function getHomePath(role: string | undefined | null): string {
  return isAdmin(role) ? "/admin/dashboard" : "/dashboard";
}

const ADMIN_PREFIX = "/admin";
const LECTURER_PREFIXES = ["/dashboard", "/catalog", "/batches", "/submissions", "/grading"];

export function isPathAllowedForRole(path: string, role: string | undefined | null): boolean {
  const r = normalizeRole(role);
  if (!r) return false;
  if (path.startsWith("/login")) return true;
  if (r === "Admin") {
    return path.startsWith(ADMIN_PREFIX) || path.startsWith("/grading");
  }
  return LECTURER_PREFIXES.some((p) => path === p || path.startsWith(`${p}/`));
}

export function resolvePostLoginPath(
  from: string | undefined,
  role: string | undefined | null
): string {
  const home = getHomePath(role);
  if (!from || from === "/login" || from === "/") return home;
  if (isPathAllowedForRole(from, role)) return from;
  return home;
}
