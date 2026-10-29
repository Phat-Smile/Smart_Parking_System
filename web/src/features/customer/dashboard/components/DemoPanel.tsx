"use client";

import { useDashboardStore, HeroState } from "../store/useDashboardStore";
import { vi } from "../../i18n/vi";

export function DemoPanel() {
    const {
        heroState,
        setHeroState,
        vehicleType,
        setVehicleType,
        setCountdown,
    } = useDashboardStore();

    if (process.env.NODE_ENV !== "development") return null;

    const states: { value: HeroState; label: string }[] = [
        { value: "empty", label: "1. Không có gì" },
        { value: "holding", label: "2. Đang thanh toán cọc" },
        { value: "confirmed", label: "3. Đã xác nhận" },
        { value: "active", label: "4. Đang đỗ" },
        { value: "paid", label: "5. Đã thanh toán" },
        { value: "overstay", label: "6. Quá hạn/Phí vượt" },
    ];

    const handleStateChange = (state: HeroState) => {
        setHeroState(state);
        if (state === "holding") setCountdown(600);
        if (state === "paid") setCountdown(900);
    };

    return (
        <div className="fixed bottom-20 md:bottom-4 right-4 bg-[var(--dash-surface)] border border-[var(--dash-primary)] p-4 rounded-xl shadow-lg z-40 max-w-xs">
            <div className="text-xs font-bold text-[var(--dash-primary)] mb-3 uppercase tracking-wider">
                {vi.common.demoPanel}
            </div>

            <div className="flex flex-col gap-2 mb-4">
                {states.map((s) => (
                    <button
                        key={s.value}
                        onClick={() => handleStateChange(s.value)}
                        className={`text-left px-3 py-1.5 rounded text-sm transition-colors ${
                            heroState === s.value
                                ? "bg-[var(--dash-primary)] text-[var(--dash-primary-text)] font-bold"
                                : "bg-[var(--dash-bg)] text-[var(--dash-text-muted)] hover:text-[var(--dash-text)]"
                        }`}
                    >
                        {s.label}
                    </button>
                ))}
            </div>

            <div className="border-t border-[var(--dash-border)] pt-3">
                <div className="text-xs text-[var(--dash-text-muted)] mb-2">
                    Loại xe đang chọn:
                </div>
                <div className="flex gap-2">
                    <button
                        onClick={() => setVehicleType("car")}
                        className={`flex-1 py-1 text-sm rounded ${vehicleType === "car" ? "bg-[var(--dash-text)]/10 text-[var(--dash-text)] font-medium" : "text-[var(--dash-text-muted)] hover:bg-[var(--dash-text)]/5"}`}
                    >
                        Ô tô
                    </button>
                    <button
                        onClick={() => setVehicleType("motorcycle")}
                        className={`flex-1 py-1 text-sm rounded ${vehicleType === "motorcycle" ? "bg-[var(--dash-text)]/10 text-[var(--dash-text)] font-medium" : "text-[var(--dash-text-muted)] hover:bg-[var(--dash-text)]/5"}`}
                    >
                        Xe máy
                    </button>
                </div>
            </div>
        </div>
    );
}
