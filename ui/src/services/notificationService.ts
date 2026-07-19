import api from "@/lib/api";
import type { ApiResponse } from "@/types/auth";
import type { NotificationPage } from "@/types/notification";

export const notificationService = {
  async listNotifications(page = 1, pageSize = 20): Promise<NotificationPage> {
    const res = await api.get<ApiResponse<NotificationPage>>("/notifications", {
      params: { page, pageSize },
    });
    return res.data.data;
  },

  async markAsRead(id: string): Promise<void> {
    await api.put<ApiResponse<null>>(`/notifications/${id}/read`);
  },
};
