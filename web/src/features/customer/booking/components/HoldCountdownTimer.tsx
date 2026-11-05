"use client";

import { useEffect, useState } from "react";
import { Clock, AlertTriangle } from "lucide-react";
import { useCustomerLanguage } from "../../i18n/customerLanguage";

interface HoldCountdownTimerProps {
    expiresAt: string | Date;
    onExpire?: () => void;
    className?: string;
}

export function HoldCountdownTimer({
    expiresAt,
    onExpire,
    className = "",
}: HoldCountdownTimerProps) {
    const { copy } = useCustomerLanguage();
    const [timeLeftMs, setTimeLeftMs] = useState<number>(() => {
        const target = new Date(expiresAt).getTime();
        return Math.max(0, target - Date.now());
    });

    useEffect(() => {
        const interval = setInterval(() => {
            const target = new Date(expiresAt).getTime();
            const diff = Math.max(0, target - Date.now());
            setTimeLeftMs(diff);

            if (diff <= 0) {
                clearInterval(interval);
                onExpire?.();
            }
        }, 1000);

        return () => clearInterval(interval);
    }, [expiresAt, onExpire]);

    const totalSeconds = Math.floor(timeLeftMs / 1000);
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    const isUrgent = totalSeconds < 180; // Dưới 3 phút cảnh báo đỏ
    const isExpired = totalSeconds <= 0;

    const formatted = `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;

    if (isExpired) {
        return (
            <div
                className={`flex items-center gap-2 px-3 py-1.5 rounded-xl bg-red-500/10 border border-red-500/30 text-red-400 text-sm font-medium ${className}`}
                role="status"
            >
                <AlertTriangle size={16} />
                <span>{copy("Your hold has expired", "Thời gian giữ chỗ đã hết hạn")}</span>
            </div>
        );
    }

    return (
        <div
            className={`flex items-center gap-2 px-3.5 py-1.5 rounded-xl text-sm font-semibold transition-colors ${
                isUrgent
                    ? "bg-amber-500/15 border border-amber-500/40 text-amber-400 animate-pulse"
                    : "bg-[#6246ea]/15 border border-[#6246ea]/30 text-[#a28aff]"
            } ${className}`}
            role="timer"
            aria-live="polite"
        >
            <Clock size={16} />
            <span>
                {copy("Spot held for:", "Giữ chỗ tạm thời:")}{" "}
                <strong className="font-mono text-base">{formatted}</strong>
            </span>
        </div>
    );
}
