export interface AppNotification {
  id: string;
  type: string;
  title: string;
  body: string | null;
  isRead: boolean;
  status: string;
  sentAt: string | null;
  createdAt: string;
  assignmentId: string | null;
}

export interface NotificationPage {
  items: AppNotification[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
