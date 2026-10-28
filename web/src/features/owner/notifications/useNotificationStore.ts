import { create } from "zustand";
import { OwnerNotification, NotificationCategory } from "./notificationTypes";
import { INITIAL_NOTIFICATIONS } from "./mockNotifications";

interface NotificationStoreState {
    notifications: OwnerNotification[];
    activeTab: NotificationCategory;
    categoryPreferences: Record<NotificationCategory, boolean>;
    setActiveTab: (tab: NotificationCategory) => void;
    setCategoryPreference: (
        category: NotificationCategory,
        enabled: boolean,
    ) => void;
    markAsRead: (id: string) => void;
    markAllAsRead: () => void;
    getUnreadCount: () => number;
}

const DEFAULT_PREFERENCES: Record<NotificationCategory, boolean> = {
    all: true,
    lots: true,
    reservations: true,
    sessions: true,
    layout: true,
    reports: true,
    system: true,
};

export const useNotificationStore = create<NotificationStoreState>(
    (set, get) => ({
        notifications: INITIAL_NOTIFICATIONS,
        activeTab: "all",
        categoryPreferences: DEFAULT_PREFERENCES,
        setActiveTab: (tab) => set({ activeTab: tab }),
        setCategoryPreference: (category, enabled) =>
            set((state) => ({
                categoryPreferences: {
                    ...state.categoryPreferences,
                    [category]: enabled,
                },
            })),
        markAsRead: (id) =>
            set((state) => ({
                notifications: state.notifications.map((n) =>
                    n.id === id ? { ...n, read: true } : n,
                ),
            })),
        markAllAsRead: () =>
            set((state) => ({
                notifications: state.notifications.map((n) => ({
                    ...n,
                    read: true,
                })),
            })),
        getUnreadCount: () => {
            const { notifications, categoryPreferences } = get();
            return notifications.filter((n) => {
                if (n.read) return false;
                const pref = categoryPreferences[n.category];
                return pref !== false;
            }).length;
        },
    }),
);
