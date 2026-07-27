import axios from "axios";
import { authService } from "@/services/authService";

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5016/api";

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    "Content-Type": "application/json",
  },
});

// Single-flight refresh: concurrent 401s share one refresh call (refresh tokens are one-use).
let refreshPromise: Promise<string> | null = null;

async function refreshAccessToken(): Promise<string> {
  if (!refreshPromise) {
    refreshPromise = (async () => {
      const accessToken = authService.getAccessToken();
      const refreshToken = authService.getRefreshToken();
      if (!accessToken || !refreshToken) {
        throw new Error("No tokens available");
      }

      const response = await axios.post(`${API_BASE_URL}/auth/refresh`, {
        accessToken,
        refreshToken,
      });

      const result = response.data.data;
      if (!result?.success || !result.accessToken || !result.refreshToken) {
        throw new Error(result?.errors?.join?.(", ") ?? "Refresh failed");
      }

      authService.saveTokens(
        result.accessToken,
        result.refreshToken,
        result.expiresAt
      );
      return result.accessToken as string;
    })().finally(() => {
      refreshPromise = null;
    });
  }
  return refreshPromise;
}

function redirectToLogin() {
  authService.clearTokens();
  if (!window.location.pathname.startsWith("/login")) {
    window.location.href = "/login";
  }
}

// Request interceptor: attach Bearer token
api.interceptors.request.use(
  (config) => {
    const token = authService.getAccessToken();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Response interceptor: auto-refresh on 401
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (
      error.response?.status === 401 &&
      originalRequest &&
      !originalRequest._retry &&
      authService.getRefreshToken()
    ) {
      originalRequest._retry = true;

      try {
        const newAccessToken = await refreshAccessToken();
        originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;
        return api(originalRequest);
      } catch {
        redirectToLogin();
        return Promise.reject(error);
      }
    }

    if (error.response?.status === 401 && !authService.getRefreshToken()) {
      redirectToLogin();
    }

    return Promise.reject(error);
  }
);

export default api;
