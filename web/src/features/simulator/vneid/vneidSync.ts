import { VneidCitizenProfile } from "./vneidSeedData";

export interface VneidAuthSession {
    sessionId: string;
    createdAt: number;
    expiresAt: number;
    qrPayload: string;
}

export type VneidSyncMessageType =
    "VNEID_SESSION_CREATED" | "VNEID_AUTH_APPROVED" | "VNEID_AUTH_REJECTED";

export interface VneidSyncMessage {
    type: VneidSyncMessageType;
    sessionId: string;
    profile?: VneidCitizenProfile;
    reason?: string;
    timestamp: number;
}

const SYNC_CHANNEL_NAME = "parkwise_vneid_sync_channel";
const ACTIVE_SESSION_STORAGE_KEY = "parkwise_vneid_active_session";
const SYNC_EVENT_STORAGE_KEY = "parkwise_vneid_sync_event";

/**
 * Generate a new authentic VNeID auth session with a 60-second expiration.
 */
export function createVneidAuthSession(
    lifetimeSeconds: number = 60,
): VneidAuthSession {
    const randomHex = Math.random().toString(36).substring(2, 10);
    const sessionId = `vneid_sess_${Date.now()}_${randomHex}`;
    const createdAt = Date.now();
    const expiresAt = createdAt + lifetimeSeconds * 1000;

    // Official SSO Deep-Link format recognized by VNeID QR Scanner
    const qrPayload = `vneid://oauth2/authorize?client_id=parkwise_smart_parking&session_id=${sessionId}&scope=identity_level2&response_type=code`;

    const session: VneidAuthSession = {
        sessionId,
        createdAt,
        expiresAt,
        qrPayload,
    };

    saveActiveVneidSession(session);
    broadcastSyncMessage({
        type: "VNEID_SESSION_CREATED",
        sessionId,
        timestamp: Date.now(),
    });

    return session;
}

/**
 * Save the latest pending session in local storage for cross-window discovery.
 */
export function saveActiveVneidSession(session: VneidAuthSession | null): void {
    if (typeof window === "undefined") return;
    try {
        if (session) {
            window.localStorage.setItem(
                ACTIVE_SESSION_STORAGE_KEY,
                JSON.stringify(session),
            );
        } else {
            window.localStorage.removeItem(ACTIVE_SESSION_STORAGE_KEY);
        }
    } catch {
        // Fallback gracefully if storage is restricted
    }
}

/**
 * Retrieve the currently pending session if not yet expired.
 */
export function getActiveVneidSession(): VneidAuthSession | null {
    if (typeof window === "undefined") return null;
    try {
        const raw = window.localStorage.getItem(ACTIVE_SESSION_STORAGE_KEY);
        if (!raw) return null;
        const session: VneidAuthSession = JSON.parse(raw);
        if (Date.now() > session.expiresAt) {
            window.localStorage.removeItem(ACTIVE_SESSION_STORAGE_KEY);
            return null;
        }
        return session;
    } catch {
        return null;
    }
}

/**
 * Broadcast message across browser tabs using BroadcastChannel with localStorage fallback.
 */
export function broadcastSyncMessage(message: VneidSyncMessage): void {
    if (typeof window === "undefined") return;

    // 1. BroadcastChannel API
    if (typeof BroadcastChannel !== "undefined") {
        try {
            const channel = new BroadcastChannel(SYNC_CHANNEL_NAME);
            channel.postMessage(message);
            channel.close();
        } catch {
            // Ignore channel error
        }
    }

    // 2. LocalStorage Event (cross-tab fallback)
    try {
        window.localStorage.setItem(
            SYNC_EVENT_STORAGE_KEY,
            JSON.stringify({ ...message, nonce: Math.random() }),
        );
    } catch {
        // Ignore storage error
    }
}

/**
 * Approve a pending VNeID session from the mobile simulator app.
 */
export function approveVneidSession(
    sessionId: string,
    profile: VneidCitizenProfile,
): void {
    const message: VneidSyncMessage = {
        type: "VNEID_AUTH_APPROVED",
        sessionId,
        profile,
        timestamp: Date.now(),
    };
    broadcastSyncMessage(message);
    saveActiveVneidSession(null);
}

/**
 * Reject a pending VNeID session from the mobile simulator app.
 */
export function rejectVneidSession(sessionId: string, reason?: string): void {
    const message: VneidSyncMessage = {
        type: "VNEID_AUTH_REJECTED",
        sessionId,
        reason: reason || "Người dùng từ chối cấp quyền trên ứng dụng VNeID.",
        timestamp: Date.now(),
    };
    broadcastSyncMessage(message);
    saveActiveVneidSession(null);
}

/**
 * Subscribe to sync messages for a specific session ID.
 * Automatically cleans up listeners when unsubscribed.
 */
export function subscribeToVneidSession(
    sessionId: string,
    handlers: {
        onApproved: (profile: VneidCitizenProfile) => void;
        onRejected?: (reason: string) => void;
    },
): () => void {
    if (typeof window === "undefined") return () => {};

    const handleMessage = (msg: VneidSyncMessage) => {
        if (msg.sessionId !== sessionId) return;

        if (msg.type === "VNEID_AUTH_APPROVED" && msg.profile) {
            handlers.onApproved(msg.profile);
        } else if (msg.type === "VNEID_AUTH_REJECTED") {
            handlers.onRejected?.(msg.reason || "Xác thực VNeID bị từ chối.");
        }
    };

    let channel: BroadcastChannel | null = null;
    if (typeof BroadcastChannel !== "undefined") {
        try {
            channel = new BroadcastChannel(SYNC_CHANNEL_NAME);
            channel.onmessage = (event) => {
                if (event.data) {
                    handleMessage(event.data);
                }
            };
        } catch {
            channel = null;
        }
    }

    const storageListener = (e: StorageEvent) => {
        if (e.key === SYNC_EVENT_STORAGE_KEY && e.newValue) {
            try {
                const parsed: VneidSyncMessage = JSON.parse(e.newValue);
                handleMessage(parsed);
            } catch {
                // Ignore parse error
            }
        }
    };

    window.addEventListener("storage", storageListener);

    return () => {
        if (channel) {
            channel.close();
        }
        window.removeEventListener("storage", storageListener);
    };
}
