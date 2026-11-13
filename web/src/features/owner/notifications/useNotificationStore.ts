import { create } from "zustand";
import { OwnerNotification, NotificationCategory } from "./notificationTypes";
import { INITIAL_NOTIFICATIONS } from "./mockNotifications";
import {
    getMyNotificationsApi,
    markNotificationReadApi,
    markAllNotificationsReadApi,
} from "./ownerNotificationsApi";

interface NotificationStoreState {
    notifications: OwnerNotification[];
    activeTab: NotificationCategory;
    categoryPreferences: Record<string, boolean>;
    isLoading: boolean;
    fetchNotifications: () => Promise<void>;
    setActiveTab: (tab: NotificationCategory) => void;
    setCategoryPreference: (category: string, enabled: boolean) => void;
    markAsRead: (id: string) => Promise<void>;
    markAllAsRead: () => Promise<void>;
    getUnreadCount: () => number;
}

export const useNotificationStore = create<NotificationStoreState>(
    (set, get) => ({
        notifications: INITIAL_NOTIFICATIONS,
        activeTab: "all",
        isLoading: false,
        categoryPreferences: {
            system: true,
            reservations: true,
            revenue: true,
            reports: true,
        },
        fetchNotifications: async () => {
            set({ isLoading: true });
            try {
                const live = await getMyNotificationsApi();
                if (live && live.length > 0) {
                    set({ notifications: live });
                }
            } finally {
                set({ isLoading: false });
            }
        },
        setActiveTab: (tab) => set({ activeTab: tab }),
        setCategoryPreference: (category, enabled) =>
            set((state) => ({
                categoryPreferences: {
                    ...state.categoryPreferences,
                    [category]: enabled,
                },
            })),
        markAsRead: async (id) => {
            set((state) => ({
                notifications: state.notifications.map((n) =>
                    n.id === id ? { ...n, read: true } : n,
                ),
            }));
            await markNotificationReadApi(id);
        },
        markAllAsRead: async () => {
            set((state) => ({
                notifications: state.notifications.map((n) => ({
                    ...n,
                    read: true,
                })),
            }));
            await markAllNotificationsReadApi();
        },
        getUnreadCount: () => {
            return get().notifications.filter((n) => !n.read).length;
        },
    }),
);
