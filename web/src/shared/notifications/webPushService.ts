"use client";

/**
 * Web Push and Audio Notification Service
 * Utilizes W3C Notification API and Web Audio API for offline synthetic chimes.
 */

export function isPushSupported(): boolean {
    if (typeof window === "undefined") return false;
    return "Notification" in window;
}

export function getPushPermission(): NotificationPermission {
    if (!isPushSupported()) return "denied";
    return Notification.permission;
}

export async function requestPushPermission(): Promise<NotificationPermission> {
    if (!isPushSupported()) return "denied";
    try {
        const permission = await Notification.requestPermission();
        return permission;
    } catch {
        return "denied";
    }
}

/**
 * Plays a clean, professional synthetic harmonic chime using the Web Audio API.
 * Does not require external audio assets.
 */
export function playNotificationChime(
    type: "info" | "success" | "warning" | "urgent" = "info",
): void {
    if (typeof window === "undefined") return;

    try {
        const AudioCtx =
            window.AudioContext ||
            (window as unknown as { webkitAudioContext: typeof AudioContext })
                .webkitAudioContext;
        if (!AudioCtx) return;

        const ctx = new AudioCtx();
        const now = ctx.currentTime;

        const frequencies: Record<typeof type, number[]> = {
            info: [587.33, 880.0], // D5 -> A5
            success: [523.25, 659.25, 783.99], // C5 -> E5 -> G5
            warning: [698.46, 554.37], // F5 -> C#5
            urgent: [880.0, 440.0, 880.0], // A5 -> A4 -> A5
        };

        const notes = frequencies[type];
        const noteDuration = 0.12;

        notes.forEach((freq, index) => {
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();

            osc.type = "sine";
            osc.frequency.setValueAtTime(freq, now + index * noteDuration);

            const startTime = now + index * noteDuration;
            const stopTime = startTime + noteDuration * 1.5;

            gain.gain.setValueAtTime(0, startTime);
            gain.gain.linearRampToValueAtTime(0.18, startTime + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.001, stopTime);

            osc.connect(gain);
            gain.connect(ctx.destination);

            osc.start(startTime);
            osc.stop(stopTime);
        });

        setTimeout(
            () => {
                ctx.close().catch(() => {});
            },
            (notes.length * noteDuration + 0.5) * 1000,
        );
    } catch {
        // AudioContext may be blocked before first user gesture
    }
}

/**
 * Sends a native desktop/mobile web push notification if permission is granted.
 */
export function sendWebPushNotification(
    title: string,
    options?: {
        body?: string;
        tag?: string;
        icon?: string;
        badge?: string;
        playSound?: boolean;
        type?: "info" | "success" | "warning" | "urgent";
    },
): boolean {
    if (typeof window === "undefined" || !isPushSupported()) return false;

    const playSound = options?.playSound ?? true;
    const type = options?.type ?? "info";

    if (playSound) {
        playNotificationChime(type);
    }

    if (Notification.permission === "granted") {
        try {
            new Notification(title, {
                body: options?.body,
                tag: options?.tag || "parkwise-notification",
                icon: options?.icon || "/icon-parkwise.png",
                badge: options?.badge || "/icon-parkwise.png",
            });
            return true;
        } catch {
            return false;
        }
    }

    return false;
}
