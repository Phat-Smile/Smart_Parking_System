"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Home, Search, CalendarCheck, CreditCard, History, Car, User } from "lucide-react";
import { vi } from "../../i18n/vi";

const navItems = [
    { href: "/dashboard", label: vi.dashboard.title, icon: Home },
    { href: "/dashboard/search", label: vi.dashboard.search, icon: Search },
    { href: "/dashboard/bookings", label: vi.dashboard.bookings, icon: CalendarCheck },
    { href: "/dashboard/payments", label: vi.dashboard.wallet, icon: CreditCard },
    { href: "/dashboard/transactions", label: vi.dashboard.transactions, icon: History },
    { href: "/dashboard/parking-history", label: vi.dashboard.history, icon: History },
    { href: "/dashboard/vehicles", label: vi.dashboard.vehicles, icon: Car },
    { href: "/dashboard/profile", label: vi.dashboard.profile, icon: User },
];

export function Sidebar() {
    const pathname = usePathname();

    return (
        <aside className="hidden md:flex flex-col w-64 h-screen bg-[#0b0c17] border-r border-white/10 sticky top-0">
            <div className="p-6">
                <Link href="/dashboard" className="text-xl font-bold text-white tracking-tight">
                    Smart Parking
                </Link>
            </div>
            <nav className="flex-1 px-4 space-y-2 overflow-y-auto">
                {navItems.map((item) => {
                    const isActive = pathname === item.href || (item.href !== "/dashboard" && pathname.startsWith(item.href));
                    const Icon = item.icon;
                    return (
                        <Link
                            key={item.href}
                            href={item.href}
                            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all ${
                                isActive
                                    ? "bg-[#6246ea] text-white shadow-glow-purple"
                                    : "text-[#d5d8e3] hover:bg-white/5 hover:text-white"
                            }`}
                        >
                            <Icon size={20} />
                            <span className="font-medium">{item.label}</span>
                        </Link>
                    );
                })}
            </nav>
            <div className="p-4 border-t border-white/10">
                <div className="text-sm text-[#8790a3] text-center">
                    &copy; 2026 Smart Parking
                </div>
            </div>
        </aside>
    );
}
