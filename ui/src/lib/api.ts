import axios from "axios";
import { authService } from "@/services/authService";

const API_BASE_URL = "https://localhost:7133/api";

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    "Content-Type": "application/json",
  },
});

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
      !originalRequest._retry &&
      authService.getRefreshToken()
    ) {
      originalRequest._retry = true;

      try {
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
        if (result.success && result.accessToken && result.refreshToken) {
          authService.saveTokens(
            result.accessToken,
            result.refreshToken,
            result.expiresAt
          );
          originalRequest.headers.Authorization = `Bearer ${result.accessToken}`;
          return api(originalRequest);
        }
      } catch {
        authService.clearTokens();
        window.location.href = "/login";
      }
    }

    return Promise.reject(error);
  }
);

export default api;
