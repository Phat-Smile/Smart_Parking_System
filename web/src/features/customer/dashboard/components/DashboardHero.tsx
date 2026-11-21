"use client";

import { useEffect, useState } from "react";
import { useDashboardStore, HeroState } from "../store/useDashboardStore";
import { vi } from "../../i18n/vi";
import {
    Search,
    MapPin,
    Clock,
    Info,
    QrCode,
    AlertTriangle,
    X,
} from "lucide-react";
import QRCode from "react-qr-code";
import {
    getMyParkingSessions,
    type MyParkingSession,
} from "../../mySessionsApi";
import {
    getMyReservations,
    type ReservationSummaryDto,
} from "../../booking/bookingApi";

function formatTime(seconds: number) {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m}:${s.toString().padStart(2, "0")}`;
}

export function DashboardHero() {
    const { heroState, vehicleType, countdown, setCountdown } =
        useDashboardStore();
    const [qrModalOpen, setQrModalOpen] = useState(false);
    const [activeSession, setActiveSession] = useState<MyParkingSession | null>(
        null,
    );
    const [activeBooking, setActiveBooking] =
        useState<ReservationSummaryDto | null>(null);

    useEffect(() => {
        getMyParkingSessions()
            .then((sessions) => {
                if (sessions && sessions.length > 0) {
                    setActiveSession(sessions[0]);
                }
            })
            .catch(() => {});
        getMyReservations()
            .then((reservations) => {
                const upcoming = (reservations || []).find(
                    (r) =>
                        r.status === "Confirmed" ||
                        r.status === "PendingPayment",
                );
                if (upcoming) setActiveBooking(upcoming);
            })
            .catch(() => {});
    }, []);

    useEffect(() => {
        if (
            (heroState === "holding" || heroState === "paid") &&
            countdown > 0
        ) {
            const timer = setInterval(() => setCountdown(countdown - 1), 1000);
            return () => clearInterval(timer);
        }
    }, [heroState, countdown, setCountdown]);

    const renderEmpty = () => (
        <div className="flex flex-col md:flex-row items-center gap-4">
            <button className="flex-1 w-full dash-btn-primary py-4 rounded-2xl flex items-center justify-center gap-2 shadow-sm">
                <Search size={20} />
                {vi.hero.empty.findSpot}
            </button>
            <div className="flex-1 w-full flex gap-4">
                <button
                    disabled={vehicleType === "motorcycle"}
                    className="flex-1 bg-[var(--dash-bg)] border border-[var(--dash-border)] text-[var(--dash-text)] py-4 rounded-2xl font-bold hover:brightness-95 dark:hover:brightness-110 transition-all disabled:opacity-50 disabled:cursor-not-allowed group relative"
                >
                    {vi.hero.empty.reserveSpot}
                    {vehicleType === "motorcycle" && (
                        <div className="absolute -top-10 left-1/2 -translate-x-1/2 whitespace-nowrap bg-black text-white text-xs py-1 px-2 rounded opacity-0 group-hover:opacity-100 transition-opacity">
                            {vi.hero.empty.motorcycleTooltip}
                        </div>
                    )}
                </button>
                <button
                    disabled={vehicleType === "motorcycle"}
                    className="flex-1 bg-[var(--dash-bg)] border border-[var(--dash-border)] text-[var(--dash-text)] py-4 rounded-2xl font-bold hover:brightness-95 dark:hover:brightness-110 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                >
                    {vi.hero.empty.bookAhead}
                </button>
            </div>
        </div>
    );

    const renderHolding = () => (
        <div className="flex flex-col md:flex-row items-center justify-between gap-6">
            <div>
                <h3 className="text-xl font-bold text-[var(--dash-text)] mb-2">
                    {vi.hero.holding.title}
                </h3>
                <p className="text-[var(--dash-text-muted)] flex items-center gap-2">
                    <Clock size={16} />
                    {vi.hero.holding.description}{" "}
                    <strong className="text-[var(--dash-text)] text-lg">
                        {formatTime(countdown)}
                    </strong>
                </p>
            </div>
            <div className="flex gap-4 w-full md:w-auto">
                <button className="flex-1 md:flex-none px-6 py-3 bg-transparent border border-red-400 text-red-500 dark:text-red-400 rounded-xl font-bold hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors">
                    {vi.hero.holding.cancel}
                </button>
                <button className="flex-1 md:flex-none px-8 py-3 dash-btn-primary rounded-xl shadow-sm">
                    Thanh toán cọc
                </button>
            </div>
        </div>
    );

    const renderConfirmed = () => (
        <div className="flex flex-col gap-4">
            <div className="flex justify-between items-start">
                <div>
                    <h3 className="text-2xl font-bold text-[var(--dash-text)] mb-1 flex items-center gap-2">
                        {activeBooking?.lotId || "Chưa có đặt chỗ"}{" "}
                        <Badge type="success">Đã xác nhận</Badge>
                    </h3>
                    <p className="text-[var(--dash-text-muted)] flex items-center gap-2 mt-2">
                        <MapPin size={16} />{" "}
                        {activeBooking ? "Vị trí bãi đỗ xe" : "Chưa có địa chỉ"}
                    </p>
                    <p className="text-[var(--dash-text)] mt-3 font-medium">
                        {activeBooking?.appointmentTime
                            ? `Giờ hẹn: ${new Date(activeBooking.appointmentTime).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })} - ${new Date(activeBooking.appointmentTime).toLocaleDateString("vi-VN")}`
                            : "Chưa có lịch hẹn"}
                    </p>
                </div>
                <button className="px-4 py-2 border border-[var(--dash-border)] text-[var(--dash-text)] rounded-lg text-sm hover:bg-[var(--dash-text)]/5 transition-colors">
                    {vi.hero.confirmed.cancel}
                </button>
            </div>
            <div className="mt-4 p-3 bg-[var(--dash-primary)]/10 border border-[var(--dash-primary)]/20 rounded-xl flex items-start gap-3 text-sm">
                <Info
                    size={18}
                    className="text-[var(--dash-primary)] flex-shrink-0 mt-0.5"
                />
                <div className="text-[var(--dash-text-muted)]">
                    <p>{vi.hero.confirmed.reserveNote}</p>
                    <p className="mt-1 text-[var(--dash-text)]">
                        {vi.hero.confirmed.assignedAtGate}
                    </p>
                </div>
            </div>
        </div>
    );

    const renderActive = () => (
        <div className="flex flex-col md:flex-row justify-between gap-6">
            <div className="flex-1">
                <div className="flex items-center gap-3 mb-4">
                    <div className="bg-[var(--dash-bg)] border border-[var(--dash-border)] px-3 py-1 rounded-lg">
                        <span className="text-xs text-[var(--dash-text-muted)] block">
                            {vi.hero.active.plate}
                        </span>
                        <span className="font-mono font-bold text-lg text-[var(--dash-text)]">
                            {activeSession?.plateNumber || "Chưa có xe"}
                        </span>
                    </div>
                    <div className="bg-[var(--dash-primary)]/10 border border-[var(--dash-primary)]/30 px-3 py-1 rounded-lg">
                        <span className="text-xs text-[var(--dash-primary)] block">
                            {vi.hero.active.spot}
                        </span>
                        <span className="font-mono font-bold text-lg text-[var(--dash-text)]">
                            {activeSession?.zoneCode ||
                                activeSession?.floorName ||
                                "Đỗ tự do"}
                        </span>
                    </div>
                </div>
                <div className="grid grid-cols-2 gap-4 text-sm">
                    <div>
                        <span className="text-[var(--dash-text-muted)]">
                            {vi.hero.active.entryTime}
                        </span>
                        <p className="font-bold text-[var(--dash-text)]">
                            {activeSession?.entryTime
                                ? new Date(
                                      activeSession.entryTime,
                                  ).toLocaleTimeString("vi-VN", {
                                      hour: "2-digit",
                                      minute: "2-digit",
                                  })
                                : "—"}
                        </p>
                    </div>
                    <div>
                        <span className="text-[var(--dash-text-muted)]">
                            {vi.hero.active.duration}
                        </span>
                        <p className="font-bold text-[var(--dash-text)]">
                            {activeSession?.entryTime ? "Đang đỗ" : "—"}
                        </p>
                    </div>
                    <div>
                        <span className="text-[var(--dash-text-muted)]">
                            {vi.hero.active.prepaid}
                        </span>
                        <p className="font-bold text-[var(--dash-text)]">0đ</p>
                    </div>
                    <div>
                        <span className="text-[var(--dash-text-muted)]">
                            {vi.hero.active.tempFee}
                        </span>
                        <p className="font-bold text-red-500">Đang tính toán</p>
                    </div>
                </div>
            </div>
            <div className="flex flex-col gap-3 justify-end md:w-48">
                <button
                    onClick={() => setQrModalOpen(true)}
                    className="w-full py-3 bg-[var(--dash-text)] text-[var(--dash-bg)] rounded-xl font-bold flex items-center justify-center gap-2 hover:opacity-90 transition-opacity"
                >
                    <QrCode size={18} />
                    {vi.hero.active.showQr}
                </button>
                <button className="w-full py-3 dash-btn-primary rounded-xl shadow-sm">
                    {vi.hero.active.pay}
                </button>
            </div>
        </div>
    );

    const renderPaid = () => (
        <div className="flex flex-col items-center text-center py-4">
            <div className="w-16 h-16 bg-emerald-500/10 text-emerald-500 rounded-full flex items-center justify-center mb-4">
                <svg
                    className="w-8 h-8"
                    fill="none"
                    viewBox="0 0 24 24"
                    stroke="currentColor"
                >
                    <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M5 13l4 4L19 7"
                    />
                </svg>
            </div>
            <h3 className="text-2xl font-bold text-[var(--dash-text)] mb-2">
                {vi.hero.paid.title}
            </h3>
            <p className="text-[var(--dash-text-muted)] mb-1">
                {vi.hero.paid.exitIn}{" "}
                <strong className="text-[var(--dash-text)] text-xl mx-1">
                    {formatTime(countdown)}
                </strong>
            </p>
            <p className="text-sm text-red-500 flex items-center gap-1 mt-2">
                <AlertTriangle size={14} /> {vi.hero.paid.warning}
            </p>
            <button
                onClick={() => setQrModalOpen(true)}
                className="mt-6 px-6 py-2 border border-[var(--dash-border)] text-[var(--dash-text)] rounded-lg hover:bg-[var(--dash-text)]/5 transition-colors flex items-center gap-2"
            >
                <QrCode size={16} /> Hiện QR ra cổng
            </button>
        </div>
    );

    const renderOverstay = () => (
        <div className="bg-red-500/10 border border-red-500/20 rounded-2xl p-6 flex flex-col md:flex-row items-center gap-6">
            <div className="w-12 h-12 bg-red-500/20 text-red-500 rounded-full flex items-center justify-center flex-shrink-0">
                <AlertTriangle size={24} />
            </div>
            <div className="flex-1 text-center md:text-left">
                <h3 className="text-xl font-bold text-[var(--dash-text)] mb-1">
                    {vi.hero.overstay.title}
                </h3>
                <p className="text-red-500">{vi.hero.overstay.description}</p>
                <p className="text-[var(--dash-text-muted)] text-sm mt-2">
                    {vi.hero.overstay.contact}
                </p>
            </div>
            <button className="w-full md:w-auto px-8 py-3 bg-red-500 text-white rounded-xl font-bold hover:bg-red-600 transition-colors">
                Thanh toán phát sinh
            </button>
        </div>
    );

    return (
        <>
            <div className="dash-card p-6 md:p-8 relative overflow-hidden h-full flex items-center">
                <div className="absolute top-0 right-0 w-64 h-64 bg-[var(--dash-primary)] opacity-[0.08] blur-[80px] rounded-full pointer-events-none"></div>

                <div className="relative z-10 w-full">
                    {heroState === "empty" && renderEmpty()}
                    {heroState === "holding" && renderHolding()}
                    {heroState === "confirmed" && renderConfirmed()}
                    {heroState === "active" && renderActive()}
                    {heroState === "paid" && renderPaid()}
                    {heroState === "overstay" && renderOverstay()}
                </div>
            </div>

            {/* QR Modal - Luôn nền trắng, mã đen */}
            {qrModalOpen && (
                <div className="fixed inset-0 bg-black/80 backdrop-blur-sm flex items-center justify-center z-[100] p-4">
                    <div className="bg-white rounded-[2rem] p-8 max-w-sm w-full relative flex flex-col items-center">
                        <button
                            onClick={() => setQrModalOpen(false)}
                            className="absolute top-4 right-4 w-11 h-11 min-w-[44px] min-h-[44px] flex items-center justify-center rounded-full bg-black/5 text-black hover:bg-black/10 transition-colors"
                            aria-label="Đóng"
                        >
                            <X className="w-5 h-5" />
                        </button>
                        <h4 className="text-xl font-bold text-black mb-1">
                            QR Phiên Đỗ
                        </h4>
                        <p className="text-gray-500 text-sm mb-6 text-center">
                            Dùng để ra cổng,{" "}
                            <strong className="text-red-600">
                                không dùng để trả tiền
                            </strong>
                        </p>

                        <div
                            className="bg-white p-4 rounded-xl shadow-lg border border-gray-100 w-full flex justify-center mb-6"
                            style={{ filter: "brightness(1.1) contrast(1.2)" }}
                        >
                            <QRCode
                                value={
                                    activeSession?.qrToken ||
                                    activeSession?.sessionId ||
                                    "no-session"
                                }
                                size={200}
                            />
                        </div>

                        <div className="w-full bg-gray-50 rounded-xl p-4 text-center border border-gray-100">
                            <span className="text-sm text-gray-500 block mb-1">
                                Biển số
                            </span>
                            <span className="text-2xl font-mono font-bold text-black">
                                {activeSession?.plateNumber || "Chưa có xe"}
                            </span>
                        </div>
                    </div>
                </div>
            )}
        </>
    );
}

function Badge({
    children,
    type = "default",
}: {
    children: React.ReactNode;
    type?: "default" | "success" | "warning";
}) {
    const colors = {
        default: "bg-[var(--dash-text)]/5 text-[var(--dash-text)]",
        success: "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400",
        warning: "bg-amber-500/10 text-amber-600 dark:text-amber-400",
    };
    return (
        <span
            className={`px-2.5 py-1 rounded-full text-xs font-semibold tracking-wide ${colors[type]}`}
        >
            {children}
        </span>
    );
}
