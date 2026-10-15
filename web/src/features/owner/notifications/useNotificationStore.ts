import { create } from 'zustand';
import { OwnerNotification, NotificationCategory } from './notificationTypes';
import { INITIAL_NOTIFICATIONS } from './mockNotifications';

interface NotificationStoreState {
  notifications: OwnerNotification[];
  activeTab: NotificationCategory;
  setActiveTab: (tab: NotificationCategory) => void;
  markAsRead: (id: string) => void;
  markAllAsRead: () => void;
  getUnreadCount: () => number;
}

export const useNotificationStore = create<NotificationStoreState>((set, get) => ({
  notifications: INITIAL_NOTIFICATIONS,
  activeTab: 'all',
  setActiveTab: (tab) => set({ activeTab: tab }),
  markAsRead: (id) =>
    set((state) => ({
      notifications: state.notifications.map((n) =>
        n.id === id ? { ...n, read: true } : n
      ),
    })),
  markAllAsRead: () =>
    set((state) => ({
      notifications: state.notifications.map((n) => ({ ...n, read: true })),
    })),
  getUnreadCount: () => {
    return get().notifications.filter((n) => !n.read).length;
  },
}));
