"use client";

import React, { useEffect, useRef, useState, useCallback } from "react";
import Link from "next/link";
import {
    Bell,
    AlertTriangle,
    Car,
    Zap,
    CheckCheck,
    ArrowRight,
    CheckCircle2,
} from "lucide-react";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { useStaffData } from "./staffData";
import { listPendingGateReadings } from "./staffApi";

interface NotificationItem {
    id: string;
    type: "ocr" | "session" | "barrier" | "system";
    titleVi: string;
    titleEn: string;
    descVi: string;
    descEn: string;
    timeVi: string;
    timeEn: string;
    href: string;
    read: boolean;
}

export function StaffNotifications() {
    const { language } = useLanguage();
    const isVi = language === "vi";
    const [isOpen, setIsOpen] = useState(false);
    const [items, setItems] = useState<NotificationItem[]>([]);
    const dropdownRef = useRef<HTMLDivElement>(null);

    const { issues, activeSessions, assignedLot } = useStaffData();
    const pendingIssuesCount = issues.length;

    const activeSessionsRef = useRef(activeSessions);
    useEffect(() => {
        activeSessionsRef.current = activeSessions;
    }, [activeSessions]);

    const lotId = assignedLot?.lotId;
    const loadRealNotifications = useCallback(async () => {
        // Always yield first so the state update below never runs synchronously inside an effect.
        await Promise.resolve();
        const notifs: NotificationItem[] = [];

        // 1. Kiểm tra sự cố OCR thực tế từ CSDL backend
        if (lotId) {
            try {
                const pendingReadings = await listPendingGateReadings(lotId);
                if (Array.isArray(pendingReadings)) {
                    pendingReadings.forEach((reading) => {
                        const plateStr = reading.observedPlate || "Chưa rõ";
                        notifs.push({
                            id: `ocr-${reading.incidentId}`,
                            type: "ocr",
                            titleVi: "Cần xác thực biển số xe OCR",
                            titleEn: "OCR plate verification needed",
                            descVi: `Biển số ${plateStr} có độ tin cậy thấp (${(reading.confidence * 100).toFixed(0)}%). Cần nhân viên xác nhận.`,
                            descEn: `Plate ${plateStr} has low OCR confidence (${(reading.confidence * 100).toFixed(0)}%). Staff confirmation required.`,
                            timeVi: "Chờ xử lý",
                            timeEn: "Pending",
                            href: "/staff/verification",
                            read: false,
                        });
                    });
                }
            } catch {
                // ignore network error
            }
        }

        // 2. Kiểm tra phương tiện đỗ xe quá hạn 24 giờ từ danh sách phiên thực tế
        const now = Date.now();
        const currentSessions = activeSessionsRef.current;
        currentSessions.forEach((s) => {
            const entryTime = new Date(s.entryTime).getTime();
            if (
                !Number.isNaN(entryTime) &&
                now - entryTime > 24 * 3600 * 1000
            ) {
                const hoursParked = Math.floor(
                    (now - entryTime) / (3600 * 1000),
                );
                notifs.push({
                    id: `overtime-${s.id}`,
                    type: "session",
                    titleVi: "Phương tiện đỗ quá hạn 24 giờ",
                    titleEn: "Vehicle parked over 24 hours",
                    descVi: `Xe ${s.plateNumber} đã đỗ ${hoursParked} giờ trong bãi${s.slotCode ? ` (Vị trí: ${s.slotCode})` : ""}.`,
                    descEn: `Vehicle ${s.plateNumber} has parked for ${hoursParked}h${s.slotCode ? ` (Slot: ${s.slotCode})` : ""}.`,
                    timeVi: `${hoursParked}h trước`,
                    timeEn: `${hoursParked}h ago`,
                    href: "/staff/sessions",
                    read: false,
                });
            }
        });

        setItems((prev) => {
            if (
                prev.length === notifs.length &&
                prev.every((p, idx) => p.id === notifs[idx]?.id)
            ) {
                return prev;
            }
            return notifs;
        });
    }, [lotId]);

    useEffect(() => {
        let active = true;
        // Run in a microtask: the loader updates state, which must not happen synchronously in the effect.
        void Promise.resolve().then(() => {
            if (active) void loadRealNotifications();
        });
        return () => {
            active = false;
        };
    }, [loadRealNotifications, activeSessions.length]);

    const unreadCount = items.filter((n) => !n.read).length;

    // Close on click outside
    useEffect(() => {
        function handleClickOutside(event: MouseEvent) {
            if (
                dropdownRef.current &&
                !dropdownRef.current.contains(event.target as Node)
            ) {
                setIsOpen(false);
            }
        }
        if (isOpen) {
            document.addEventListener("mousedown", handleClickOutside);
        }
        return () => {
            document.removeEventListener("mousedown", handleClickOutside);
        };
    }, [isOpen]);

    function markAllAsRead() {
        setItems((prev) => prev.map((item) => ({ ...item, read: true })));
    }

    function markAsRead(id: string) {
        setItems((prev) =>
            prev.map((item) =>
                item.id === id ? { ...item, read: true } : item,
            ),
        );
        setIsOpen(false);
    }

    return (
        <div ref={dropdownRef} className="staff-notifications-container">
            <button
                type="button"
                className="staff-notifications-btn"
                onClick={() => setIsOpen(!isOpen)}
                aria-label={isVi ? "Thông báo" : "Notifications"}
                aria-expanded={isOpen}
            >
                <span className="staff-notifications-icon-wrap">
                    <Bell size={16} strokeWidth={2.1} aria-hidden="true" />
                    {unreadCount > 0 && (
                        <span className="staff-notifications-badge">
                            {unreadCount}
                        </span>
                    )}
                </span>
                <span>{isVi ? "Thông báo" : "Notifications"}</span>
            </button>

            {/* Notification Dropdown */}
            {isOpen && (
                <div className="staff-notifications-dropdown">
                    <div className="staff-notif-head">
                        <strong className="staff-notif-title">
                            <Bell size={15} strokeWidth={2.2} />
                            <span>
                                {isVi
                                    ? "Thông báo trạm trực"
                                    : "Station Notifications"}
                            </span>
                        </strong>
                        {unreadCount > 0 && (
                            <button
                                type="button"
                                onClick={markAllAsRead}
                                className="staff-notif-mark-all"
                            >
                                <CheckCheck size={13} strokeWidth={2.2} />
                                <span>
                                    {isVi ? "Đã đọc tất cả" : "Mark all read"}
                                </span>
                            </button>
                        )}
                    </div>

                    <div className="staff-notif-list">
                        {items.length === 0 ? (
                            <div
                                className="staff-notif-empty"
                                style={{
                                    padding: "20px 16px",
                                    textAlign: "center",
                                }}
                            >
                                <CheckCircle2
                                    size={24}
                                    style={{
                                        margin: "0 auto 8px auto",
                                        color: "#10b981",
                                    }}
                                />
                                <p
                                    style={{
                                        fontWeight: 600,
                                        fontSize: "13px",
                                        color: "#334155",
                                        margin: 0,
                                    }}
                                >
                                    {isVi
                                        ? "Không có thông báo mới trong ca trực"
                                        : "No new notifications in current shift"}
                                </p>
                                <span
                                    style={{
                                        fontSize: "12px",
                                        color: "#94a3b8",
                                        display: "block",
                                        marginTop: "4px",
                                    }}
                                >
                                    {isVi
                                        ? "Tất cả phương tiện và camera đang hoạt động bình thường"
                                        : "All vehicles and cameras operating normally"}
                                </span>
                            </div>
                        ) : (
                            items.map((item) => (
                                <Link
                                    key={item.id}
                                    href={item.href}
                                    onClick={() => markAsRead(item.id)}
                                    className={`staff-notif-item ${item.read ? "is-read" : "is-unread"}`}
                                >
                                    <span
                                        className={`staff-notif-type-icon type-${item.type}`}
                                    >
                                        {item.type === "ocr" ? (
                                            <AlertTriangle
                                                size={14}
                                                strokeWidth={2.2}
                                            />
                                        ) : item.type === "session" ? (
                                            <Car size={14} strokeWidth={2.2} />
                                        ) : item.type === "barrier" ? (
                                            <Zap size={14} strokeWidth={2.2} />
                                        ) : (
                                            <Bell size={14} strokeWidth={2.2} />
                                        )}
                                    </span>
                                    <div className="staff-notif-body">
                                        <div className="staff-notif-row">
                                            <span className="staff-notif-item-title">
                                                {isVi
                                                    ? item.titleVi
                                                    : item.titleEn}
                                            </span>
                                            <small className="staff-notif-time">
                                                {isVi
                                                    ? item.timeVi
                                                    : item.timeEn}
                                            </small>
                                        </div>
                                        <p className="staff-notif-desc">
                                            {isVi ? item.descVi : item.descEn}
                                        </p>
                                    </div>
                                </Link>
                            ))
                        )}
                    </div>

                    <div className="staff-notif-foot">
                        <Link
                            href="/staff/verification"
                            onClick={() => setIsOpen(false)}
                            className="staff-notif-foot-link"
                        >
                            <span>
                                {isVi
                                    ? `Xử lý sự cố OCR (${pendingIssuesCount})`
                                    : `Review OCR issues (${pendingIssuesCount})`}
                            </span>
                            <ArrowRight size={13} strokeWidth={2.2} />
                        </Link>
                    </div>
                </div>
            )}
        </div>
    );
}
