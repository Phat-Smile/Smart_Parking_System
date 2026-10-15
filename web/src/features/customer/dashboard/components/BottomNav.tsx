"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Home, Search, CalendarCheck, Wallet, User } from "lucide-react";
import { vi } from "../../i18n/vi";

const bottomNavItems = [
    { href: "/dashboard", label: vi.dashboard.title, icon: Home },
    { href: "/dashboard/search", label: vi.dashboard.search, icon: Search },
    { href: "/dashboard/bookings", label: vi.dashboard.bookings, icon: CalendarCheck },
    { href: "/dashboard/payments", label: vi.dashboard.wallet, icon: Wallet },
    { href: "/dashboard/profile", label: vi.dashboard.profile, icon: User },
];

export function BottomNav() {
    const pathname = usePathname();

    return (
        <nav className="md:hidden fixed bottom-0 left-0 right-0 bg-[var(--dash-surface)]/95 backdrop-blur-md border-t border-[var(--dash-border)] pb-safe z-50 shadow-[0_-4px_20px_rgba(0,0,0,0.05)]">
            <div className="flex items-center justify-around h-16 px-2">
                {bottomNavItems.map((item) => {
                    const isActive = pathname === item.href || (item.href !== "/dashboard" && pathname.startsWith(item.href));
                    const Icon = item.icon;
                    return (
                        <Link
                            key={item.href}
                            href={item.href}
                            className={`flex flex-col items-center justify-center w-full h-full space-y-1 transition-colors min-w-[44px] min-h-[44px] ${
                                isActive ? "text-[var(--dash-primary)]" : "text-[var(--dash-text-muted)] hover:text-[var(--dash-text)]"
                            }`}
                        >
                            <Icon size={20} strokeWidth={isActive ? 2 : 1.5} className={isActive ? "drop-shadow-[0_0_8px_var(--dash-primary)]" : ""} />
                            <span className="text-[10px] font-medium">{item.label}</span>
                        </Link>
                    );
                })}
            </div>
        </nav>
    );
}
