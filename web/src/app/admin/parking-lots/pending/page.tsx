import Link from 'next/link';

export default function PendingParkingLotsPage() {
    // Dummy data to populate the UI initially
    const pendingLots = [
        {
            id: "PL-001",
            parkingName: "Downtown Central Parking",
            owner: { name: "Nguyen Van A", email: "vana.nguyen@example.com", phone: "+84 987 654 321" },
            submissionInfo: { date: "Oct 10, 2026", time: "14:30", documents: 3 },
            status: "Pending Review",
        },
        {
            id: "PL-002",
            parkingName: "Vincom Mega Mall Basement",
            owner: { name: "Tran Thi B", email: "thib.tran@example.com", phone: "+84 912 345 678" },
            submissionInfo: { date: "Oct 09, 2026", time: "09:15", documents: 5 },
            status: "Under Review",
        },
        {
            id: "PL-003",
            parkingName: "Tech Hub Secure Parking",
            owner: { name: "Le Minh C", email: "minhc.le@example.com", phone: "+84 905 111 222" },
            submissionInfo: { date: "Oct 08, 2026", time: "16:45", documents: 4 },
            status: "Pending Review",
        },
        {
            id: "PL-004",
            parkingName: "District 1 Open Lot",
            owner: { name: "Pham Van D", email: "vand.pham@example.com", phone: "+84 933 444 555" },
            submissionInfo: { date: "Oct 08, 2026", time: "11:20", documents: 2 },
            status: "Action Required",
        },
    ];

    return (
        <div className="space-y-8 animate-in fade-in duration-500">
            {/* Header Area */}
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <div className="flex items-center gap-2 text-sm text-[#525252] font-medium mb-1">
                        <Link href="/admin" className="hover:text-[#171717] transition-colors">Admin</Link>
                        <span>/</span>
                        <Link href="/admin/parking-lots" className="hover:text-[#171717] transition-colors">Parking Lots</Link>
                        <span>/</span>
                        <span className="text-[#171717]">Pending</span>
                    </div>
                    <h2 className="text-[28px] font-extrabold text-[#171717] tracking-tight">Pending Parking Lots</h2>
                    <p className="text-[#525252] mt-1 text-[15px]">Review and approve new parking lot submissions from partners.</p>
                </div>
                <div className="flex items-center gap-3">
                    <div className="relative">
                        <input type="text" placeholder="Search by name or owner..." className="w-64 pl-10 pr-4 py-2.5 bg-white border border-[#e5e5e5] rounded-full text-sm focus:outline-none focus:border-[#d4d4d4] focus:ring-4 focus:ring-[#eafe89]/20 transition-all text-[#171717] placeholder:text-[#a3a3a3] shadow-sm" />
                        <svg className="w-4 h-4 absolute left-4 top-3 text-[#a3a3a3]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                        </svg>
                    </div>
                    <button className="inline-flex items-center justify-center p-2.5 border border-[#e5e5e5] rounded-full bg-white text-[#171717] hover:bg-[#f5f5f5] transition-colors shadow-sm focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-[#eafe89]">
                        <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z" />
                        </svg>
                    </button>
                </div>
            </div>

            {/* Main Table Card */}
            <div className="bg-white rounded-3xl shadow-[0_2px_8px_rgb(0,0,0,0.04)] border border-[#e5e5e5] overflow-hidden">
                <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                        <thead>
                            <tr className="bg-[#f9f9f9] border-b border-[#e5e5e5]">
                                <th className="px-6 py-4 text-xs font-bold text-[#525252] uppercase tracking-wider">Parking Name & ID</th>
                                <th className="px-6 py-4 text-xs font-bold text-[#525252] uppercase tracking-wider">Owner Details</th>
                                <th className="px-6 py-4 text-xs font-bold text-[#525252] uppercase tracking-wider">Submission Info</th>
                                <th className="px-6 py-4 text-xs font-bold text-[#525252] uppercase tracking-wider">Status</th>
                                <th className="px-6 py-4 text-xs font-bold text-[#525252] uppercase tracking-wider text-right">Review Action</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-[#e5e5e5]">
                            {pendingLots.map((lot) => (
                                <tr key={lot.id} className="hover:bg-[#fcfcfc] transition-colors group">
                                    <td className="px-6 py-5">
                                        <div className="flex items-center gap-4">
                                            <div className="w-10 h-10 rounded-xl bg-[#f5f5f5] flex items-center justify-center shrink-0 border border-[#e5e5e5] group-hover:border-[#d4d4d4] transition-colors">
                                                <svg className="w-5 h-5 text-[#525252]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                                                </svg>
                                            </div>
                                            <div>
                                                <p className="text-[15px] font-bold text-[#171717]">{lot.parkingName}</p>
                                                <p className="text-[13px] text-[#a3a3a3] mt-0.5 font-medium">{lot.id}</p>
                                            </div>
                                        </div>
                                    </td>
                                    <td className="px-6 py-5">
                                        <p className="text-[14px] font-bold text-[#171717]">{lot.owner.name}</p>
                                        <p className="text-[13px] text-[#525252] mt-0.5">{lot.owner.email}</p>
                                        <p className="text-[12px] text-[#a3a3a3] mt-0.5">{lot.owner.phone}</p>
                                    </td>
                                    <td className="px-6 py-5">
                                        <div className="flex flex-col gap-1.5">
                                            <div className="flex items-center text-[13px] text-[#525252]">
                                                <svg className="w-4 h-4 mr-1.5 text-[#a3a3a3]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                                                </svg>
                                                {lot.submissionInfo.date} at {lot.submissionInfo.time}
                                            </div>
                                            <div className="flex items-center text-[12px] text-[#1e19d8] font-semibold bg-blue-50 w-fit px-2 py-0.5 rounded-md">
                                                <svg className="w-3.5 h-3.5 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                                                </svg>
                                                {lot.submissionInfo.documents} Documents
                                            </div>
                                        </div>
                                    </td>
                                    <td className="px-6 py-5">
                                        <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold border
                                            ${lot.status === 'Pending Review' ? 'bg-amber-50 text-amber-700 border-amber-200' : 
                                              lot.status === 'Under Review' ? 'bg-blue-50 text-blue-700 border-blue-200' :
                                              'bg-red-50 text-red-700 border-red-200'}`}>
                                            <span className={`w-1.5 h-1.5 rounded-full mr-1.5
                                                ${lot.status === 'Pending Review' ? 'bg-amber-500' : 
                                                  lot.status === 'Under Review' ? 'bg-blue-500' :
                                                  'bg-red-500'}`}></span>
                                            {lot.status}
                                        </span>
                                    </td>
                                    <td className="px-6 py-5 text-right">
                                        <Link href={`/admin/parking-lots/pending/${lot.id}`} className="inline-flex items-center justify-center px-4 py-2 border border-transparent text-sm font-bold rounded-lg text-[#171717] bg-[#eafe89] hover:bg-[#d4f55a] transition-all focus:outline-none focus:ring-4 focus:ring-[#eafe89]/30">
                                            Review
                                            <svg className="w-4 h-4 ml-1.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                                            </svg>
                                        </Link>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
                
                {/* Pagination (dummy) */}
                <div className="px-6 py-4 border-t border-[#e5e5e5] bg-white flex items-center justify-between">
                    <p className="text-sm text-[#525252]">Showing <span className="font-bold text-[#171717]">1</span> to <span className="font-bold text-[#171717]">4</span> of <span className="font-bold text-[#171717]">4</span> pending submissions</p>
                    <div className="flex gap-1">
                        <button disabled className="p-2 border border-[#e5e5e5] rounded-lg text-[#a3a3a3] cursor-not-allowed">
                            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" /></svg>
                        </button>
                        <button className="px-3.5 py-1.5 border border-[#171717] bg-[#171717] text-white rounded-lg text-sm font-bold">1</button>
                        <button disabled className="p-2 border border-[#e5e5e5] rounded-lg text-[#a3a3a3] cursor-not-allowed">
                            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" /></svg>
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}
