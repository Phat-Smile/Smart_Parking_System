import { ReactNode } from "react";
import { TopNav } from "../../features/customer/dashboard/components/TopNav";
import { BottomNav } from "../../features/customer/dashboard/components/BottomNav";
import { ThemeProvider } from "../../shared/ui/theme/ThemeContext";
import { Bell } from "lucide-react";
import Link from "next/link";
import "../../features/customer/dashboard/styles/theme.css";

export default function DashboardLayout({ children }: { children: ReactNode }) {
    return (
        <ThemeProvider>
            <div className="dash-wrapper flex flex-col min-h-screen">
                <TopNav />
                
                <div className="flex-1 flex flex-col min-w-0 relative">
                    {/* Mobile Header */}
                    <header className="md:hidden flex items-center justify-between h-16 px-4 bg-[var(--dash-surface)]/90 backdrop-blur-md border-b border-[var(--dash-border)] sticky top-0 z-50 shadow-sm">
                        <span className="font-bold text-lg text-[var(--dash-text)] tracking-tight">Smart Parking</span>
                        <div className="flex items-center gap-3">
                            <Link href="/dashboard/notifications" className="relative p-2 text-[var(--dash-text-muted)]">
                                <Bell size={20} strokeWidth={1.5} />
                                <span className="absolute top-1 right-1 w-2 h-2 bg-red-500 rounded-full"></span>
                            </Link>
                        </div>
                    </header>
                    
                    <main className="flex-1 w-full max-w-7xl mx-auto p-4 md:p-8 pb-24 md:pb-8">
                        {children}
                    </main>

                    <BottomNav />
                </div>
            </div>
        </ThemeProvider>
    );
}
