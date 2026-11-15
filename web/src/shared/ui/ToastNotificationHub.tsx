"use client";

import React, { useState, useEffect, useSyncExternalStore } from "react";
import { Bell, CheckCircle, AlertTriangle, Info, X } from "lucide-react";
import {
    isPushSupported,
    getPushPermission,
    requestPushPermission,
    sendWebPushNotification,
} from "../notifications/webPushService";
import { useCustomerLanguage } from "../../features/customer/i18n/customerLanguage";

export const PUSH_PROMPT_SESSION_KEY =
    "parkwise_push_prompt_dismissed_session";

export function isPushPromptDismissedInSession(): boolean {
    if (typeof window === "undefined") return false;
    try {
        return (
            window.sessionStorage?.getItem(PUSH_PROMPT_SESSION_KEY) === "true"
        );
    } catch {
        return false;
    }
}

export function markPushPromptDismissedInSession(): void {
    if (typeof window === "undefined") return;
    try {
        window.sessionStorage?.setItem(PUSH_PROMPT_SESSION_KEY, "true");
    } catch {
        // Safe fallback if sessionStorage is restricted or throws
    }
}

export interface LiveToast {
    id: string;
    title: string;
    message: string;
    type: "info" | "success" | "warning";
    timestamp: Date;
}

type ToastListener = (toast: LiveToast) => void;
const listeners = new Set<ToastListener>();

