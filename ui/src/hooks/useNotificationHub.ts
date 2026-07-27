import { useEffect, useRef } from "react";
import * as signalR from "@microsoft/signalr";
import { authService } from "@/services/authService";
import type { RealtimeNotification } from "@/types/notification";

function hubBaseUrl(): string {
  const apiBase = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5016/api";
  return apiBase.replace(/\/api\/?$/, "");
}

export function useNotificationHub(onReceive: (payload: RealtimeNotification) => void) {
  const handlerRef = useRef(onReceive);

  useEffect(() => {
    handlerRef.current = onReceive;
  });

  useEffect(() => {
    const token = authService.getAccessToken();
    if (!token) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${hubBaseUrl()}/hubs/notifications`, {
        accessTokenFactory: () => authService.getAccessToken() ?? "",
      })
      .withAutomaticReconnect()
      .build();

    connection.on("ReceiveNotification", (payload: RealtimeNotification) => {
      handlerRef.current(payload);
    });

    void connection.start().catch(() => {
      // Hub may be unavailable in local dev without NotificationService running.
    });

    return () => {
      void connection.stop();
    };
  }, []);
}
