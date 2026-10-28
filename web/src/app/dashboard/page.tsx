import { DashboardHero } from "../../features/customer/dashboard/components/DashboardHero";
import { DemoPanel } from "../../features/customer/dashboard/components/DemoPanel";
import { Search, MapPin, Car, CalendarCheck, Wallet } from "lucide-react";
import Link from "next/link";

export default function DashboardPage() {
    return (
        <div className="animate-in fade-in duration-500 space-y-8">
            {/* Greeting & Search */}
            <div className="flex flex-col md:flex-row md:items-end justify-between gap-4">
                <div>
                    <h1 className="text-3xl font-bold text-[var(--dash-text)] mb-2">
                        Chào buổi sáng, Alexandre! 👋
                    </h1>
                    <p className="text-[var(--dash-text-muted)]">
                        Hãy tìm bãi đỗ xe phù hợp cho ngày hôm nay.
                    </p>
                </div>
                <div className="flex w-full md:w-auto gap-2">
                    <div className="relative flex-1 md:w-64">
                        <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-[var(--dash-text-muted)] w-4 h-4" />
                        <input
                            type="text"
                            placeholder="Tìm bãi đỗ..."
                            className="w-full pl-9 pr-4 py-2.5 bg-[var(--dash-surface)] border border-[var(--dash-border)] rounded-xl outline-none focus:border-[var(--dash-primary)] transition-colors text-sm text-[var(--dash-text)]"
                        />
                    </div>
                    <Link
                        href="/dashboard/search"
                        className="dash-btn-primary px-4 py-2.5 rounded-xl flex items-center justify-center gap-2 whitespace-nowrap text-sm"
                    >
                        <MapPin size={16} />
                        Gần tôi
                    </Link>
                </div>
            </div>

            {/* 4 Stats Cards */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div className="dash-card p-5">
                    <div className="w-10 h-10 rounded-full bg-blue-500/10 text-blue-500 flex items-center justify-center mb-3">
                        <MapPin size={20} />
                    </div>
                    <div className="text-2xl font-bold text-[var(--dash-text)] mb-1">
                        12
                    </div>
                    <div className="text-xs text-[var(--dash-text-muted)] uppercase tracking-wide font-medium">
                        Bãi gần đây
                    </div>
                </div>
                <Link
                    href="/dashboard/vehicles"
                    className="dash-card p-5 hover:border-[var(--dash-primary)] transition-colors group cursor-pointer block"
                >
                    <div className="w-10 h-10 rounded-full bg-purple-500/10 text-purple-500 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                        <Car size={20} />
                    </div>
                    <div className="text-2xl font-bold text-[var(--dash-text)] mb-1">
                        2
                    </div>
                    <div className="text-xs text-[var(--dash-text-muted)] uppercase tracking-wide font-medium">
                        Xe của tôi
                    </div>
                </Link>
                <Link
                    href="/dashboard/bookings"
                    className="dash-card p-5 hover:border-[var(--dash-primary)] transition-colors group cursor-pointer block"
                >
                    <div className="w-10 h-10 rounded-full bg-[var(--dash-primary)]/10 text-[var(--dash-primary)] flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                        <CalendarCheck size={20} />
                    </div>
                    <div className="text-2xl font-bold text-[var(--dash-text)] mb-1">
                        1
                        <span className="text-sm font-normal text-[var(--dash-text-muted)]">
                            /3
                        </span>
                    </div>
                    <div className="text-xs text-[var(--dash-text-muted)] uppercase tracking-wide font-medium">
                        Đang hiệu lực
                    </div>
                </Link>
                <Link
                    href="/dashboard/payments"
                    className="dash-card p-5 hover:border-[var(--dash-primary)] transition-colors group cursor-pointer block"
                >
                    <div className="w-10 h-10 rounded-full bg-emerald-500/10 text-emerald-500 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                        <Wallet size={20} />
                    </div>
                    <div className="text-2xl font-bold text-[var(--dash-text)] mb-1 text-emerald-500 text-lg sm:text-2xl truncate">
                        150k
                    </div>
                    <div className="text-xs text-[var(--dash-text-muted)] uppercase tracking-wide font-medium">
                        Số dư ví
                    </div>
                </Link>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Hero Section */}
                <div className="lg:col-span-2">
                    <DashboardHero />
                </div>

                {/* Mini map or Recent */}
                <div className="dash-card p-6 lg:col-span-1 h-[400px] flex flex-col">
                    <h3 className="font-bold text-[var(--dash-text)] mb-4">
                        Gợi ý gần đây
                    </h3>
                    <div className="flex-1 rounded-xl overflow-hidden relative border border-[var(--dash-border)] bg-[var(--dash-bg)] flex items-center justify-center">
                        <div
                            className="absolute inset-0 opacity-[0.03] dark:opacity-10"
                            style={{
                                backgroundImage:
                                    "radial-gradient(circle at 2px 2px, currentColor 1px, transparent 0)",
                                backgroundSize: "24px 24px",
                            }}
                        ></div>
                        <div className="text-center z-10 p-4">
                            <MapPin className="h-10 w-10 text-[var(--dash-primary)] mx-auto mb-2 opacity-50" />
                            <p className="text-[var(--dash-text-muted)] text-sm">
                                Bản đồ khu vực
                            </p>
                        </div>
                    </div>
                </div>
            </div>

            <DemoPanel />
        </div>
    );
}
