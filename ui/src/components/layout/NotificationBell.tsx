import { useState, useEffect, useRef, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { Bell } from "lucide-react";
import { notificationService } from "@/services/notificationService";
import { useNotificationHub } from "@/hooks/useNotificationHub";
import type { AppNotification, RealtimeNotification } from "@/types/notification";
import { cn } from "@/lib/utils";

const PAGE_SIZE = 10;

function formatTime(iso: string): string {
  return new Date(iso).toLocaleString("vi-VN");
}

export function NotificationBell() {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [notifications, setNotifications] = useState<AppNotification[]>([]);
  const [loading, setLoading] = useState(false);
  const [toast, setToast] = useState<RealtimeNotification | null>(null);

  const containerRef = useRef<HTMLDivElement>(null);

  const load = useCallback(() => {
    setLoading(true);
    notificationService
      .listNotifications(1, PAGE_SIZE)
      .then((result) => setNotifications(result.items))
      .catch(() => setNotifications([]))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  useNotificationHub((payload) => {
    setToast(payload);
    const optimistic: AppNotification = {
      id: `rt-${Date.now()}`,
      type: payload.type,
      title: payload.title,
      body: payload.body ?? null,
      isRead: false,
      status: "Sent",
      sentAt: new Date().toISOString(),
      createdAt: new Date().toISOString(),
      assignmentId: null,
      batchId: payload.batchId ?? null,
    };
    setNotifications((prev) => [optimistic, ...prev].slice(0, PAGE_SIZE));
    window.setTimeout(() => setToast(null), 6000);
  });

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const unreadCount = notifications.filter((n) => !n.isRead).length;

  const handleToggle = () => {
    setOpen((prev) => {
      if (!prev) load();
      return !prev;
    });
  };

  const navigateForNotification = (notification: {
    assignmentId?: string | null;
    batchId?: string | null;
  }) => {
    if (notification.batchId) {
      navigate(`/grading/queue?batchId=${notification.batchId}`);
      return;
    }
    if (notification.assignmentId) {
      navigate(`/grading/${notification.assignmentId}`);
    }
  };

  const handleSelect = async (notification: AppNotification) => {
    if (!notification.isRead && !notification.id.startsWith("rt-")) {
      try {
        await notificationService.markAsRead(notification.id);
        setNotifications((prev) =>
          prev.map((n) => (n.id === notification.id ? { ...n, isRead: true } : n))
        );
      } catch {
        // Non-critical
      }
    } else if (!notification.isRead) {
      setNotifications((prev) =>
        prev.map((n) => (n.id === notification.id ? { ...n, isRead: true } : n))
      );
    }
    setOpen(false);
    navigateForNotification(notification);
  };

  return (
    <>
      {toast && (
        <div className="fixed top-4 right-4 z-[100] max-w-sm rounded-xl border border-line bg-card shadow-lg px-4 py-3">
          <p className="text-sm font-semibold text-ink">{toast.title}</p>
          {toast.body && <p className="text-xs text-ink-soft mt-1">{toast.body}</p>}
          {toast.batchId && (
            <button
              type="button"
              className="mt-2 text-xs text-primary hover:underline"
              onClick={() => {
                setToast(null);
                navigate(`/grading/queue?batchId=${toast.batchId}`);
              }}
            >
              Mở hàng chờ
            </button>
          )}
        </div>
      )}

      <div ref={containerRef} className="relative">
        <button
          type="button"
          onClick={handleToggle}
          className="relative p-2 rounded-lg border border-line bg-card hover:bg-secondary transition-colors"
          title="Thông báo"
        >
          <Bell className="h-4 w-4 text-ink-soft" />
          {unreadCount > 0 && (
            <span className="absolute -top-1 -right-1 min-w-[1.1rem] h-[1.1rem] px-1 rounded-full bg-brand-red text-white text-[10px] font-medium flex items-center justify-center">
              {unreadCount > 9 ? "9+" : unreadCount}
            </span>
          )}
        </button>

        {open && (
          <div className="absolute right-0 mt-2 w-96 max-h-[28rem] overflow-y-auto rounded-xl border border-line bg-card shadow-lg z-50">
            <div className="px-4 py-3 border-b border-line">
              <p className="text-sm font-semibold text-ink">Thông báo</p>
            </div>
            {loading ? (
              <div className="p-4 text-sm text-ink-soft">Đang tải...</div>
            ) : notifications.length === 0 ? (
              <div className="p-4 text-sm text-ink-soft">Chưa có thông báo nào.</div>
            ) : (
              <div className="divide-y divide-line">
                {notifications.map((n) => (
                  <button
                    key={n.id}
                    type="button"
                    onClick={() => void handleSelect(n)}
                    className={cn(
                      "w-full text-left px-4 py-3 hover:bg-secondary transition-colors",
                      !n.isRead && "bg-brand-red/5"
                    )}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <p className="text-sm font-medium text-ink">{n.title}</p>
                      {!n.isRead && (
                        <span className="mt-1 h-2 w-2 rounded-full bg-brand-red shrink-0" />
                      )}
                    </div>
                    {n.body && (
                      <p className="text-xs text-ink-soft mt-0.5 line-clamp-2">{n.body}</p>
                    )}
                    <p className="text-xs text-ink-soft/70 mt-1">
                      {formatTime(n.sentAt ?? n.createdAt)}
                    </p>
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
      </div>
    </>
  );
}
