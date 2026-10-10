export default function AdminDashboardPage() {
    return (
        <div className="space-y-8 animate-in fade-in duration-500">
            {/* Header Area */}
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h2 className="text-[28px] font-extrabold text-[#171717] tracking-tight">Welcome back, Admin 👋</h2>
                    <p className="text-[#525252] mt-1 text-[15px]">Here's what's happening with your parking system today.</p>
                </div>
                <button className="inline-flex items-center justify-center px-6 py-3 border border-transparent text-sm font-bold rounded-full shadow-sm text-[#171717] bg-[#eafe89] hover:bg-[#d4f55a] transition-all hover:scale-[1.02] active:scale-[0.98] focus:outline-none focus:ring-4 focus:ring-[#eafe89]/30">
                    <svg className="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                    </svg>
                    Add Parking Slot
                </button>
            </div>
            
            {/* Metrics */}
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
                {[
                    { title: "Total Users", value: "1,248", trend: "+12.5%", up: true, icon: "users" },
                    { title: "Active Bookings", value: "156", trend: "+5.2%", up: true, icon: "ticket" },
                    { title: "Available Slots", value: "84", trend: "-2.1%", up: false, icon: "parking" },
                    { title: "Revenue Today", value: "$1,240", trend: "+8.4%", up: true, icon: "chart" }
                ].map((metric, i) => (
                    <div key={i} className="bg-white rounded-2xl p-6 shadow-[0_2px_8px_rgb(0,0,0,0.04)] border border-[#e5e5e5] hover:shadow-[0_8px_24px_rgb(0,0,0,0.06)] hover:border-[#d4d4d4] transition-all group">
                        <div className="flex items-center justify-between mb-4">
                            <div className="w-12 h-12 rounded-2xl bg-[#f5f5f5] group-hover:bg-[#eafe89]/20 transition-colors flex items-center justify-center text-[#171717]">
                                {metric.icon === 'users' && <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" /></svg>}
                                {metric.icon === 'ticket' && <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 5v2m0 4v2m0 4v2M5 5a2 2 0 00-2 2v3a2 2 0 110 4v3a2 2 0 002 2h14a2 2 0 002-2v-3a2 2 0 110-4V7a2 2 0 00-2-2H5z" /></svg>}
                                {metric.icon === 'parking' && <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 10l7-7m0 0l7 7m-7-7v18" /></svg>}
                                {metric.icon === 'chart' && <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" /></svg>}
                            </div>
                            <span className={`px-2.5 py-1 rounded-full text-xs font-bold flex items-center ${metric.up ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'}`}>
                                <svg className={`w-3.5 h-3.5 mr-1 ${metric.up ? '' : 'transform rotate-180'}`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
                                </svg>
                                {metric.trend}
                            </span>
                        </div>
                        <h3 className="text-3xl font-extrabold text-[#171717]">{metric.value}</h3>
                        <p className="text-[#525252] font-medium text-sm mt-1">{metric.title}</p>
                    </div>
                ))}
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Main Chart / Map Area */}
                <div className="lg:col-span-2 bg-white rounded-3xl shadow-[0_2px_8px_rgb(0,0,0,0.04)] border border-[#e5e5e5] p-6 lg:p-8">
                    <div className="flex items-center justify-between mb-8">
                        <h3 className="text-xl font-bold text-[#171717]">Parking Occupancy</h3>
                        <select className="bg-[#f5f5f5] border-none text-sm font-semibold rounded-xl px-4 py-2.5 text-[#171717] focus:ring-4 focus:ring-[#eafe89]/30 outline-none cursor-pointer">
                            <option>Today</option>
                            <option>This Week</option>
                            <option>This Month</option>
                        </select>
                    </div>
                    <div className="h-[360px] bg-[#f9f9f9] rounded-2xl flex flex-col items-center justify-center border-2 border-dashed border-[#e5e5e5]">
                        <div className="w-16 h-16 bg-white rounded-full shadow-sm flex items-center justify-center mb-4 text-[#a3a3a3]">
                            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" />
                            </svg>
                        </div>
                        <p className="text-[#525252] font-medium">Map / Analytics Chart Overview</p>
                        <p className="text-[#a3a3a3] text-sm mt-1">Integration pending</p>
                    </div>
                </div>

                {/* Recent Activity */}
                <div className="bg-white rounded-3xl shadow-[0_2px_8px_rgb(0,0,0,0.04)] border border-[#e5e5e5] p-6 lg:p-8 flex flex-col">
                    <div className="flex items-center justify-between mb-8">
                        <h3 className="text-xl font-bold text-[#171717]">Recent Activity</h3>
                        <button className="text-sm text-[#1e19d8] hover:text-[#110e82] hover:underline font-bold transition-colors">View All</button>
                    </div>
                    <div className="space-y-6 flex-1 overflow-y-auto pr-2">
                        {[
                            { name: "Sarah Jenkins", action: "Booked Slot A-12", time: "2 mins ago", type: "booking" },
                            { name: "Michael Chen", action: "Completed payment", time: "15 mins ago", type: "payment" },
                            { name: "Admin", action: "Updated pricing rules", time: "1 hour ago", type: "system" },
                            { name: "Emma Wilson", action: "Cancelled booking C-04", time: "2 hours ago", type: "cancel" },
                            { name: "David Miller", action: "Booked Slot B-08", time: "3 hours ago", type: "booking" },
                        ].map((item, i) => (
                            <div key={i} className="flex items-start gap-4 group cursor-pointer">
                                <div className={`w-12 h-12 rounded-2xl flex items-center justify-center shrink-0 transition-colors
                                    ${item.type === 'booking' ? 'bg-[#eafe89]/20 text-[#171717]' : 
                                      item.type === 'payment' ? 'bg-green-100 text-green-600' :
                                      item.type === 'cancel' ? 'bg-red-100 text-red-600' :
                                      'bg-[#f5f5f5] text-[#171717]'}`}>
                                    {item.type === 'booking' && <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 10l7-7m0 0l7 7m-7-7v18" /></svg>}
                                    {item.type === 'payment' && <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>}
                                    {item.type === 'system' && <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" /></svg>}
                                    {item.type === 'cancel' && <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" /></svg>}
                                </div>
                                <div className="flex-1 min-w-0">
                                    <p className="text-[15px] font-bold text-[#171717] truncate group-hover:text-[#1e19d8] transition-colors">{item.action}</p>
                                    <p className="text-[13px] text-[#525252] mt-0.5 truncate">by {item.name}</p>
                                    <p className="text-[11px] font-semibold text-[#a3a3a3] mt-1">{item.time}</p>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            </div>
        </div>
    );
}