export function emitLiveToast(
    title: string,
    message: string,
    type: "info" | "success" | "warning" = "info",
) {
    const toast: LiveToast = {
        id: `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
        title,
        message,
        type,
        timestamp: new Date(),
    };

    // Trigger native Web Push & Chime sound
    sendWebPushNotification(title, {
        body: message,
        type:
            type === "warning"
                ? "warning"
                : type === "success"
                  ? "success"
                  : "info",
    });

    listeners.forEach((listener) => listener(toast));
}

export function ToastNotificationHub() {
    const { copy } = useCustomerLanguage();
    const [toasts, setToasts] = useState<LiveToast[]>([]);
    const [userPermission, setUserPermission] =
        useState<NotificationPermission | null>(null);
    const [promptDismissed, setPromptDismissed] = useState(false);
    const [sessionDismissed, setSessionDismissed] = useState(false);

    // false on the server and during hydration, true afterwards, without setting state in an effect.
    const isMounted = useSyncExternalStore(
        () => () => {},
        () => true,
        () => false,
    );

    const isSupported =
        isMounted && typeof window !== "undefined" ? isPushSupported() : false;
    const detectedPermission =
        isMounted && typeof window !== "undefined"
            ? getPushPermission()
            : "default";
    const permission = userPermission ?? detectedPermission;

    useEffect(() => {
        if (isPushPromptDismissedInSession()) {
            setSessionDismissed(true);
        }
    }, []);

    const isDismissed = promptDismissed || sessionDismissed;

    useEffect(() => {
        const handleNewToast: ToastListener = (toast) => {
            setToasts((prev) => [toast, ...prev].slice(0, 3));
            setTimeout(() => {
                setToasts((prev) => prev.filter((t) => t.id !== toast.id));
            }, 5000);
        };

        listeners.add(handleNewToast);
        return () => {
            listeners.delete(handleNewToast);
        };
    }, []);

    useEffect(() => {
        if (!isSupported || permission === "granted" || isDismissed) return;
        const timer = setTimeout(() => {
            setPromptDismissed(true);
        }, 8000);
        return () => clearTimeout(timer);
    }, [isSupported, permission, isDismissed]);

    async function handleRequestPermission() {
        const result = await requestPushPermission();
        setUserPermission(result);
        setPromptDismissed(true);
        if (result === "granted") {
            emitLiveToast(
                copy("Push notifications enabled", "Đã bật thông báo đẩy"),
                copy(
                    "You will receive live alerts about your parking session.",
                    "Bạn sẽ nhận được cảnh báo theo thời gian thực về phiên đỗ xe.",
                ),
                "success",
            );
        }
    }

    function handleDismissLater() {
        setPromptDismissed(true);
        setSessionDismissed(true);
        markPushPromptDismissedInSession();
    }

    if (!isMounted) return null;

    const showPrompt =
        isSupported && permission !== "granted" && !isDismissed;

    return (
        <>
            {/* Quick Web Push Enabler Bar/Badge:
                On mobile (< 640px / sm): fixed at top of screen (top-16 left-2 right-2) as a compact horizontal bar,
                keeping bottom form controls and action buttons (such as entrance confirmation) completely unobstructed.
                On desktop (>= 640px / sm): fixed at bottom-right (bottom-4 right-4). */}
            {showPrompt && (
                <div
                    data-testid="push-notification-prompt"
                    className="pw-push-prompt fixed top-16 left-2 right-2 sm:top-auto sm:left-auto sm:bottom-4 sm:right-4 z-50 pointer-events-none select-none max-w-none sm:max-w-xs w-auto sm:w-full no-print"
                    aria-live="polite"
                >
                    <div className="pointer-events-auto bg-neutral-900/95 backdrop-blur border border-neutral-700/80 text-white px-3 py-2 sm:py-1.5 rounded-xl sm:rounded-full shadow-xl flex items-center justify-between sm:justify-start gap-2 text-xs">
                        <div className="flex items-center gap-2 min-w-0">
                            <Bell className="w-3.5 h-3.5 text-amber-400 animate-pulse shrink-0" />
                            <span className="truncate text-[11px] sm:text-xs">
                                {copy(
                                    "Get parking entry and exit alerts?",
                                    "Nhận thông báo khi xe vào/ra?",
                                )}
                            </span>
                        </div>
                        <div className="flex items-center gap-1.5 shrink-0">
                            <button
                                type="button"
                                onClick={handleRequestPermission}
                                className="bg-brand-500 hover:bg-brand-400 text-white font-semibold px-2.5 py-0.5 rounded-full text-[11px] transition cursor-pointer shrink-0"
                            >
                                {copy("Enable", "Bật")}
                            </button>
                            <button
                                type="button"
                                onClick={handleDismissLater}
                                className="bg-neutral-800 hover:bg-neutral-700 text-neutral-300 hover:text-white px-2 py-0.5 rounded-full text-[11px] transition cursor-pointer shrink-0"
                            >
                                {copy("Later", "Để sau")}
                            </button>
                            <button
                                type="button"
                                onClick={handleDismissLater}
                                className="text-neutral-400 hover:text-white p-0.5 rounded-full transition cursor-pointer shrink-0"
                                aria-label={copy(
                                    "Dismiss prompt",
                                    "Đóng gợi ý thông báo",
                                )}
                            >
                                <X className="w-3 h-3" />
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Render active floating toasts */}
            {toasts.length > 0 && (
                <div
                    className="pw-toast-hub fixed bottom-4 right-4 z-50 flex flex-col items-end gap-2.5 pointer-events-none select-none max-w-xs w-full no-print"
                    aria-live="polite"
                >
                    {toasts.map((toast) => {
                        const isSuccess = toast.type === "success";
                        const isWarning = toast.type === "warning";
                        const borderColor = isSuccess
                            ? "border-emerald-500/40"
                            : isWarning
                              ? "border-amber-500/40"
                              : "border-blue-500/40";
                        const bgColor = isSuccess
                            ? "bg-emerald-950/90"
                            : isWarning
                              ? "bg-amber-950/90"
                              : "bg-neutral-900/95";

                        return (
                            <div
                                key={toast.id}
                                className={`pointer-events-auto w-full ${bgColor} backdrop-blur-md border ${borderColor} text-white p-3.5 rounded-xl shadow-2xl transition-all duration-300 animate-in slide-in-from-right-4 fade-in`}
                            >
                                <div className="flex items-start justify-between gap-2.5">
                                    <div className="flex items-start gap-2.5">
                                        {isSuccess ? (
                                            <CheckCircle className="w-4 h-4 text-emerald-400 mt-0.5 shrink-0" />
                                        ) : isWarning ? (
                                            <AlertTriangle className="w-4 h-4 text-amber-400 mt-0.5 shrink-0" />
                                        ) : (
                                            <Info className="w-4 h-4 text-blue-400 mt-0.5 shrink-0" />
                                        )}
                                        <div>
                                            <strong className="block text-xs font-bold leading-tight">
                                                {toast.title}
                                            </strong>
                                            <p className="text-[11px] text-neutral-300 mt-0.5 leading-snug">
                                                {toast.message}
                                            </p>
                                            <span className="text-[9px] text-neutral-400 mt-1 block">
                                                {copy("Just now", "Vừa xong")}
                                            </span>
                                        </div>
                                    </div>
                                    <button
                                        type="button"
                                        onClick={() =>
                                            setToasts((prev) =>
                                                prev.filter(
                                                    (t) => t.id !== toast.id,
                                                ),
                                            )
                                        }
                                        className="text-neutral-400 hover:text-white p-0.5 rounded cursor-pointer"
                                        aria-label={copy("Dismiss", "Đóng")}
                                    >
                                        <X className="w-3.5 h-3.5" />
                                    </button>
                                </div>
                            </div>
                        );
                    })}
                </div>
            )}
        </>
    );
}
