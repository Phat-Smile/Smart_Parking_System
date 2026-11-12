"use client";

import React, { useState, useEffect, useMemo } from "react";
import Link from "next/link";
import {
    Search,
    X,
    RefreshCw,
    Plus,
    Car,
    Bike,
    Clock,
    DollarSign,
    CheckCircle2,
    XCircle,
    QrCode,
    LogOut,
    Layers,
    Users,
    Ticket,
    Smartphone,
    AlertTriangle,
    ParkingSquare,
} from "lucide-react";
import { PlateBadge, QrCodeModal, FormattedTime, formatVnd } from "./StaffUi";
import {
    cancelParkingSession,
    describeStaffError,
    listLotSessions,
} from "./staffApi";
import { useStaffData } from "./staffData";
import type {
    CustomerType,
    SessionStatus,
    StaffSession,
    VehicleType,
} from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function StaffSessionsPage() {
    const { sessions, cancelSession, assignedLot, fetchAssignedLot } =
        useStaffData();
    const { t } = useLanguage();

    const [isRefreshing, setIsRefreshing] = useState(false);
    const [filterType, setFilterType] = useState<CustomerType | "All">("All");
    const [statusFilter, setStatusFilter] = useState<SessionStatus | "All">(
        "All",
    );
    const [search, setSearch] = useState("");
    const [selectedQrSession, setSelectedQrSession] =
        useState<StaffSession | null>(null);

    // Cancel modal state
    const [cancellingSession, setCancellingSession] =
        useState<StaffSession | null>(null);
    const [cancelReason, setCancelReason] = useState(
        "Tài xế yêu cầu quay đầu xe không vào bãi",
    );
    const [cancelBusy, setCancelBusy] = useState(false);
    const [errorMsg, setErrorMsg] = useState("");
    const [realSessions, setRealSessions] = useState<StaffSession[] | null>(
        null,
    );

    const loadRealSessions = React.useCallback(async (lotId: string) => {
        try {
            const page = await listLotSessions(lotId, undefined, 1, 100);
            if (page && Array.isArray(page.items)) {
                const mapped: StaffSession[] = page.items.map((item) => {
                    const isPaid = item.status === "Paid";
                    const isDone =
                        item.status === "Completed" ||
                        item.status === "Cancelled";
                    const fallbackFee =
                        item.vehicleType === "Car" ? 30000 : 6000;
                    return {
                        id: item.sessionId,
                        lotId,
                        plateNumber: item.plateNumber,
                        vehicleType: (item.vehicleType === "Car"
                            ? "Car"
                            : "Motorbike") as CustomerType extends string
                            ? VehicleType
                            : never,
                        customerType: "Guest",
                        entryTime: item.entryTime,
                        status: item.status as SessionStatus,
                        slotCode: item.slotCode,
                        paymentStatus: isPaid
                            ? "Paid"
                            : isDone
                              ? "Paid"
                              : "Pending",
                        totalFeeVnd: fallbackFee,
                        amountDueVnd: isPaid || isDone ? 0 : fallbackFee,
                    };
                });
                setRealSessions(mapped);
            }
        } catch {
            // fallback to store sessions
        }
    }, []);

    // Load 100% real sessions from backend DB on mount
    useEffect(() => {
        let active = true;
        fetchAssignedLot()
            .then((lot) => {
                if (active && lot?.lotId) {
                    void loadRealSessions(lot.lotId);
                }
            })
            .catch(() => {});
        return () => {
            active = false;
        };
    }, [fetchAssignedLot, loadRealSessions]);

    async function handleRefresh() {
        setIsRefreshing(true);
        setErrorMsg("");
        try {
            const lot = await fetchAssignedLot();
            if (lot?.lotId) {
                await loadRealSessions(lot.lotId);
            }
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setIsRefreshing(false);
        }
    }

    const effectiveSessions = realSessions ?? sessions;

    // 5 Real KPI Stats calculated directly from real sessions
    const totalSessions = effectiveSessions.length;
    const activeCount = effectiveSessions.filter(
        (s) => s.status === "Active",
    ).length;
    const paidCount = effectiveSessions.filter(
        (s) => s.status === "Paid",
    ).length;
    const completedCount = effectiveSessions.filter(
        (s) => s.status === "Completed",
    ).length;
    const cancelledCount = effectiveSessions.filter(
        (s) => s.status === "Cancelled",
    ).length;
    const pendingPaymentCount = effectiveSessions.filter(
        (s) =>
            (s.amountDueVnd ?? 0) > 0 &&
            s.status !== "Cancelled" &&
            s.status !== "Completed",
    ).length;

    const filtered = useMemo(() => {
        return effectiveSessions
            .filter((s) =>
                filterType === "All" ? true : s.customerType === filterType,
            )
            .filter((s) =>
                statusFilter === "All" ? true : s.status === statusFilter,
            )
            .filter(
                (s) =>
                    !search.trim() ||
                    s.plateNumber
                        .toLowerCase()
                        .includes(search.trim().toLowerCase()) ||
                    (s.slotCode ?? "")
                        .toLowerCase()
                        .includes(search.trim().toLowerCase()) ||
                    s.id.toLowerCase().includes(search.trim().toLowerCase()) ||
                    (s.driverName &&
                        s.driverName
                            .toLowerCase()
                            .includes(search.trim().toLowerCase())),
            );
    }, [effectiveSessions, filterType, statusFilter, search]);

    async function handleConfirmCancel() {
        if (!cancellingSession) return;
        setCancelBusy(true);
        setErrorMsg("");

        try {
            // 100% Real API: Call POST /api/parking/sessions/{id}/cancel
            await cancelParkingSession(cancellingSession.id, cancelReason);
            cancelSession(cancellingSession.id);
            if (assignedLot?.lotId) {
                await loadRealSessions(assignedLot.lotId);
            }
            setCancellingSession(null);
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setCancelBusy(false);
        }
    }

    function handleResetFilters() {
        setSearch("");
        setFilterType("All");
        setStatusFilter("All");
    }

    return (
        <div className="staff-page">
            {/* Header */}
            <header className="staff-header">
                <div>
                    <h1 className="staff-title">{t("sessionsTitle")}</h1>
                    <p className="staff-subtitle">
                        {assignedLot?.lotName
                            ? `Bãi trực: ${assignedLot.lotName} · `
                            : ""}
                        {t("sessionsSubtitle")}
                    </p>
                </div>

                <div className="staff-header-actions">
                    <button
                        type="button"
                        className="staff-btn staff-btn-secondary"
                        onClick={handleRefresh}
                        disabled={isRefreshing}
                        title="Đồng bộ lại danh sách phiên xe mới nhất từ CSDL"
                    >
                        <RefreshCw
                            size={14}
                            strokeWidth={2.2}
                            className={isRefreshing ? "staff-spin" : ""}
                        />
                        <span>
                            {isRefreshing
                                ? "Đang tải CSDL..."
                                : "Làm mới từ CSDL"}
                        </span>
                    </button>
                    <Link
                        href="/staff/entry"
                        className="staff-btn-checkin-primary"
                        style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "6px",
                            padding: "9px 16px",
                            borderRadius: "10px",
                            textDecoration: "none",
                            fontWeight: 700,
                            fontSize: "13px",
                        }}
                    >
                        <Plus size={15} strokeWidth={2.5} />
                        <span>Check-in xe mới</span>
                    </Link>
                </div>
            </header>

            {/* Error Alert Banner */}
            {errorMsg && (
                <div className="staff-alert staff-alert-danger">
                    <span
                        style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "6px",
                        }}
                    >
                        <AlertTriangle size={15} strokeWidth={2.2} />
                        <span>{errorMsg}</span>
                    </span>
                    <button type="button" onClick={() => setErrorMsg("")}>
                        ✕
                    </button>
                </div>
            )}

            {/* 4.1. 5 Real Metric Stat Cards */}
            <div className="staff-session-kpis">
                <div className="staff-session-kpi-card">
                    <div className="staff-session-kpi-icon neutral">
                        <Layers size={20} strokeWidth={2.2} />
                    </div>
                    <div className="staff-session-kpi-info">
                        <span className="staff-session-kpi-val">
                            {totalSessions}
                        </span>
                        <span className="staff-session-kpi-label">
                            Tổng phiên xe
                        </span>
                    </div>
                </div>

                <div className="staff-session-kpi-card">
                    <div className="staff-session-kpi-icon emerald">
                        <ParkingSquare size={20} strokeWidth={2.2} />
                    </div>
                    <div className="staff-session-kpi-info">
                        <span
                            className="staff-session-kpi-val"
                            style={{ color: "#15803d" }}
                        >
                            {activeCount}
                        </span>
                        <span className="staff-session-kpi-label">
                            Đang đỗ trong bãi
                        </span>
                    </div>
                </div>

                <div className="staff-session-kpi-card">
                    <div className="staff-session-kpi-icon amber">
                        <DollarSign size={20} strokeWidth={2.2} />
                    </div>
                    <div className="staff-session-kpi-info">
                        <span
                            className="staff-session-kpi-val"
                            style={{ color: "#b45309" }}
                        >
                            {pendingPaymentCount}
                        </span>
                        <span className="staff-session-kpi-label">
                            Chờ thanh toán cước
                        </span>
                    </div>
                </div>

                <div className="staff-session-kpi-card">
                    <div className="staff-session-kpi-icon blue">
                        <CheckCircle2 size={20} strokeWidth={2.2} />
                    </div>
                    <div className="staff-session-kpi-info">
                        <span
                            className="staff-session-kpi-val"
                            style={{ color: "#0369a1" }}
                        >
                            {completedCount}
                        </span>
                        <span className="staff-session-kpi-label">
                            Đã rời bãi (Hoàn tất)
                        </span>
                    </div>
                </div>

                <div className="staff-session-kpi-card">
                    <div className="staff-session-kpi-icon rose">
                        <XCircle size={20} strokeWidth={2.2} />
                    </div>
                    <div className="staff-session-kpi-info">
                        <span
                            className="staff-session-kpi-val"
                            style={{ color: "#dc2626" }}
                        >
                            {cancelledCount}
                        </span>
                        <span className="staff-session-kpi-label">
                            Đã hủy phiên
                        </span>
                    </div>
                </div>
            </div>

            {/* Modern Segmented Filter Toolbar */}
            <div className="staff-toolbar">
                <div className="staff-toolbar-top">
                    <div className="staff-search-box">
                        <Search
                            size={15}
                            strokeWidth={2.3}
                            className="search-icon"
                        />
                        <input
                            type="text"
                            className="staff-search-input"
                            placeholder={t("sessionsSearchPlaceholder")}
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                        />
                        {search && (
                            <button
                                type="button"
                                className="staff-search-clear"
                                onClick={() => setSearch("")}
                                title="Xóa tìm kiếm"
                            >
                                <X size={14} strokeWidth={2.2} />
                            </button>
                        )}
                    </div>

                    <div className="staff-toolbar-summary">
                        <span className="staff-summary-pill">
                            Tổng hiển thị: <strong>{filtered.length}</strong> /{" "}
                            {totalSessions} phiên xe
                        </span>
                    </div>
                </div>

                <div className="staff-toolbar-bottom">
                    {/* Status filter tabs */}
                    <div className="staff-filter-group">
                        <span className="staff-filter-label">Trạng thái:</span>
                        <div className="staff-filter-tabs">
                            {[
                                {
                                    key: "All",
                                    label: "Tất cả",
                                    icon: (
                                        <Layers size={13} strokeWidth={2.2} />
                                    ),
                                    count: totalSessions,
                                },
                                {
                                    key: "Active",
                                    label: "Đang đỗ (Active)",
                                    icon: <Clock size={13} strokeWidth={2.2} />,
                                    count: activeCount,
                                },
                                {
                                    key: "Paid",
                                    label: "Đã thanh toán (Paid)",
                                    icon: (
                                        <CheckCircle2
                                            size={13}
                                            strokeWidth={2.2}
                                        />
                                    ),
                                    count: paidCount,
                                },
                                {
                                    key: "Completed",
                                    label: "Đã rời bãi (Completed)",
                                    icon: (
                                        <LogOut size={13} strokeWidth={2.2} />
                                    ),
                                    count: completedCount,
                                },
                                {
                                    key: "Cancelled",
                                    label: "Đã hủy (Cancelled)",
                                    icon: (
                                        <XCircle size={13} strokeWidth={2.2} />
                                    ),
                                    count: cancelledCount,
                                },
                            ].map((tab) => {
                                const isActive = statusFilter === tab.key;
                                return (
                                    <button
                                        key={tab.key}
                                        type="button"
                                        className={`staff-filter-btn ${isActive ? "active" : ""}`}
                                        onClick={() =>
                                            setStatusFilter(
                                                tab.key as
                                                    SessionStatus | "All",
                                            )
                                        }
                                    >
                                        <span
                                            style={{
                                                display: "inline-flex",
                                                alignItems: "center",
                                            }}
                                        >
                                            {tab.icon}
                                        </span>
                                        <span>{tab.label}</span>
                                        <span className="staff-filter-badge">
                                            {tab.count}
                                        </span>
                                    </button>
                                );
                            })}
                        </div>
                    </div>

                    {/* Customer type filter tabs */}
                    <div className="staff-filter-group">
                        <span className="staff-filter-label">Đối tượng:</span>
                        <div className="staff-filter-tabs">
                            {[
                                {
                                    key: "All",
                                    label: "Tất cả phiên xe",
                                    icon: <Users size={13} strokeWidth={2.2} />,
                                    count: totalSessions,
                                },
                                {
                                    key: "Guest",
                                    label: "Khách vãng lai",
                                    icon: (
                                        <Ticket size={13} strokeWidth={2.2} />
                                    ),
                                    count: sessions.filter(
                                        (s) => s.customerType === "Guest",
                                    ).length,
                                },
                                {
                                    key: "Registered",
                                    label: "Khách đăng ký App",
                                    icon: (
                                        <Smartphone
                                            size={13}
                                            strokeWidth={2.2}
                                        />
                                    ),
                                    count: sessions.filter(
                                        (s) => s.customerType === "Registered",
                                    ).length,
                                },
                            ].map((tab) => {
                                const isActive = filterType === tab.key;
                                return (
                                    <button
                                        key={tab.key}
                                        type="button"
                                        className={`staff-filter-btn ${isActive ? "active" : ""}`}
                                        onClick={() =>
                                            setFilterType(
                                                tab.key as CustomerType | "All",
                                            )
                                        }
                                    >
                                        <span
                                            style={{
                                                display: "inline-flex",
                                                alignItems: "center",
                                            }}
                                        >
                                            {tab.icon}
                                        </span>
                                        <span>{tab.label}</span>
                                        <span className="staff-filter-badge">
                                            {tab.count}
                                        </span>
                                    </button>
                                );
                            })}
                        </div>
                    </div>
                </div>
            </div>

            {/* Sessions Table */}
            <div className="staff-card staff-table-card">
                <table className="staff-table staff-sessions-table">
                    <colgroup>
                        <col style={{ width: "13%" }} />
                        <col style={{ width: "11%" }} />
                        <col style={{ width: "8%" }} />
                        <col style={{ width: "7%" }} />
                        <col style={{ width: "13%" }} />
                        <col style={{ width: "11%" }} />
                        <col style={{ width: "13%" }} />
                        <col style={{ width: "8%" }} />
                        <col style={{ width: "16%" }} />
                    </colgroup>
                    <thead>
                        <tr>
                            <th>{t("sessionsLicensePlate")}</th>
                            <th>{t("sessionsCategory")}</th>
                            <th>{t("sessionsVehicle")}</th>
                            <th>{t("sessionsSpot")}</th>
                            <th>{t("sessionsEntryTime")}</th>
                            <th>{t("sessionsStatus")}</th>
                            <th>{t("sessionsPaymentStatus")}</th>
                            <th>{t("sessionsQr")}</th>
                            <th className="text-right">
                                {t("sessionsActions")}
                            </th>
                        </tr>
                    </thead>
                    <tbody>
                        {totalSessions === 0 && !isRefreshing ? (
                            /* State 2: Empty Database */
                            <tr>
                                <td colSpan={9} style={{ padding: 0 }}>
                                    <div className="staff-empty-state-box">
                                        <div className="empty-icon-wrap">
                                            <ParkingSquare
                                                size={32}
                                                strokeWidth={1.8}
                                            />
                                        </div>
                                        <h3>
                                            Chưa có phiên gửi xe nào trong CSDL
                                        </h3>
                                        <p>
                                            Hãy tiến hành Check-in xe tại cổng
                                            vào để tạo phiên mới trên CSDL thật
                                            của hệ thống.
                                        </p>
                                        <Link
                                            href="/staff/entry"
                                            className="staff-btn-checkin-primary"
                                            style={{
                                                display: "inline-flex",
                                                alignItems: "center",
                                                gap: "6px",
                                                padding: "8px 16px",
                                                borderRadius: "10px",
                                                textDecoration: "none",
                                                fontWeight: 700,
                                                fontSize: "13px",
                                            }}
                                        >
                                            <Plus size={14} strokeWidth={2.4} />
                                            <span>
                                                Đi đến Cổng Vào (Check-in)
                                            </span>
                                        </Link>
                                    </div>
                                </td>
                            </tr>
                        ) : filtered.length === 0 ? (
                            /* State 3: No Search / Filter Matches */
                            <tr>
                                <td colSpan={9} style={{ padding: 0 }}>
                                    <div className="staff-empty-state-box">
                                        <div className="empty-icon-wrap">
                                            <Search
                                                size={30}
                                                strokeWidth={1.8}
                                            />
                                        </div>
                                        <h3>
                                            Không tìm thấy phiên xe nào phù hợp
                                        </h3>
                                        <p>
                                            Không có kết quả nào khớp với các bộ
                                            lọc hoặc từ khóa tìm kiếm &quot;
                                            {search}&quot;.
                                        </p>
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-secondary staff-btn-sm"
                                            onClick={handleResetFilters}
                                            style={{
                                                display: "inline-flex",
                                                alignItems: "center",
                                                gap: "6px",
                                            }}
                                        >
                                            <RefreshCw
                                                size={13}
                                                strokeWidth={2.2}
                                            />
                                            <span>Đặt lại tất cả bộ lọc</span>
                                        </button>
                                    </div>
                                </td>
                            </tr>
                        ) : (
                            /* State 5: Normal Table Rows */
                            filtered.map((s) => (
                                <tr key={s.id}>
                                    <td>
                                        <PlateBadge plate={s.plateNumber} />
                                    </td>
                                    <td>
                                        {s.customerType === "Guest" ? (
                                            <span className="staff-cust-pill guest">
                                                <Ticket
                                                    size={12}
                                                    strokeWidth={2.2}
                                                />
                                                <span>
                                                    {t("sessionsGuest")}
                                                </span>
                                            </span>
                                        ) : (
                                            <span className="staff-cust-pill registered">
                                                <Smartphone
                                                    size={12}
                                                    strokeWidth={2.2}
                                                />
                                                <span>
                                                    {t("sessionsRegistered")}
                                                </span>
                                            </span>
                                        )}
                                    </td>
                                    <td>
                                        {s.vehicleType === "Car" ? (
                                            <span className="staff-vehicle-pill">
                                                <Car
                                                    size={13}
                                                    strokeWidth={2.2}
                                                />
                                                <span>{t("staffCar")}</span>
                                            </span>
                                        ) : (
                                            <span className="staff-vehicle-pill">
                                                <Bike
                                                    size={13}
                                                    strokeWidth={2.2}
                                                />
                                                <span>
                                                    {t("staffMotorbike")}
                                                </span>
                                            </span>
                                        )}
                                    </td>
                                    <td>
                                        <span className="staff-slot-code">
                                            {s.slotCode || "—"}
                                        </span>
                                    </td>
                                    <td>
                                        <div className="staff-time-cell">
                                            <Clock
                                                size={12}
                                                strokeWidth={2.2}
                                                style={{ color: "#94a3b8" }}
                                            />
                                            <FormattedTime
                                                iso={s.entryTime}
                                                dateTime
                                            />
                                        </div>
                                    </td>
                                    <td>
                                        {s.status === "Active" ? (
                                            <span className="staff-session-status status-active">
                                                <Clock
                                                    size={11}
                                                    strokeWidth={2.3}
                                                />
                                                <span>{t("statusActive")}</span>
                                            </span>
                                        ) : s.status === "Paid" ? (
                                            <span className="staff-session-status status-paid">
                                                <CheckCircle2
                                                    size={11}
                                                    strokeWidth={2.3}
                                                />
                                                <span>{t("statusPaid")}</span>
                                            </span>
                                        ) : s.status === "Completed" ? (
                                            <span className="staff-session-status status-completed">
                                                <LogOut
                                                    size={11}
                                                    strokeWidth={2.3}
                                                />
                                                <span>
                                                    {t("statusCompleted")}
                                                </span>
                                            </span>
                                        ) : (
                                            <span className="staff-session-status status-cancelled">
                                                <XCircle
                                                    size={11}
                                                    strokeWidth={2.3}
                                                />
                                                <span>
                                                    {t("statusCancelled")}
                                                </span>
                                            </span>
                                        )}
                                    </td>
                                    <td>
                                        {s.paymentStatus === "Paid" ||
                                        s.status === "Paid" ? (
                                            <span className="staff-pay-badge pay-paid">
                                                <CheckCircle2
                                                    size={11}
                                                    strokeWidth={2.3}
                                                />
                                                <span>{t("statusPaid")}</span>
                                            </span>
                                        ) : (s.amountDueVnd ?? 0) > 0 ? (
                                            <div className="staff-payment-cell">
                                                <span className="staff-pay-badge pay-pending">
                                                    <Clock
                                                        size={11}
                                                        strokeWidth={2.3}
                                                    />
                                                    <span>
                                                        {t("statusPending")}
                                                    </span>
                                                </span>
                                                <span className="staff-due-badge">
                                                    {t("sessionsDue")}:{" "}
                                                    {formatVnd(
                                                        s.amountDueVnd ?? 0,
                                                    )}
                                                </span>
                                            </div>
                                        ) : (
                                            <span className="staff-pay-badge pay-free">
                                                <span>Miễn phí (0₫)</span>
                                            </span>
                                        )}
                                    </td>
                                    <td>
                                        {s.qrToken ? (
                                            <button
                                                type="button"
                                                className="staff-btn-qr-pill"
                                                onClick={() =>
                                                    setSelectedQrSession(s)
                                                }
                                                title="Xem vé QR điện tử của phiên xe"
                                            >
                                                <QrCode
                                                    size={13}
                                                    strokeWidth={2.2}
                                                />
                                                <span>
                                                    {t("sessionsViewQr")}
                                                </span>
                                            </button>
                                        ) : (
                                            <span
                                                style={{
                                                    fontSize: "12px",
                                                    color: "#94a3b8",
                                                }}
                                            >
                                                —
                                            </span>
                                        )}
                                    </td>
                                    <td className="text-right">
                                        {s.status === "Active" ||
                                        s.status === "Paid" ? (
                                            <div className="staff-table-btn-group">
                                                {s.status === "Active" && (
                                                    <button
                                                        type="button"
                                                        className="staff-btn-table-action action-cancel"
                                                        onClick={() =>
                                                            setCancellingSession(
                                                                s,
                                                            )
                                                        }
                                                        title="Hủy phiên khi xe chưa vào bãi hoặc quay đầu"
                                                    >
                                                        <XCircle
                                                            size={13}
                                                            strokeWidth={2.2}
                                                        />
                                                        <span>
                                                            {t(
                                                                "sessionsCancelEntry",
                                                            )}
                                                        </span>
                                                    </button>
                                                )}
                                                <Link
                                                    href="/staff/exit"
                                                    className="staff-btn-table-action action-exit"
                                                    title="Tiến hành kiểm tra & xuất bãi tại Cổng Ra"
                                                >
                                                    <LogOut
                                                        size={13}
                                                        strokeWidth={2.2}
                                                    />
                                                    <span>Cho xe ra</span>
                                                </Link>
                                            </div>
                                        ) : (
                                            <span className="staff-action-closed">
                                                —
                                            </span>
                                        )}
                                    </td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>

            {/* QR View Modal */}
            {selectedQrSession?.qrToken && (
                <QrCodeModal
                    qrToken={selectedQrSession.qrToken}
                    plateNumber={selectedQrSession.plateNumber}
                    vehicleType={selectedQrSession.vehicleType}
                    entryTime={selectedQrSession.entryTime}
                    onClose={() => setSelectedQrSession(null)}
                />
            )}

            {/* Cancel Session Confirmation Modal */}
            {cancellingSession && (
                <div
                    className="staff-modal-backdrop"
                    onClick={() => setCancellingSession(null)}
                >
                    <div
                        className="staff-modal"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="staff-modal-header">
                            <div
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: "8px",
                                }}
                            >
                                <AlertTriangle
                                    size={18}
                                    strokeWidth={2.3}
                                    style={{ color: "#d97706" }}
                                />
                                <h2>{t("sessionsCancelTitle")}</h2>
                            </div>
                            <button
                                type="button"
                                className="staff-modal-close"
                                onClick={() => setCancellingSession(null)}
                            >
                                ✕
                            </button>
                        </div>
                        <div className="staff-modal-body">
                            <p className="staff-modal-text">
                                {t("sessionsCancelConfirmText")}{" "}
                                <strong
                                    style={{
                                        fontFamily: "var(--pw-admin-font-mono)",
                                        color: "#1e293b",
                                    }}
                                >
                                    {cancellingSession.plateNumber}
                                </strong>
                                ?
                            </p>
                            <p className="staff-modal-hint">
                                Phiên xe sẽ được đánh dấu Cancelled trong CSDL
                                và vị trí đỗ sẽ được giải phóng ngay lập tức.
                            </p>

                            <div className="staff-form-group">
                                <label htmlFor="staff-cancel-reason">
                                    {t("sessionsCancelReasonLabel")}
                                </label>
                                <textarea
                                    id="staff-cancel-reason"
                                    className="staff-textarea"
                                    rows={3}
                                    value={cancelReason}
                                    onChange={(e) =>
                                        setCancelReason(e.target.value)
                                    }
                                />
                            </div>

                            <div className="staff-modal-action-bar">
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-secondary"
                                    onClick={() => setCancellingSession(null)}
                                >
                                    {t("sessionsGoBack")}
                                </button>
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-danger staff-btn-flex"
                                    disabled={cancelBusy}
                                    onClick={handleConfirmCancel}
                                    style={{
                                        display: "inline-flex",
                                        alignItems: "center",
                                        gap: "6px",
                                    }}
                                >
                                    {cancelBusy ? (
                                        <>
                                            <RefreshCw
                                                size={13}
                                                strokeWidth={2.3}
                                                className="staff-spin"
                                            />
                                            <span>
                                                {t("sessionsCancelling")}
                                            </span>
                                        </>
                                    ) : (
                                        <>
                                            <XCircle
                                                size={14}
                                                strokeWidth={2.2}
                                            />
                                            <span>
                                                {t("sessionsConfirmCancel")}
                                            </span>
                                        </>
                                    )}
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
