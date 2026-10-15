import { vi } from "../../../features/customer/i18n/vi";
import { Search, MapPin } from "lucide-react";

export default function SearchPage() {
    return (
        <div className="animate-in fade-in duration-500 space-y-6">
            <h1 className="text-2xl font-bold text-white mb-6">{vi.dashboard.search}</h1>
            
            {/* Thanh tìm kiếm */}
            <div className="relative">
                <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                    <Search className="h-5 w-5 text-[#8790a3]" />
                </div>
                <input 
                    type="text" 
                    className="block w-full pl-11 pr-4 py-4 bg-[#0b0c17] border border-white/10 rounded-2xl text-white placeholder-[#8790a3] focus:outline-none focus:border-[#6246ea] focus:ring-1 focus:ring-[#6246ea] transition-all shadow-card-light"
                    placeholder={vi.searchPage.placeholder}
                />
                <button className="absolute inset-y-2 right-2 bg-[#6246ea] text-white px-6 rounded-xl font-bold hover:bg-[#7c5cff] transition-colors shadow-glow-purple">
                    {vi.searchPage.button}
                </button>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Lịch sử hoặc Gợi ý */}
                <div className="lg:col-span-1 space-y-4">
                    <div className="bg-[#0b0c17] border border-white/10 rounded-2xl p-6 shadow-card-light h-[400px]">
                        <h3 className="text-lg font-bold text-white mb-4">{vi.searchPage.recent}</h3>
                        <ul className="space-y-3">
                            {[1, 2, 3].map((i) => (
                                <li key={i} className="flex items-start gap-3 p-3 rounded-xl hover:bg-white/5 cursor-pointer transition-colors border border-transparent hover:border-white/10">
                                    <div className="bg-[#101426] p-2 rounded-lg text-[#8790a3]">
                                        <MapPin size={18} />
                                    </div>
                                    <div>
                                        <p className="text-white font-medium">Vincom Center Đồng Khởi</p>
                                        <p className="text-sm text-[#8790a3]">72 Lê Thánh Tôn, Quận 1</p>
                                    </div>
                                </li>
                            ))}
                        </ul>
                    </div>
                </div>

                {/* Bản đồ */}
                <div className="lg:col-span-2">
                    <div className="bg-[#0b0c17] border border-white/10 rounded-2xl overflow-hidden shadow-card-light h-[400px] flex items-center justify-center relative">
                        {/* Placeholder Map Pattern */}
                        <div className="absolute inset-0 opacity-[0.03]" style={{ backgroundImage: 'radial-gradient(circle at 2px 2px, white 1px, transparent 0)', backgroundSize: '24px 24px' }}></div>
                        <div className="text-center z-10">
                            <MapPin className="h-12 w-12 text-[#6246ea] mx-auto mb-3 opacity-50" />
                            <p className="text-[#8790a3]">Bản đồ Mapbox sẽ hiển thị ở đây</p>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
