"use client";

import { useState } from "react";
import { vi } from "../../../features/customer/i18n/vi";
import { BookingBadge, BookingStatus } from "../../../features/customer/booking/components/BookingBadge";
import { NewBookingModal } from "../../../features/customer/booking/components/NewBookingModal";
import { Plus, Calendar, MapPin, Clock, Car } from "lucide-react";

// Mock data for bookings
const mockBookings: { id: string, parkingName: string, address: string, plate: string, date: string, time: string, status: BookingStatus }[] = [
    {
        id: "BK-1001",
        parkingName: "Vincom Center",
        address: "72 Lê Thánh Tôn, Q1",
        plate: "29A-12345",
        date: "Hôm nay",
        time: "14:30",
        status: "confirmed"
    },
    {
        id: "BK-1002",
        parkingName: "Saigon Centre",
        address: "65 Lê Lợi, Q1",
        plate: "29A-12345",
        date: "Hôm nay",
        time: "16:00",
        status: "holding"
    },
    {
        id: "BK-0999",
        parkingName: "Bitexco",
        address: "2 Hải Triều, Q1",
        plate: "29A-12345",
        date: "Hôm qua",
        time: "09:00",
        status: "used"
    }
];

export default function BookingsPage() {
    const activeCount = mockBookings.filter(b => b.status === 'confirmed' || b.status === 'holding').length;
    const [isModalOpen, setIsModalOpen] = useState(false);

    return (
        <div className="animate-in fade-in duration-500 space-y-6">
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-white mb-1">{vi.bookingsPage.title}</h1>
                    <p className="text-[#8790a3] text-sm">
                        <strong className="text-white">{activeCount}/3</strong> {vi.bookingsPage.activeCount}
                    </p>
                </div>
                <button 
                    disabled={activeCount >= 3}
                    onClick={() => setIsModalOpen(true)}
                    className="bg-[#6246ea] text-white px-6 py-3 rounded-xl font-bold flex items-center justify-center gap-2 hover:bg-[#7c5cff] transition-colors disabled:opacity-50 disabled:cursor-not-allowed shadow-glow-purple"
                >
                    <Plus size={20} />
                    {vi.bookingsPage.newBooking}
                </button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                {mockBookings.map(booking => (
                    <div key={booking.id} className="bg-[#0b0c17] border border-white/10 rounded-2xl p-6 shadow-card-light hover:border-white/20 transition-colors flex flex-col h-full">
                        <div className="flex justify-between items-start mb-4">
                            <BookingBadge status={booking.status} />
                            <span className="text-[#8790a3] text-xs font-mono">{booking.id}</span>
                        </div>
                        
                        <h3 className="text-lg font-bold text-white mb-1 truncate">{booking.parkingName}</h3>
                        <p className="text-[#8790a3] text-sm flex items-center gap-2 mb-6 truncate">
                            <MapPin size={14} className="flex-shrink-0" /> {booking.address}
                        </p>

                        <div className="mt-auto space-y-3 bg-[#101426] rounded-xl p-4 border border-white/5">
                            <div className="flex justify-between text-sm">
                                <span className="text-[#8790a3] flex items-center gap-2"><Car size={14}/> Xe</span>
                                <span className="text-white font-mono font-medium">{booking.plate}</span>
                            </div>
                            <div className="flex justify-between text-sm">
                                <span className="text-[#8790a3] flex items-center gap-2"><Calendar size={14}/> Ngày</span>
                                <span className="text-white font-medium">{booking.date}</span>
                            </div>
                            <div className="flex justify-between text-sm">
                                <span className="text-[#8790a3] flex items-center gap-2"><Clock size={14}/> Giờ vào</span>
                                <span className="text-white font-medium">{booking.time}</span>
                            </div>
                        </div>

                        {(booking.status === 'confirmed' || booking.status === 'holding') && (
                            <button className="w-full mt-4 py-2.5 border border-[#ff9388] text-[#ff9388] rounded-xl font-bold hover:bg-[#ff9388]/10 transition-colors">
                                Hủy đặt chỗ
                            </button>
                        )}
                    </div>
                ))}
            </div>

            <NewBookingModal 
                isOpen={isModalOpen} 
                onClose={() => setIsModalOpen(false)} 
            />
        </div>
    );
}
