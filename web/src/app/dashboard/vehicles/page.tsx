import { vi } from "../../../features/customer/i18n/vi";

export default function VehiclesPage() {
    return (
        <div className="animate-in fade-in duration-500 space-y-6">
            <h1 className="text-2xl font-bold text-white">{vi.dashboard.vehicles}</h1>
            <div className="bg-[#0b0c17] border border-white/10 rounded-2xl p-6 shadow-card-light h-64 flex items-center justify-center text-[#8790a3]">
                Nội dung trang Xe của tôi sẽ nằm ở đây...
            </div>
        </div>
    );
}
