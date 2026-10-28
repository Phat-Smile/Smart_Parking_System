"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Bell, Wallet, User } from "lucide-react";
import { vi } from "../../i18n/vi";
import { ThemeToggle } from "../../../../shared/ui/ThemeToggle";

const navItems = [
    { href: "/dashboard", label: vi.dashboard.title },
    { href: "/dashboard/search", label: vi.dashboard.search },
    { href: "/dashboard/bookings", label: vi.dashboard.bookings },
    { href: "/dashboard/payments", label: vi.dashboard.wallet },
    { href: "/dashboard/vehicles", label: vi.dashboard.vehicles },
    { href: "/dashboard/parking-history", label: vi.dashboard.history },
];

export function TopNav() {
    const pathname = usePathname();

    return (
        <header className="hidden md:flex items-center justify-between h-20 px-8 sticky top-0 z-50 border-b border-[var(--dash-border)] bg-[var(--dash-surface)]/90 backdrop-blur-md shadow-sm">
            <div className="flex items-center gap-8">
                <Link
                    href="/dashboard"
                    className="text-xl font-bold tracking-tight text-[var(--dash-text)]"
                >
                    Smart Parking
                </Link>

                <nav className="flex items-center gap-1">
                    {navItems.map((item) => {
                        const isActive =
                            pathname === item.href ||
                            (item.href !== "/dashboard" &&
                                pathname.startsWith(item.href));
                        return (
                            <Link
                                key={item.href}
                                href={item.href}
                                className={`px-4 py-2 rounded-xl text-sm font-medium transition-colors ${
                                    isActive
                                        ? "bg-[var(--dash-primary)] text-[var(--dash-primary-text)]"
                                        : "text-[var(--dash-text-muted)] hover:bg-[var(--dash-text)]/5 hover:text-[var(--dash-text)]"
                                }`}
                            >
                                {item.label}
                            </Link>
                        );
                    })}
                </nav>
            </div>

            <div className="flex items-center gap-5">
                {/* Vehicle Selector */}
                <div className="bg-[var(--dash-bg)] px-3 py-1.5 rounded-xl border border-[var(--dash-border)] flex items-center gap-2">
                    <select className="bg-transparent text-[var(--dash-text)] text-sm font-medium outline-none cursor-pointer appearance-none pr-4">
                        <option
                            value="all"
                            className="bg-[var(--dash-surface)] text-[var(--dash-text)]"
                        >
                            Tất cả xe
                        </option>
                        <option
                            value="29A-12345"
                            className="bg-[var(--dash-surface)] text-[var(--dash-text)]"
                        >
                            29A-12345 (Ô tô)
                        </option>
                    </select>
                </div>

                {/* Wallet */}
                <Link
                    href="/dashboard/payments"
                    className="flex items-center gap-2 bg-[var(--dash-bg)] px-3 py-1.5 rounded-xl border border-[var(--dash-border)] hover:border-[var(--dash-primary)] transition-colors"
                >
                    <div className="flex flex-col text-right">
                        <span className="text-[10px] text-[var(--dash-text-muted)] leading-tight">
                            {vi.wallet.balance}
                        </span>
                        <span className="text-sm font-bold text-[var(--dash-text)] leading-tight">
                            150.000đ
                        </span>
                    </div>
                    <button className="dash-btn-primary p-1.5 rounded-lg flex items-center justify-center">
                        <Wallet size={14} />
                    </button>
                </Link>

                {/* Theme Toggle */}
                <ThemeToggle />

                {/* Notifications */}
                <Link
                    href="/dashboard/notifications"
                    className="relative p-2 text-[var(--dash-text-muted)] hover:text-[var(--dash-text)] transition-colors rounded-full hover:bg-[var(--dash-text)]/5"
                >
                    <Bell size={20} strokeWidth={1.5} />
                    <span className="absolute top-1.5 right-1.5 w-2 h-2 bg-red-500 rounded-full border-2 border-[var(--dash-surface)]"></span>
                </Link>

                {/* Profile Avatar */}
                <Link
                    href="/dashboard/profile"
                    className="w-9 h-9 rounded-full bg-[var(--dash-primary)]/20 text-[var(--dash-primary)] flex items-center justify-center border border-[var(--dash-primary)]/30 hover:scale-105 transition-transform"
                >
                    <User size={18} strokeWidth={1.5} />
                </Link>
            </div>
        </header>
    );
}
