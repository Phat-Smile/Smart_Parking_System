import { vi } from "../../../features/customer/i18n/vi";

export default function ProfilePage() {
    return (
        <div className="animate-in fade-in duration-500 space-y-6">
            <h1 className="text-2xl font-bold text-white">
                {vi.dashboard.profile}
            </h1>
            <div className="bg-[#0b0c17] border border-white/10 rounded-2xl p-6 shadow-card-light h-64 flex items-center justify-center text-[#8790a3]">
                Nội dung trang Hồ sơ cá nhân sẽ nằm ở đây...
            </div>
        </div>
    );
}
