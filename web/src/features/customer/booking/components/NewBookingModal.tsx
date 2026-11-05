"use client";

import { en } from "../../i18n/en";
import { X, Calendar, Clock, Car, MapPin } from "lucide-react";
import { useState } from "react";

interface NewBookingModalProps {
    isOpen: boolean;
    onClose: () => void;
}

export function NewBookingModal({ isOpen, onClose }: NewBookingModalProps) {
    const [date, setDate] = useState("");
    const [time, setTime] = useState("");

    if (!isOpen) return null;

    // Lấy ngày hiện tại và tính ngày tối đa (7 ngày sau)
    const today = new Date();
    const maxDate = new Date();
    maxDate.setDate(today.getDate() + 7);

    const todayStr = today.toISOString().split("T")[0];
    const maxDateStr = maxDate.toISOString().split("T")[0];

    return (
        <div className="fixed inset-0 bg-black/80 backdrop-blur-sm flex items-center justify-center z-[100] p-4">
            <div className="bg-[#0b0c17] border border-white/10 rounded-[2rem] p-6 max-w-md w-full relative shadow-card-dark animate-in fade-in zoom-in duration-200">
                <button
                    onClick={onClose}
                    className="absolute top-6 right-6 text-[#8790a3] hover:text-white transition-colors"
                >
                    <X size={24} />
                </button>

                <h2 className="text-xl font-bold text-white mb-6">
                    {en.bookingsPage.newBooking}
                </h2>

                <div className="space-y-4">
                    {/* Bãi đỗ */}
                    <div>
                        <label className="block text-sm text-[#8790a3] mb-1.5 flex items-center gap-2">
                            <MapPin size={16} /> Enter a parking lot or address
                        </label>
                        <input
                            type="text"
                            placeholder="e.g. Vincom Center Dong Khoi..."
                            className="w-full bg-[#101426] border border-white/10 rounded-xl px-4 py-3 text-white outline-none focus:border-[#6246ea] transition-colors placeholder-[#8790a3]/50"
                        />
                    </div>

                    {/* Chọn xe */}
                    <div className="grid grid-cols-3 gap-4">
                        <div className="col-span-2">
                            <label className="block text-sm text-[#8790a3] mb-1.5 flex items-center gap-2">
                                <Car size={16} /> Enter license plate
                            </label>
                            <input
                                type="text"
                                placeholder="e.g. 29A-12345"
                                className="w-full bg-[#101426] border border-white/10 rounded-xl px-4 py-3 text-white outline-none focus:border-[#6246ea] transition-colors placeholder-[#8790a3]/50 uppercase"
                            />
                        </div>
                        <div className="col-span-1">
                            <label className="block text-sm text-[#8790a3] mb-1.5">
                                Vehicle type
                            </label>
                            <select className="w-full bg-[#101426] border border-white/10 rounded-xl px-4 py-3 text-white outline-none focus:border-[#6246ea] transition-colors">
                                <option value="car" className="bg-[#0b0c17]">
                                    Car
                                </option>
                                <option
                                    value="motorcycle"
                                    className="bg-[#0b0c17]"
                                    disabled
                                >
                                    Motorbike
                                </option>
                            </select>
                        </div>
                    </div>

                    {/* Ngày giờ */}
                    <div className="grid grid-cols-2 gap-4">
                        <div>
                            <label className="block text-sm text-[#8790a3] mb-1.5 flex items-center gap-2">
                                <Calendar size={16} />{" "}
                                {en.bookingsPage.form.date}
                            </label>
                            <input
                                type="date"
                                min={todayStr}
                                max={maxDateStr}
                                value={date}
                                onChange={(e) => setDate(e.target.value)}
                                className="w-full bg-[#101426] border border-white/10 rounded-xl px-4 py-3 text-white outline-none focus:border-[#6246ea] transition-colors"
                            />
                        </div>
                        <div>
                            <label className="block text-sm text-[#8790a3] mb-1.5 flex items-center gap-2">
                                <Clock size={16} /> {en.bookingsPage.form.time}
                            </label>
                            <input
                                type="time"
                                value={time}
                                onChange={(e) => setTime(e.target.value)}
                                className="w-full bg-[#101426] border border-white/10 rounded-xl px-4 py-3 text-white outline-none focus:border-[#6246ea] transition-colors [color-scheme:dark]"
                            />
                        </div>
                    </div>
                </div>

                <div className="mt-6 p-3 bg-[#6246ea]/10 border border-[#6246ea]/20 rounded-xl text-xs text-[#d5d8e3]">
                    <ul className="list-disc pl-4 space-y-1">
                        <li>Book up to 7 days ahead.</li>
                        <li>Available for cars only.</li>
                        <li>Booking deposit: 20,000 VND (deducted from your wallet).</li>
                    </ul>
                </div>

                <button
                    className="w-full mt-6 bg-[#6246ea] text-white py-4 rounded-xl font-bold hover:bg-[#7c5cff] transition-colors shadow-glow-purple disabled:opacity-50"
                    disabled={!date || !time}
                    onClick={() => {
                        // Demo action
                        onClose();
                    }}
                >
                    {en.bookingsPage.form.submit}
                </button>
            </div>
        </div>
    );
}
