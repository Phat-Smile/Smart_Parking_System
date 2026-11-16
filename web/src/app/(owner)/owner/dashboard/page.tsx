"use client";

import React, { useEffect, useState, useSyncExternalStore } from "react";
import Image from "next/image";
import Link from "next/link";
import "@/styles/bento-dashboard.css";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import {
    getOwnerParkingLotsApi,
    type OwnerParkingLotItem,
} from "@/features/owner/api/ownerApi";
import {
    getMyOwnerApplication,
    type MyOwnerApplicationDto,
} from "@/features/owner/api/ownerLegalApi";
import { loadOwnerLots } from "@/features/owner/lots/ownerLotsSync";
import { setOwnerViewMode } from "@/features/owner/mode/useOwnerViewMode";
import {
    loadAggregatedOwnerStats,
    type AggregatedOwnerDashboardStats,
} from "@/features/owner/dashboard/ownerStatisticsApi";
import { DashboardEmptyState } from "@/features/owner/dashboard/DashboardEmptyState";

// ── TRAFFIC DATA POINTS (DEFAULT ALL 0 ON PRODUCTION DASHBOARD) ──

interface TrafficDataPoint {
    time: string;
    inCount: number;
    outCount: number;
    x: number;
    yIn: number;
    yOut: number;
}

const ZERO_TRAFFIC_TODAY: TrafficDataPoint[] = [
    { time: "06:00", inCount: 0, outCount: 0, x: 50, yIn: 160, yOut: 160 },
    { time: "08:30", inCount: 0, outCount: 0, x: 135, yIn: 160, yOut: 160 },
    { time: "11:30", inCount: 0, outCount: 0, x: 220, yIn: 160, yOut: 160 },
    { time: "14:30", inCount: 0, outCount: 0, x: 305, yIn: 160, yOut: 160 },
    { time: "17:30", inCount: 0, outCount: 0, x: 390, yIn: 160, yOut: 160 },
    { time: "20:30", inCount: 0, outCount: 0, x: 475, yIn: 160, yOut: 160 },
    { time: "23:00", inCount: 0, outCount: 0, x: 560, yIn: 160, yOut: 160 },
];

const ZERO_TRAFFIC_7DAYS: TrafficDataPoint[] = [
    { time: "T2", inCount: 0, outCount: 0, x: 50, yIn: 160, yOut: 160 },
    { time: "T3", inCount: 0, outCount: 0, x: 135, yIn: 160, yOut: 160 },
    { time: "T4", inCount: 0, outCount: 0, x: 220, yIn: 160, yOut: 160 },
    { time: "T5", inCount: 0, outCount: 0, x: 305, yIn: 160, yOut: 160 },
    { time: "T6", inCount: 0, outCount: 0, x: 390, yIn: 160, yOut: 160 },
    { time: "T7", inCount: 0, outCount: 0, x: 475, yIn: 160, yOut: 160 },
    { time: "CN", inCount: 0, outCount: 0, x: 560, yIn: 160, yOut: 160 },
];

const ZERO_TRAFFIC_MONTH: TrafficDataPoint[] = [
    { time: "Tuần 1", inCount: 0, outCount: 0, x: 50, yIn: 160, yOut: 160 },
    { time: "Tuần 2", inCount: 0, outCount: 0, x: 220, yIn: 160, yOut: 160 },
    { time: "Tuần 3", inCount: 0, outCount: 0, x: 390, yIn: 160, yOut: 160 },
    { time: "Tuần 4", inCount: 0, outCount: 0, x: 560, yIn: 160, yOut: 160 },
];

