import Image from 'next/image';
import Link from 'next/link';
import { Manrope } from 'next/font/google';

const manrope = Manrope({ subsets: ["latin"], display: "swap" });

export default function AdminLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    return (
        <div className={`flex min-h-screen bg-[#f7f7f7] ${manrope.className}`}>
            {/* Sidebar */}
            <aside className="w-[280px] bg-[#171717] text-white flex flex-col shadow-xl z-20 shrink-0">
                <div className="h-20 flex items-center px-6 border-b border-[#333]">
                    <Link href="/admin" className="block">
                        <Image
                            src="/images/parkwise/logo.png"
                            alt="ParkWise"
                            width={140}
                            height={49}
                            className="object-contain"
                            priority
                        />
                    </Link>
                </div>
                
                <div className="px-6 py-6 border-b border-[#333]">
                    <div className="text-[11px] font-bold text-[#a3a3a3] uppercase tracking-wider mb-3">Admin Portal</div>
                    <div className="flex items-center gap-3">
                        <div className="w-11 h-11 rounded-full bg-[#eafe89] flex items-center justify-center text-[#171717] font-bold text-sm shadow-sm">
                            AD
                        </div>
                        <div className="flex-1 min-w-0">
                            <p className="text-sm font-bold text-white truncate">System Admin</p>
                            <p className="text-xs text-[#a3a3a3] truncate">admin@parkwise.com</p>
                        </div>
                    </div>
                </div>

                <nav className="flex-1 p-4 space-y-1.5 overflow-y-auto">
                    <Link href="/admin" className="flex items-center px-4 py-3.5 text-sm font-semibold rounded-xl bg-[#eafe89] text-[#171717] transition-colors shadow-sm">
                        <svg className="w-5 h-5 mr-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2V6zM14 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2V6zM4 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2zM14 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2v-2z" />
                        </svg>
                        Dashboard
                    </Link>
                    <Link href="/admin/slots" className="flex items-center px-4 py-3.5 text-sm font-medium rounded-xl text-[#a3a3a3] hover:bg-[#262626] hover:text-white transition-colors">
                        <svg className="w-5 h-5 mr-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 10l7-7m0 0l7 7m-7-7v18" />
                        </svg>
                        Parking Slots
                    </Link>
                    <Link href="/admin/users" className="flex items-center px-4 py-3.5 text-sm font-medium rounded-xl text-[#a3a3a3] hover:bg-[#262626] hover:text-white transition-colors">
                        <svg className="w-5 h-5 mr-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
                        </svg>
                        Users Management
                    </Link>
                    <Link href="/admin/reports" className="flex items-center px-4 py-3.5 text-sm font-medium rounded-xl text-[#a3a3a3] hover:bg-[#262626] hover:text-white transition-colors">
                        <svg className="w-5 h-5 mr-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                        </svg>
                        Reports & Analytics
                    </Link>
                    <Link href="/admin/settings" className="flex items-center px-4 py-3.5 text-sm font-medium rounded-xl text-[#a3a3a3] hover:bg-[#262626] hover:text-white transition-colors">
                        <svg className="w-5 h-5 mr-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                        </svg>
                        Settings
                    </Link>
                </nav>

                <div className="p-4 border-t border-[#333]">
                    <button className="flex items-center justify-center w-full px-4 py-3.5 text-sm font-semibold rounded-xl text-white bg-[#262626] hover:bg-red-500 hover:text-white transition-colors">
                        <svg className="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                        </svg>
                        Sign Out
                    </button>
                </div>
            </aside>

            {/* Main Content */}
            <main className="flex-1 flex flex-col min-w-0 h-screen overflow-hidden">
                <header className="h-20 flex items-center justify-between px-8 border-b border-[#e5e5e5] bg-white z-10 shadow-[0_1px_2px_rgb(0,0,0,0.03)] shrink-0">
                    <h1 className="text-xl font-bold text-[#171717] hidden lg:block">Dashboard Overview</h1>
                    
                    <div className="flex items-center space-x-6 ml-auto lg:ml-0">
                        <div className="relative hidden md:block">
                            <input type="text" placeholder="Search everywhere..." className="w-[320px] pl-11 pr-4 py-2.5 bg-[#f5f5f5] border border-transparent rounded-full text-sm focus:outline-none focus:bg-white focus:border-[#e5e5e5] focus:ring-4 focus:ring-[#eafe89]/20 transition-all text-[#171717] placeholder:text-[#a3a3a3]" />
                            <svg className="w-4 h-4 absolute left-4 top-3 text-[#a3a3a3]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                            </svg>
                        </div>
                        <button className="relative p-2.5 rounded-full hover:bg-[#f5f5f5] transition-colors text-[#525252]">
                            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
                            </svg>
                            <span className="absolute top-2 right-2 w-2 h-2 bg-red-500 rounded-full"></span>
                        </button>
                    </div>
                </header>
                
                <div className="flex-1 overflow-auto p-8 bg-[#f7f7f7]">
                    <div className="max-w-[1400px] mx-auto">
                        {children}
                    </div>
                </div>
            </main>
        </div>
    );
}
