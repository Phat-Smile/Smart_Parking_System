"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Bell, Wallet } from "lucide-react";
import { vi } from "../../i18n/vi";
import { getMyVehicles, type RegisteredVehicle } from "../../myVehiclesApi";
import { getMyWallet } from "../../wallet/walletApi";
import { getMyNotifications } from "../../customerNotificationApi";

export function TopBar() {
    const [vehicles, setVehicles] = useState<RegisteredVehicle[]>([]);
    const [balance, setBalance] = useState<number | null>(null);
    const [unreadCount, setUnreadCount] = useState(0);

    useEffect(() => {
        getMyVehicles().then(res => setVehicles(res.vehicles || [])).catch(() => {});
        getMyWallet().then(w => setBalance(w.balance)).catch(() => {});
        getMyNotifications(true, 1, 1).then(p => setUnreadCount(p.unreadCount)).catch(() => {});
    }, []);

    return (
        <header className="hidden md:flex items-center justify-between h-20 px-8 bg-[#03010e]/80 backdrop-blur-md border-b border-white/10 sticky top-0 z-50">
            <div className="flex items-center gap-4">
                <div className="bg-[#101426] px-4 py-2 rounded-xl border border-white/10 flex items-center gap-2">
                    <span className="text-[#8790a3] text-sm">
                        Xe đang chọn:
                    </span>
                    <select className="bg-transparent text-white font-medium outline-none cursor-pointer">
                        {vehicles.length === 0 ? (
                            <option value="none" className="bg-[#0b0c17] text-white">
                                Chưa có xe
                            </option>
                        ) : (
                            <>
                                <option value="all" className="bg-[#0b0c17] text-white">
                                    Tất cả xe
                                </option>
                                {vehicles.map((v) => (
                                    <option
                                        key={v.vehicleId}
                                        value={v.licensePlate}
                                        className="bg-[#0b0c17] text-white"
                                    >
                                        {v.licensePlate} ({v.vehicleType === "Car" ? "Ô tô" : v.vehicleType === "Motorbike" ? "Xe máy" : "Xe đạp"})
                                    </option>
                                ))}
                            </>
                        )}
                    </select>
                </div>
            </div>

            <div className="flex items-center gap-6">
                <Link
                    href="/dashboard/payments"
                    className="flex items-center gap-3 bg-[#15182e] px-4 py-2 rounded-xl border border-white/10 hover:border-[#6246ea]/50 transition-colors"
                >
                    <div className="flex flex-col">
                        <span className="text-xs text-[#8790a3]">
                            {vi.wallet.balance}
                        </span>
                        <span className="text-sm font-bold text-white">
                            {balance !== null
                                ? new Intl.NumberFormat("vi-VN", {
                                      style: "currency",
                                      currency: "VND",
                                  }).format(balance)
                                : "—"}
                        </span>
                    </div>
                    <button
                        className="bg-[#6246ea] text-white p-2 rounded-lg hover:bg-[#7c5cff] transition-colors"
                        title={vi.wallet.topUp}
                    >
                        <Wallet size={16} />
                    </button>
                </Link>

                <Link
                    href="/dashboard/notifications"
                    className="relative p-2 text-[#d5d8e3] hover:text-white transition-colors bg-[#101426] rounded-xl border border-white/10 hover:border-white/20"
                >
                    <Bell size={20} />
                    {unreadCount > 0 && (
                        <span className="absolute top-1 right-1 w-2 h-2 bg-[#ff9388] rounded-full"></span>
                    )}
                </Link>
            </div>
        </header>
    );
}