export default function DashboardPage() {
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );

    const displayName =
        session?.displayName ||
        (session?.account?.email
            ? session.account.email.split("@")[0]
            : "Chủ bãi");

    // Real API Data states
    const [realLots, setRealLots] = useState<OwnerParkingLotItem[]>([]);
    const [stats, setStats] = useState<AggregatedOwnerDashboardStats | null>(
        null,
    );
    const [application, setApplication] =
        useState<MyOwnerApplicationDto | null>(null);
    const [loading, setLoading] = useState(true);

    // Line Chart timeframe inside card
    const [chartTimeframe, setChartTimeframe] = useState<
        "today" | "week" | "month"
    >("today");

    // Smart operations toggle
    const [surgeAutoEnabled, setSurgeAutoEnabled] = useState(false);

    // Selected point for line chart tooltip (defaults to 17:30)
    const [activePointIndex, setActivePointIndex] = useState<number>(4);

    // Mặc định đảm bảo Dashboard chính luôn hoạt động ở chế độ Live thật
    useEffect(() => {
        if (typeof window !== "undefined") {
            const params = new URLSearchParams(window.location.search);
            if (params.get("mode") !== "sample") {
                setOwnerViewMode("live");
            }
        }
    }, []);

    // Fetch real data on mount
    useEffect(() => {
        let active = true;
        async function fetchDashboardData() {
            setLoading(true);
            try {
                const [appRes, lotsRes] = await Promise.all([
                    getMyOwnerApplication().catch(() => null),
                    getOwnerParkingLotsApi({ page: 1, pageSize: 100 }).catch(
                        () => ({
                            items: [],
                            totalCount: 0,
                            page: 1,
                            pageSize: 100,
                        }),
                    ),
                    loadOwnerLots().catch(() => []),
                ]);

                if (active) {
                    setApplication(appRes);
                    const items = lotsRes?.items || [];
                    setRealLots(items);
                    const approvedIds = items
                        .filter(
                            (l) =>
                                l.approvalStatus?.toLowerCase() === "approved",
                        )
                        .map((l) => l.id);
                    if (approvedIds.length > 0) {
                        const statsData = await loadAggregatedOwnerStats(
                            approvedIds,
                        ).catch(() => null);
                        if (active) setStats(statsData);
                    }
                    setLoading(false);
                }
            } catch {
                if (active) {
                    setLoading(false);
                }
            }
        }
        void fetchDashboardData();
        return () => {
            active = false;
        };
    }, [session]);

    const approvedLots = realLots.filter(
        (l) => l.approvalStatus?.toLowerCase() === "approved",
    );
    const pendingLots = realLots.filter(
        (l) => l.approvalStatus?.toLowerCase() === "pending",
    );

    // Pending Legal Owner Application Notice
    const isPendingApproval =
        application?.status === "Pending" &&
        session?.account?.role !== "ParkingLotOwner";

    // Chart path calculations for zero baseline
    const trafficData =
        chartTimeframe === "today"
            ? ZERO_TRAFFIC_TODAY
            : chartTimeframe === "week"
              ? ZERO_TRAFFIC_7DAYS
              : ZERO_TRAFFIC_MONTH;

    const currentPoint =
        trafficData[Math.min(activePointIndex, trafficData.length - 1)];

    const inPath = trafficData.reduce(
        (acc, p, i) =>
            i === 0 ? `M ${p.x} ${p.yIn}` : `${acc} L ${p.x} ${p.yIn}`,
        "",
    );
    const inAreaPath = `${inPath} L ${trafficData[trafficData.length - 1].x} 160 L ${trafficData[0].x} 160 Z`;

    const outPath = trafficData.reduce(
        (acc, p, i) =>
            i === 0 ? `M ${p.x} ${p.yOut}` : `${acc} L ${p.x} ${p.yOut}`,
        "",
    );

    if (loading) {
        return (
            <div
                className="bento-dashboard-wrap"
                style={{ padding: "40px 0", textAlign: "center" }}
            >
                <div
                    style={{
                        maxWidth: 480,
                        margin: "0 auto",
                        background: "#fff",
                        border: "1px solid #E1E7DE",
                        borderRadius: 16,
                        padding: "36px 24px",
                    }}
                >
                    <div
                        style={{
                            width: 36,
                            height: 36,
                            margin: "0 auto 14px",
                            border: "3px solid #E1E7DE",
                            borderTopColor: "#19301D",
                            borderRadius: "50%",
                            animation: "spin 0.8s linear infinite",
                        }}
                    />
                    <h3
                        style={{
                            fontSize: 16,
                            fontWeight: 800,
                            color: "#172018",
                            margin: "0 0 6px",
                        }}
                    >
                        Đang đồng bộ dữ liệu vận hành bãi xe...
                    </h3>
                    <p style={{ fontSize: 13, color: "#718071", margin: 0 }}>
                        Vui lòng đợi giây lát trong khi ParkWise kết nối với hệ
                        thống máy chủ.
                    </p>
                </div>
                <style jsx>{`
                    @keyframes spin {
                        to {
                            transform: rotate(360deg);
                        }
                    }
                `}</style>
            </div>
        );
    }

    return (
        <div className="bento-dashboard-wrap">
            {/* ── HEADER ROW (CHỈ CÓ TIÊU ĐỀ + NÚT ĐĂNG KÝ BÃI MỚI) ── */}
            <div className="bento-header-row">
                <div className="bento-title-group">
                    <div className="bento-title-icon">
                        <svg
                            width="22"
                            height="22"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="#19301D"
                            strokeWidth="2.4"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                        >
                            <line x1="18" y1="20" x2="18" y2="10" />
                            <line x1="12" y1="20" x2="12" y2="4" />
                            <line x1="6" y1="20" x2="6" y2="14" />
                        </svg>
                    </div>
                    <div>
                        <h1 className="bento-header-title">
                            Tổng quan vận hành cơ sở
                        </h1>
                        <p className="bento-header-subtitle">
                            Theo dõi doanh thu, tỷ lệ lấp đầy và tình trạng hoạt
                            động của các bãi xe theo thời gian thực.
                        </p>
                    </div>
                </div>

                <div className="bento-header-controls">
                    {/* Chỉ giữ duy nhất nút Đăng ký bãi mới */}
                    <Link
                        href="/owner/parking-lots/new"
                        className="bento-btn-lime-add"
                    >
                        <svg
                            width="15"
                            height="15"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2.8"
                        >
                            <line x1="12" y1="5" x2="12" y2="19" />
                            <line x1="5" y1="12" x2="19" y2="12" />
                        </svg>
                        <span>Đăng ký bãi mới</span>
                    </Link>
                </div>
            </div>

            {/* Thông báo nếu tài khoản còn đang chờ duyệt hồ sơ pháp lý */}
            {isPendingApproval && (
                <div
                    style={{
                        background: "#FEFCE8",
                        border: "1.5px solid #FDE047",
                        borderRadius: 14,
                        padding: "16px 20px",
                        marginBottom: 20,
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        flexWrap: "wrap",
                        gap: 12,
                    }}
                >
                    <div>
                        <div
                            style={{
                                fontWeight: 800,
                                color: "#854D0E",
                                fontSize: 14.5,
                            }}
                        >
                            Hồ sơ Đối tác đang chờ Ban quản trị ParkWise phê
                            duyệt
                        </div>
                        <p
                            style={{
                                margin: "4px 0 0",
                                color: "#A16207",
                                fontSize: 13,
                            }}
                        >
                            Hồ sơ pháp lý của cơ sở{" "}
                            <strong>
                                {application?.businessName || displayName}
                            </strong>{" "}
                            đang trong hàng đợi thẩm định. Bạn có thể xem bản mô
                            phỏng hoặc theo dõi tiến trình.
                        </p>
                    </div>
                    <div style={{ display: "flex", gap: 10 }}>
                        <Link
                            href="/owner/onboarding"
                            style={{
                                background: "#CA8A04",
                                color: "#FFFFFF",
                                padding: "7px 14px",
                                borderRadius: 8,
                                fontWeight: 700,
                                fontSize: 12.5,
                                textDecoration: "none",
                            }}
                        >
                            Theo dõi hồ sơ
                        </Link>
                        <Link
                            href="/owner/simulation"
                            onClick={() => setOwnerViewMode("sample")}
                            style={{
                                background: "#FFFFFF",
                                border: "1px solid #CA8A04",
                                color: "#854D0E",
                                padding: "7px 14px",
                                borderRadius: 8,
                                fontWeight: 700,
                                fontSize: 12.5,
                                textDecoration: "none",
                            }}
                        >
                            Xem bản mô phỏng
                        </Link>
                    </div>
                </div>
            )}

            {/* ── ROW 1: 3 BENTO COLUMNS ── */}
            <div className="bento-row-top">
                {/* CỘT 1: DOANH THU HÔM NAY (ĐƯA VỀ 0) + 3 THẺ PHÂN BỔ NHANH */}
                <div
                    className="bento-card"
                    style={{
                        display: "flex",
                        flexDirection: "column",
                        justifyContent: "space-between",
                    }}
                >
                    <div>
                        <div
                            className="bento-card-header"
                            style={{ marginBottom: 8 }}
                        >
                            <div className="bento-card-title-wrap">
                                <div
                                    style={{
                                        width: 22,
                                        height: 22,
                                        borderRadius: "50%",
                                        background: "#EAF7EE",
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "center",
                                        color: "#19301D",
                                    }}
                                >
                                    <svg
                                        width="13"
                                        height="13"
                                        viewBox="0 0 24 24"
                                        fill="none"
                                        stroke="currentColor"
                                        strokeWidth="2.5"
                                    >
                                        <line x1="12" y1="1" x2="12" y2="23" />
                                        <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
                                    </svg>
                                </div>
                                <h3 className="bento-card-title">
                                    Doanh thu hôm nay
                                </h3>
                            </div>
                            <span
                                className="bento-badge-success"
                                style={{
                                    background:
                                        stats && stats.totalRevenue > 0
                                            ? "#EAF7EE"
                                            : "#F1F5F9",
                                    color:
                                        stats && stats.totalRevenue > 0
                                            ? "#269B62"
                                            : "#64748B",
                                }}
                            >
                                {stats && stats.totalRevenue > 0
                                    ? "Ghi nhận hôm nay"
                                    : "Chưa có lượt đỗ"}
                            </span>
                        </div>

                        {/* Số tiền doanh thu chính */}
                        <div className="bento-rev-number">
                            {stats &&
                            stats.hasRealActivity &&
                            stats.totalRevenue > 0 ? (
                                <>
                                    {new Intl.NumberFormat("vi-VN").format(
                                        stats.totalRevenue,
                                    )}{" "}
                                    <span className="bento-rev-unit">đ</span>
                                </>
                            ) : (
                                <span
                                    style={{
                                        color: "#718071",
                                        fontWeight: 700,
                                    }}
                                >
                                    — <span className="bento-rev-unit">đ</span>
                                </span>
                            )}
                        </div>
                        <div className="bento-rev-sub">
                            {stats &&
                            stats.hasRealActivity &&
                            stats.totalRevenue > 0
                                ? `Phí thực thu đã trừ cọc: ${new Intl.NumberFormat("vi-VN").format(stats.netRevenue || stats.totalRevenue)} đ`
                                : "Chưa phát sinh doanh thu hôm nay"}
                        </div>

                        {/* Biểu đồ sóng nước phẳng (mức 0) */}
                        <div
                            style={{
                                width: "100%",
                                height: 48,
                                margin: "10px 0",
                            }}
                        >
                            <svg
                                viewBox="0 0 340 48"
                                width="100%"
                                height="100%"
                                preserveAspectRatio="none"
                            >
                                <line
                                    x1="0"
                                    y1="40"
                                    x2="340"
                                    y2="40"
                                    stroke="#CBD5C8"
                                    strokeWidth="2"
                                    strokeDasharray="4 4"
                                />
                                <circle
                                    cx="340"
                                    cy="40"
                                    r="3"
                                    fill="#A8BFA9"
                                    stroke="#FFFFFF"
                                    strokeWidth="1.5"
                                />
                            </svg>
                        </div>
                    </div>

                    {/* 3 Thẻ chỉ số phân bổ nhanh (Đều đưa về 0) */}
                    <div>
                        <div
                            style={{
                                fontSize: 12,
                                fontWeight: 700,
                                color: "#172018",
                                margin: "10px 0 6px",
                            }}
                        >
                            Chỉ số phân bổ nhanh
                        </div>
                        <div className="bento-mini-kpis">
                            <div className="bento-mini-kpi-card highlight">
                                <div className="bento-mini-kpi-label">
                                    <svg
                                        width="11"
                                        height="11"
                                        viewBox="0 0 24 24"
                                        fill="none"
                                        stroke="currentColor"
                                        strokeWidth="2.5"
                                    >
                                        <rect
                                            x="2"
                                            y="5"
                                            width="20"
                                            height="14"
                                            rx="2"
                                        />
                                        <circle cx="7" cy="15" r="2" />
                                        <circle cx="17" cy="15" r="2" />
                                    </svg>
                                    Bãi hoạt động
                                </div>
                                <div className="bento-mini-kpi-val">
                                    {approvedLots.length} / {realLots.length}
                                </div>
                                <div className="bento-mini-kpi-desc">
                                    {approvedLots.length > 0
                                        ? "Đang đón xe"
                                        : "Chưa có"}
                                </div>
                            </div>

                            <div className="bento-mini-kpi-card">
                                <div className="bento-mini-kpi-label">
                                    <svg
                                        width="11"
                                        height="11"
                                        viewBox="0 0 24 24"
                                        fill="none"
                                        stroke="#718071"
                                        strokeWidth="2.5"
                                    >
                                        <path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                                        <circle cx="8.5" cy="7" r="4" />
                                    </svg>
                                    Lượt gửi xe
                                </div>
                                <div className="bento-mini-kpi-val">
                                    {stats && stats.todaySessionsCount > 0
                                        ? stats.todaySessionsCount
                                        : "—"}
                                </div>
                                <div
                                    className="bento-mini-kpi-desc"
                                    style={{
                                        color:
                                            stats &&
                                            stats.todaySessionsCount > 0
                                                ? "#269B62"
                                                : "#718071",
                                    }}
                                >
                                    {stats && stats.todaySessionsCount > 0
                                        ? "Lượt đỗ hôm nay"
                                        : "Chưa có lượt đỗ"}
                                </div>
                            </div>

                            <div className="bento-mini-kpi-card">
                                <div className="bento-mini-kpi-label">
                                    <svg
                                        width="11"
                                        height="11"
                                        viewBox="0 0 24 24"
                                        fill="none"
                                        stroke="#718071"
                                        strokeWidth="2.5"
                                    >
                                        <rect
                                            x="3"
                                            y="4"
                                            width="18"
                                            height="18"
                                            rx="2"
                                        />
                                        <line x1="16" y1="2" x2="16" y2="6" />
                                        <line x1="8" y1="2" x2="8" y2="6" />
                                    </svg>
                                    Đặt giữ chỗ
                                </div>
                                <div className="bento-mini-kpi-val">
                                    {stats && stats.activeReservationsCount > 0
                                        ? stats.activeReservationsCount
                                        : "—"}
                                </div>
                                <div
                                    className="bento-mini-kpi-desc"
                                    style={{
                                        color:
                                            stats &&
                                            stats.activeReservationsCount > 0
                                                ? "#269B62"
                                                : "#718071",
                                    }}
                                >
                                    {stats && stats.activeReservationsCount > 0
                                        ? `${stats.activeReservationsCount} ô đặt trước`
                                        : "Chưa có lượt đặt"}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                {/* CỘT 2: TÌNH TRẠNG SỨC CHỨA CƠ SỞ BÃI XE (LIÊN KẾT REAL APIS) */}
                <div className="bento-card">
                    <div className="bento-card-header">
                        <div className="bento-card-title-wrap">
                            <div
                                style={{
                                    width: 22,
                                    height: 22,
                                    borderRadius: "50%",
                                    background: "#EAF7EE",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    color: "#19301D",
                                }}
                            >
                                <svg
                                    width="13"
                                    height="13"
                                    viewBox="0 0 24 24"
                                    fill="none"
                                    stroke="currentColor"
                                    strokeWidth="2.5"
                                >
                                    <rect
                                        x="2"
                                        y="5"
                                        width="20"
                                        height="14"
                                        rx="2"
                                    />
                                    <circle cx="7" cy="15" r="2" />
                                    <circle cx="17" cy="15" r="2" />
                                </svg>
                            </div>
                            <h3 className="bento-card-title">
                                Tình trạng cơ sở bãi xe ({realLots.length} cơ
                                sở)
                            </h3>
                        </div>
                        <Link
                            href="/owner/parking-lots"
                            style={{
                                fontSize: 12,
                                fontWeight: 700,
                                color: "#172018",
                                textDecoration: "none",
                            }}
                        >
                            Quản lý tất cả →
                        </Link>
                    </div>

                    {/* Danh sách cơ sở thực tế hoặc Trạng thái trống */}
                    <div
                        style={{
                            display: "flex",
                            flexDirection: "column",
                            gap: 10,
                        }}
                    >
                        {realLots.length > 0 ? (
                            realLots.slice(0, 3).map((lot, idx) => {
                                const isApproved =
                                    lot.approvalStatus?.toLowerCase() ===
                                    "approved";
                                const isPending =
                                    lot.approvalStatus?.toLowerCase() ===
                                    "pending";
                                const fallbackImg =
                                    idx === 0
                                        ? "/images/parkwise/dashboard-parking-garage.webp"
                                        : idx === 1
                                          ? "/images/parkwise/dashboard-parking-structure.webp"
                                          : "/images/parkwise/dashboard-parking-entrance.webp";

                                return (
                                    <div
                                        key={lot.id}
                                        className="bento-lot-item"
                                    >
                                        <Image
                                            src={fallbackImg}
                                            alt={lot.name}
                                            width={48}
                                            height={48}
                                            className="bento-lot-thumb"
                                        />
                                        <div className="bento-lot-info">
                                            <div className="bento-lot-name-row">
                                                <h4 className="bento-lot-name">
                                                    {lot.name}
                                                </h4>
                                                <span className="bento-lot-tag">
                                                    {lot.type || "Bãi xe"}
                                                </span>
                                            </div>

                                            <div
                                                style={{
                                                    fontSize: 11.5,
                                                    color: "#718071",
                                                    marginBottom: 6,
                                                    whiteSpace: "nowrap",
                                                    overflow: "hidden",
                                                    textOverflow: "ellipsis",
                                                }}
                                            >
                                                {lot.address}
                                            </div>

                                            {/* Thao tác nhanh cho bãi xe */}
                                            <div
                                                style={{
                                                    display: "flex",
                                                    gap: 6,
                                                    flexWrap: "wrap",
                                                }}
                                            >
                                                <Link
                                                    href={`/owner/layout/2d`}
                                                    style={{
                                                        fontSize: 11,
                                                        fontWeight: 700,
                                                        color: "#0284c7",
                                                        textDecoration: "none",
                                                        background: "#f0f9ff",
                                                        padding: "2px 7px",
                                                        borderRadius: 4,
                                                    }}
                                                >
                                                    Sơ đồ 2D
                                                </Link>
                                                <Link
                                                    href={`/owner/layout/3d`}
                                                    style={{
                                                        fontSize: 11,
                                                        fontWeight: 700,
                                                        color: "#475569",
                                                        textDecoration: "none",
                                                        background: "#f8fafc",
                                                        padding: "2px 7px",
                                                        borderRadius: 4,
                                                    }}
                                                >
                                                    Sơ đồ 3D
                                                </Link>
                                                <Link
                                                    href={`/owner/pricing`}
                                                    style={{
                                                        fontSize: 11,
                                                        fontWeight: 700,
                                                        color: "#475569",
                                                        textDecoration: "none",
                                                        background: "#f8fafc",
                                                        padding: "2px 7px",
                                                        borderRadius: 4,
                                                    }}
                                                >
                                                    Bảng giá
                                                </Link>
                                                <Link
                                                    href={`/owner/lots/${lot.id}/policies`}
                                                    style={{
                                                        fontSize: 11,
                                                        fontWeight: 700,
                                                        color: "#1e19d8",
                                                        textDecoration: "none",
                                                        background: "#eef2ff",
                                                        padding: "2px 7px",
                                                        borderRadius: 4,
                                                    }}
                                                >
                                                    Cọc & 30/70
                                                </Link>
                                            </div>
                                        </div>

                                        <div className="bento-lot-side">
                                            <div
                                                style={{
                                                    fontSize: 11.5,
                                                    fontWeight: 750,
                                                    color: isApproved
                                                        ? "#269B62"
                                                        : isPending
                                                          ? "#D97706"
                                                          : "#DC4545",
                                                    display: "inline-flex",
                                                    alignItems: "center",
                                                    gap: 4,
                                                }}
                                            >
                                                <span
                                                    style={{
                                                        width: 6,
                                                        height: 6,
                                                        borderRadius: "50%",
                                                        background: isApproved
                                                            ? "#269B62"
                                                            : isPending
                                                              ? "#D97706"
                                                              : "#DC4545",
                                                    }}
                                                />
                                                {isApproved
                                                    ? "Đang đón xe"
                                                    : isPending
                                                      ? "Chờ duyệt"
                                                      : "Bị từ chối"}
                                            </div>
                                        </div>
                                    </div>
                                );
                            })
                        ) : (
                            <DashboardEmptyState
                                title="Chưa có cơ sở bãi xe nào trong hệ thống"
                                description="Bạn cần đăng ký thông tin bãi xe và chờ Quản trị viên duyệt để kích hoạt vận hành."
                                actionHref="/owner/parking-lots/new"
                                actionLabel="+ Đăng ký bãi xe đầu tiên →"
                            />
                        )}
                    </div>
                </div>

                {/* CỘT 3: LƯU LƯỢNG 7 NGÀY & VẬN HÀNH THÔNG MINH (ĐƯA VỀ 0) */}
                <div className="bento-right-col">
                    <div
                        className="bento-card"
                        style={{ padding: "16px 18px" }}
                    >
                        <div
                            className="bento-card-header"
                            style={{ marginBottom: 6 }}
                        >
                            <div className="bento-card-title-wrap">
                                <svg
                                    width="14"
                                    height="14"
                                    viewBox="0 0 24 24"
                                    fill="none"
                                    stroke="#718071"
                                    strokeWidth="2.5"
                                >
                                    <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
                                </svg>
                                <h3
                                    className="bento-card-title"
                                    style={{ fontSize: 13.5 }}
                                >
                                    Lưu lượng 7 ngày qua
                                </h3>
                            </div>
                            <span
                                style={{
                                    fontSize: 11,
                                    fontWeight: 700,
                                    color: "#718071",
                                }}
                            >
                                0%
                            </span>
                        </div>

                        {/* Cột 7 ngày đều ở mức 0 */}
                        <div className="bento-bars-grid">
                            {["T2", "T3", "T4", "T5", "T6", "T7", "CN"].map(
                                (day) => (
                                    <div key={day} className="bento-bar-col">
                                        <div
                                            className="bento-bar-rect"
                                            style={{
                                                height: 6,
                                                background: "#E1E7DE",
                                            }}
                                        />
                                        <div className="bento-bar-label">
                                            {day}
                                        </div>
                                        <div className="bento-bar-sub">0</div>
                                    </div>
                                ),
                            )}
                        </div>
                    </div>

                    <div className="bento-smart-card">
                        <div
                            style={{
                                position: "relative",
                                zIndex: 2,
                                maxWidth: "62%",
                            }}
                        >
                            <div
                                style={{
                                    fontSize: 9.5,
                                    fontWeight: 800,
                                    letterSpacing: 0.8,
                                    color: "#B8F34A",
                                    marginBottom: 3,
                                    textTransform: "uppercase",
                                }}
                            >
                                VẬN HÀNH THÔNG MINH
                            </div>
                            <div
                                style={{
                                    fontSize: 13,
                                    fontWeight: 800,
                                    color: "#FFFFFF",
                                    lineHeight: 1.3,
                                    marginBottom: 4,
                                }}
                            >
                                Bật tự động giá cao điểm chiều nay?
                            </div>
                            <div
                                style={{
                                    fontSize: 10.5,
                                    color: "#A8BFA9",
                                    lineHeight: 1.3,
                                    marginBottom: 10,
                                }}
                            >
                                Tự động tăng +20% biểu phí từ 17:00 - 19:00 tại
                                các bãi trung tâm.
                            </div>
                            <Link
                                href="/owner/pricing"
                                style={{
                                    display: "inline-block",
                                    background: "#B8F34A",
                                    color: "#19301D",
                                    fontSize: 11,
                                    fontWeight: 800,
                                    padding: "5px 12px",
                                    borderRadius: 6,
                                    textDecoration: "none",
                                    boxShadow: "0 2px 6px rgba(0,0,0,0.2)",
                                }}
                            >
                                Kiểm tra cấu hình →
                            </Link>
                        </div>

                        <div
                            style={{
                                position: "relative",
                                zIndex: 2,
                                display: "flex",
                                flexDirection: "column",
                                alignItems: "flex-end",
                                gap: 12,
                            }}
                        >
                            <button
                                type="button"
                                onClick={() =>
                                    setSurgeAutoEnabled(!surgeAutoEnabled)
                                }
                                style={{
                                    width: 44,
                                    height: 24,
                                    borderRadius: 999,
                                    background: surgeAutoEnabled
                                        ? "#B8F34A"
                                        : "#324A36",
                                    border: "none",
                                    cursor: "pointer",
                                    padding: 2,
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: surgeAutoEnabled
                                        ? "flex-end"
                                        : "flex-start",
                                    transition: "all 0.2s ease",
                                }}
                            >
                                <span
                                    style={{
                                        width: 20,
                                        height: 20,
                                        borderRadius: "50%",
                                        background: surgeAutoEnabled
                                            ? "#19301D"
                                            : "#A8BFA9",
                                    }}
                                />
                            </button>
                        </div>

                        <Image
                            src="/images/parkwise/vehicle-sedan-cutout.png"
                            alt="Xe sedan ParkWise"
                            width={145}
                            height={75}
                            className="bento-smart-card-bg-car"
                            style={{ objectFit: "contain" }}
                        />
                    </div>
                </div>
            </div>

            {/* ── ROW 2: LINE CHART + PAYMENT BREAKDOWN (ĐƯA VỀ 0) ── */}
            <div className="bento-row-middle">
                {/* THẺ BIỂU ĐỒ ĐƯỜNG: BIẾN ĐỘNG LƯU LƯỢNG XE VÀO/RA (MỨC 0) */}
                <div className="bento-card">
                    <div className="bento-card-header">
                        <div className="bento-card-title-wrap">
                            <div
                                style={{
                                    width: 22,
                                    height: 22,
                                    borderRadius: "50%",
                                    background: "#EAF7EE",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    color: "#19301D",
                                }}
                            >
                                <svg
                                    width="13"
                                    height="13"
                                    viewBox="0 0 24 24"
                                    fill="none"
                                    stroke="currentColor"
                                    strokeWidth="2.5"
                                >
                                    <line x1="18" y1="20" x2="18" y2="10" />
                                    <line x1="12" y1="20" x2="12" y2="4" />
                                    <line x1="6" y1="20" x2="6" y2="14" />
                                </svg>
                            </div>
                            <h3 className="bento-card-title">
                                Biến động lưu lượng xe vào/ra theo khung giờ
                                (06:00 - 23:00)
                            </h3>
                        </div>

                        {/* Controls Header Card: Bộ lọc thời gian + Chú thích Vào/Ra */}
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 16,
                                flexWrap: "wrap",
                            }}
                        >
                            <div className="bento-timeframe-pills">
                                <button
                                    type="button"
                                    className={`bento-timeframe-btn ${chartTimeframe === "today" ? "active" : ""}`}
                                    onClick={() => {
                                        setChartTimeframe("today");
                                        setActivePointIndex(4);
                                    }}
                                >
                                    Hôm nay
                                </button>
                                <button
                                    type="button"
                                    className={`bento-timeframe-btn ${chartTimeframe === "week" ? "active" : ""}`}
                                    onClick={() => {
                                        setChartTimeframe("week");
                                        setActivePointIndex(4);
                                    }}
                                >
                                    7 ngày qua
                                </button>
                                <button
                                    type="button"
                                    className={`bento-timeframe-btn ${chartTimeframe === "month" ? "active" : ""}`}
                                    onClick={() => {
                                        setChartTimeframe("month");
                                        setActivePointIndex(2);
                                    }}
                                >
                                    Tháng này
                                </button>
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 12,
                                    fontSize: 12,
                                    fontWeight: 700,
                                }}
                            >
                                <div
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 5,
                                    }}
                                >
                                    <span
                                        style={{
                                            width: 8,
                                            height: 8,
                                            borderRadius: "50%",
                                            background: "#19301D",
                                        }}
                                    />
                                    <span style={{ color: "#172018" }}>
                                        Vào
                                    </span>
                                </div>
                                <div
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 5,
                                    }}
                                >
                                    <span
                                        style={{
                                            width: 8,
                                            height: 8,
                                            borderRadius: "50%",
                                            background: "#A7E34A",
                                        }}
                                    />
                                    <span style={{ color: "#718071" }}>Ra</span>
                                </div>
                            </div>
                        </div>
                    </div>

                    {/* SVG Line Chart: Baseline mức 0 */}
                    <div
                        className="bento-chart-container"
                        style={{ height: 180 }}
                    >
                        <svg
                            viewBox="0 0 600 180"
                            width="100%"
                            height="100%"
                            preserveAspectRatio="none"
                        >
                            {/* Horizontal Grid lines */}
                            {[0, 20, 40, 60, 80, 100].map((val, idx) => {
                                const y = 160 - (val / 100) * 110;
                                return (
                                    <g key={idx}>
                                        <line
                                            x1="40"
                                            y1={y}
                                            x2="580"
                                            y2={y}
                                            stroke="#EEF2EB"
                                            strokeWidth="1"
                                            strokeDasharray="3 3"
                                        />
                                        <text
                                            x="32"
                                            y={y + 3}
                                            textAnchor="end"
                                            fontSize="10"
                                            fill="#718071"
                                            fontWeight="600"
                                        >
                                            {val}
                                        </text>
                                    </g>
                                );
                            })}

                            {/* Baseline mức 0 */}
                            <path
                                d={inPath}
                                fill="none"
                                stroke="#19301D"
                                strokeWidth="2.6"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            />
                            <path
                                d={outPath}
                                fill="none"
                                stroke="#A7E34A"
                                strokeWidth="2.2"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            />

                            {/* Các điểm dữ liệu tương tác đều là 0 */}
                            {trafficData.map((pt, idx) => (
                                <g
                                    key={idx}
                                    style={{ cursor: "pointer" }}
                                    onClick={() => setActivePointIndex(idx)}
                                >
                                    <circle
                                        cx={pt.x}
                                        cy={pt.yOut}
                                        r="3.5"
                                        fill="#A7E34A"
                                        stroke="#FFFFFF"
                                        strokeWidth="1.5"
                                    />
                                    <circle
                                        cx={pt.x}
                                        cy={pt.yIn}
                                        r={
                                            activePointIndex === idx
                                                ? "5.5"
                                                : "3.5"
                                        }
                                        fill="#19301D"
                                        stroke={
                                            activePointIndex === idx
                                                ? "#B8F34A"
                                                : "#FFFFFF"
                                        }
                                        strokeWidth={
                                            activePointIndex === idx
                                                ? "2.5"
                                                : "1.5"
                                        }
                                    />
                                    <text
                                        x={pt.x}
                                        y="174"
                                        textAnchor="middle"
                                        fontSize="10.5"
                                        fill={
                                            activePointIndex === idx
                                                ? "#172018"
                                                : "#718071"
                                        }
                                        fontWeight={
                                            activePointIndex === idx
                                                ? "800"
                                                : "500"
                                        }
                                    >
                                        {pt.time}
                                    </text>
                                </g>
                            ))}
                        </svg>

                        {/* Tooltip Box nổi bật: 0 xe */}
                        {currentPoint && (
                            <div
                                className="bento-tooltip-box"
                                style={{
                                    left: `${(currentPoint.x / 600) * 100}%`,
                                    top: `${(currentPoint.yIn / 180) * 100}%`,
                                }}
                            >
                                <span
                                    style={{
                                        color: "#19301D",
                                        fontWeight: 800,
                                    }}
                                >
                                    {currentPoint.time}
                                </span>
                                <span
                                    style={{
                                        margin: "0 6px",
                                        color: "#E1E7DE",
                                    }}
                                >
                                    |
                                </span>
                                <span style={{ color: "#19301D" }}>
                                    Vào: <strong>0 xe</strong>
                                </span>
                                <span
                                    style={{
                                        margin: "0 6px",
                                        color: "#E1E7DE",
                                    }}
                                >
                                    |
                                </span>
                                <span style={{ color: "#718071" }}>
                                    Ra: <strong>0 xe</strong>
                                </span>
                            </div>
                        )}
                    </div>
                </div>

                {/* THẺ CƠ CẤU DOANH THU THEO PHƯƠNG THỨC (ĐƯA VỀ 0) */}
                <div
                    className="bento-card"
                    style={{
                        display: "flex",
                        flexDirection: "column",
                        justifyContent: "space-between",
                    }}
                >
                    <div>
                        <div className="bento-card-header">
                            <div className="bento-card-title-wrap">
                                <div
                                    style={{
                                        width: 22,
                                        height: 22,
                                        borderRadius: "50%",
                                        background: "#EAF7EE",
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "center",
                                        color: "#19301D",
                                    }}
                                >
                                    <svg
                                        width="13"
                                        height="13"
                                        viewBox="0 0 24 24"
                                        fill="none"
                                        stroke="currentColor"
                                        strokeWidth="2.5"
                                    >
                                        <circle cx="12" cy="12" r="10" />
                                        <path d="M12 2a10 10 0 0 1 10 10" />
                                    </svg>
                                </div>
                                <h3 className="bento-card-title">
                                    Cơ cấu doanh thu theo phương thức
                                </h3>
                            </div>
                            <span
                                style={{
                                    fontSize: 11.5,
                                    fontWeight: 700,
                                    color: "#718071",
                                    background: "#F1F5F9",
                                    padding: "2px 8px",
                                    borderRadius: 999,
                                }}
                            >
                                0%
                            </span>
                        </div>

                        {/* Phương thức 1: Ví ParkWise / VNPay (QR) 0% */}
                        <div className="bento-pay-item">
                            <div className="bento-pay-header">
                                <span>Ví ParkWise / VNPay (QR)</span>
                                <span>
                                    0 đ{" "}
                                    <span className="bento-pay-sub">(0%)</span>
                                </span>
                            </div>
                            <div className="bento-pay-bar-wrap">
                                <div
                                    className="bento-pay-bar-fill"
                                    style={{ width: "0%" }}
                                />
                            </div>
                        </div>

                        {/* Phương thức 2: Tiền mặt tại bốt trực 0% */}
                        <div
                            className="bento-pay-item"
                            style={{ marginBottom: 16 }}
                        >
                            <div className="bento-pay-header">
                                <span style={{ color: "#718071" }}>
                                    Tiền mặt tại bốt trực
                                </span>
                                <span style={{ color: "#718071" }}>
                                    0 đ{" "}
                                    <span className="bento-pay-sub">(0%)</span>
                                </span>
                            </div>
                            <div className="bento-pay-bar-wrap">
                                <div
                                    className="bento-pay-bar-fill cash"
                                    style={{ width: "0%" }}
                                />
                            </div>
                        </div>
                    </div>

                    {/* Banner phụ: 0% */}
                    <div className="bento-highlight-banner">
                        <div>
                            <div
                                style={{
                                    fontSize: 11,
                                    color: "#718071",
                                    fontWeight: 600,
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 4,
                                    marginBottom: 2,
                                }}
                            >
                                <span style={{ color: "#718071" }}>↗</span> Tỷ
                                lệ thanh toán thành công
                            </div>
                            <div
                                style={{
                                    fontSize: 16,
                                    fontWeight: 850,
                                    color: "#172018",
                                }}
                            >
                                0%{" "}
                                <span
                                    style={{
                                        fontSize: 12,
                                        fontWeight: 600,
                                        color: "#718071",
                                    }}
                                >
                                    chưa có giao dịch
                                </span>
                            </div>
                        </div>

                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 12,
                            }}
                        >
                            <Link
                                href="/owner/reports"
                                style={{
                                    fontSize: 11.5,
                                    fontWeight: 750,
                                    color: "#172018",
                                    textDecoration: "none",
                                    padding: "5px 10px",
                                    background: "#FFFFFF",
                                    border: "1px solid #E1E7DE",
                                    borderRadius: 6,
                                }}
                            >
                                Xem báo cáo →
                            </Link>
                        </div>
                    </div>
                </div>
            </div>

            {/* ── ROW 3: BẢNG PHIÊN XE RA/VÀO TRỰC TIẾP TẠI BỐT KIỂM SOÁT (BẢNG TRỐNG) ── */}
            <div
                className="bento-card"
                style={{ padding: 0, overflow: "hidden" }}
            >
                <div
                    style={{
                        padding: "16px 20px",
                        borderBottom: "1px solid #E1E7DE",
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                        flexWrap: "wrap",
                        gap: 12,
                    }}
                >
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 10,
                        }}
                    >
                        <div
                            style={{
                                width: 22,
                                height: 22,
                                borderRadius: "50%",
                                background: "#EAF7EE",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                color: "#19301D",
                            }}
                        >
                            <svg
                                width="13"
                                height="13"
                                viewBox="0 0 24 24"
                                fill="none"
                                stroke="currentColor"
                                strokeWidth="2.5"
                            >
                                <rect
                                    x="3"
                                    y="11"
                                    width="18"
                                    height="11"
                                    rx="2"
                                />
                                <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                            </svg>
                        </div>
                        <h3 className="bento-card-title">
                            Phiên xe ra/vào trực tiếp tại bốt kiểm soát
                        </h3>
                        <span
                            style={{
                                fontSize: 11.5,
                                fontWeight: 700,
                                color: "#269B62",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 5,
                            }}
                        >
                            <span
                                style={{
                                    width: 6,
                                    height: 6,
                                    borderRadius: "50%",
                                    background: "#269B62",
                                }}
                            />
                            Đang hoạt động
                        </span>
                    </div>

                    <Link
                        href="/owner/sessions"
                        style={{
                            fontSize: 12.5,
                            fontWeight: 700,
                            color: "#172018",
                            textDecoration: "none",
                        }}
                    >
                        Xem tất cả phiên →
                    </Link>
                </div>

                <div className="bento-table-wrap">
                    {stats && stats.recentSessions.length > 0 ? (
                        <table className="bento-table">
                            <thead>
                                <tr>
                                    <th>MÃ PHIÊN</th>
                                    <th>BIỂN SỐ XE</th>
                                    <th>LOẠI XE</th>
                                    <th>VỊ TRÍ Ô</th>
                                    <th>GIỜ VÀO</th>
                                    <th>THỜI LƯỢNG</th>
                                    <th>PHÍ TẠM TÍNH</th>
                                    <th>TRẠNG THÁI</th>
                                </tr>
                            </thead>
                            <tbody>
                                {stats.recentSessions.map((session) => (
                                    <tr key={session.sessionId}>
                                        <td
                                            style={{
                                                fontFamily: "monospace",
                                                fontSize: 12,
                                            }}
                                        >
                                            {session.sessionId.slice(0, 8)}...
                                        </td>
                                        <td style={{ fontWeight: 700 }}>
                                            {session.plateNumber}
                                        </td>
                                        <td>{session.vehicleType}</td>
                                        <td>
                                            {session.slotCode ||
                                                "Khu vực chung"}
                                        </td>
                                        <td>{session.entryTime}</td>
                                        <td>{session.durationMinutes} phút</td>
                                        <td
                                            style={{
                                                fontWeight: 700,
                                                color: "#19301D",
                                            }}
                                        >
                                            {new Intl.NumberFormat(
                                                "vi-VN",
                                            ).format(session.interimFee)}{" "}
                                            đ
                                        </td>
                                        <td>
                                            <span
                                                style={{
                                                    padding: "2px 8px",
                                                    borderRadius: 4,
                                                    fontSize: 11,
                                                    fontWeight: 700,
                                                    background:
                                                        session.status ===
                                                        "Active"
                                                            ? "#EAF7EE"
                                                            : "#F1F5F9",
                                                    color:
                                                        session.status ===
                                                        "Active"
                                                            ? "#269B62"
                                                            : "#64748B",
                                                }}
                                            >
                                                {session.status === "Active"
                                                    ? "Đang đỗ"
                                                    : session.status}
                                            </span>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    ) : (
                        <div style={{ padding: "16px" }}>
                            <DashboardEmptyState
                                title="Chưa phát sinh phiên xe nào trong ngày hôm nay"
                                description="Hệ thống Camera AI OCR và bốt kiểm soát sẽ tự động ghi nhận và hiển thị các phiên gửi xe thời gian thực ngay khi có phương tiện check-in qua cổng."
                                actionHref="/owner/sessions"
                                actionLabel="Xem lịch sử phiên đỗ xe →"
                            />
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
}
