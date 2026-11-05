import { en } from "../../i18n/en";
import { vi } from "../../i18n/vi";
import { useCustomerLanguage } from "../../i18n/customerLanguage";

export type BookingStatus =
    "holding" | "confirmed" | "used" | "cancelled" | "expired";

export const bookingBadgeConfig: Record<
    BookingStatus,
    { label: string; className: string }
> = {
    holding: {
        label: en.bookingsPage.status.holding,
        className: "bg-[#f59e0b]/20 text-[#fbbf24] border border-[#f59e0b]/30",
    },
    confirmed: {
        label: en.bookingsPage.status.confirmed,
        className: "bg-[#10b981]/20 text-[#34d399] border border-[#10b981]/30",
    },
    used: {
        label: en.bookingsPage.status.used,
        className: "bg-white/10 text-[#d5d8e3] border border-white/20",
    },
    cancelled: {
        label: en.bookingsPage.status.cancelled,
        className: "bg-[#b42318]/20 text-[#ff9388] border border-[#b42318]/30",
    },
    expired: {
        label: en.bookingsPage.status.expired,
        className: "bg-[#56657a]/20 text-[#8790a3] border border-[#56657a]/30",
    },
};

export function BookingBadge({ status }: { status: BookingStatus }) {
    const { isVietnamese } = useCustomerLanguage();
    const config = bookingBadgeConfig[status] || bookingBadgeConfig.expired;
    return (
        <span
            className={`px-2.5 py-1 rounded-md text-xs font-medium ${config.className}`}
        >
            {isVietnamese ? vi.bookingsPage.status[status] : config.label}
        </span>
    );
}
