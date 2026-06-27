import api from "@/lib/api";
import type { ApiResponse } from "@/types/auth";
import type { User, PagedResult, UserRole, UserStatus } from "@/types/user";

export const userService = {
  async getUsers(
    page: number = 1,
    pageSize: number = 10,
    search?: string
  ): Promise<PagedResult<User>> {
    const params = new URLSearchParams();
    params.append("page", String(page));
    params.append("pageSize", String(pageSize));
    if (search) {
      params.append("search", search);
    }
    const res = await api.get<ApiResponse<PagedResult<User>>>("/users", { params });
    return res.data.data;
  },

  async getUserById(id: string): Promise<User> {
    const res = await api.get<ApiResponse<User>>(`/users/${id}`);
    return res.data.data;
  },

  async createUser(payload: {
    email: string;
    password?: string;
    fullName: string;
    phoneNumber?: string;
    markerCode?: string;
    role: UserRole;
  }): Promise<User> {
    const res = await api.post<ApiResponse<User>>("/users", payload);
    return res.data.data;
  },

  async updateUser(
    id: string,
    payload: {
      fullName?: string;
      phoneNumber?: string;
      markerCode?: string;
      role?: UserRole;
      status?: UserStatus;
    }
  ): Promise<User> {
    const res = await api.put<ApiResponse<User>>(`/users/${id}`, payload);
    return res.data.data;
  },

  async deleteUser(id: string): Promise<void> {
    await api.delete(`/users/${id}`);
  },
};
