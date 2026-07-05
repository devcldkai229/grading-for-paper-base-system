import api from "@/lib/api";
import type { ApiResponse, AuthResult, JwtPayload } from "@/types/auth";

const TOKEN_KEY = "access_token";
const REFRESH_KEY = "refresh_token";
const EXPIRES_KEY = "token_expires_at";
const REMEMBER_KEY = "remember_me";

function getStorage(): Storage {
  const remember = localStorage.getItem(REMEMBER_KEY) === "true";
  return remember ? localStorage : sessionStorage;
}

export const authService = {
  // ---- API calls ----

  async login(
    email: string,
    password: string,
    rememberMe: boolean
  ): Promise<AuthResult> {
    // Store preference before saving tokens
    localStorage.setItem(REMEMBER_KEY, String(rememberMe));

    const response = await api.post<ApiResponse<AuthResult>>("/auth/login", {
      email,
      password,
    });

    const result = response.data.data;
    if (result.success && result.accessToken && result.refreshToken) {
      this.saveTokens(result.accessToken, result.refreshToken, result.expiresAt);
    }
    return result;
  },

  async googleLogin(idToken: string): Promise<AuthResult> {
    // Google login always remembers
    localStorage.setItem(REMEMBER_KEY, "true");

    const response = await api.post<ApiResponse<AuthResult>>("/auth/google", {
      idToken,
    });

    const result = response.data.data;
    if (result.success && result.accessToken && result.refreshToken) {
      this.saveTokens(result.accessToken, result.refreshToken, result.expiresAt);
    }
    return result;
  },

  async logout(): Promise<void> {
    try {
      await api.post("/auth/logout");
    } catch {
      // Even if API fails, still clear local tokens
    }
    this.clearTokens();
  },

  async changePassword(currentPassword: string, newPassword: string): Promise<AuthResult> {
    const response = await api.post<ApiResponse<AuthResult>>("/auth/change-password", {
      currentPassword,
      newPassword,
    });
    return response.data.data;
  },

  // ---- Token management ----

  saveTokens(
    accessToken: string,
    refreshToken: string,
    expiresAt: string | null
  ): void {
    const storage = getStorage();
    storage.setItem(TOKEN_KEY, accessToken);
    storage.setItem(REFRESH_KEY, refreshToken);
    if (expiresAt) {
      storage.setItem(EXPIRES_KEY, expiresAt);
    }
  },

  getAccessToken(): string | null {
    return (
      localStorage.getItem(TOKEN_KEY) || sessionStorage.getItem(TOKEN_KEY)
    );
  },

  getRefreshToken(): string | null {
    return (
      localStorage.getItem(REFRESH_KEY) || sessionStorage.getItem(REFRESH_KEY)
    );
  },

  clearTokens(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(EXPIRES_KEY);
    localStorage.removeItem(REMEMBER_KEY);
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(REFRESH_KEY);
    sessionStorage.removeItem(EXPIRES_KEY);
  },

  isAuthenticated(): boolean {
    return !!this.getAccessToken();
  },

  // ---- JWT decode ----

  decodeToken(): JwtPayload | null {
    const token = this.getAccessToken();
    if (!token) return null;

    try {
      const base64Url = token.split(".")[1];
      const base64 = base64Url.replace(/-/g, "+").replace(/_/g, "/");
      const jsonPayload = decodeURIComponent(
        atob(base64)
          .split("")
          .map((c) => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2))
          .join("")
      );
      const raw = JSON.parse(jsonPayload) as Record<string, unknown>;
      return {
        sub: String(raw.sub ?? ""),
        email: String(raw.email ?? raw.Email ?? ""),
        name: raw.name ? String(raw.name) : raw.Name ? String(raw.Name) : undefined,
        role: String(raw.role ?? raw.Role ?? ""),
        jti: String(raw.jti ?? ""),
        exp: Number(raw.exp ?? 0),
        iss: String(raw.iss ?? ""),
        aud: String(raw.aud ?? ""),
      };
    } catch {
      return null;
    }
  },
};
