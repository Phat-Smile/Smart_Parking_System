"use client";

import React, { useEffect, useState, useSyncExternalStore } from "react";
import Link from "next/link";
import { OwnerPageHeader } from "@/components/layout/OwnerPageHeader";
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

    const [realLots, setRealLots] = useState<OwnerParkingLotItem[]>([]);
    const [application, setApplication] = useState<MyOwnerApplicationDto | null>(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        let active = true;
        async function fetchDashboardData() {
            setLoading(true);
            try {
                const [appRes, lotsRes] = await Promise.all([
                    getMyOwnerApplication().catch(() => null),
                    getOwnerParkingLotsApi().catch(() => ({
                        items: [],
                        totalCount: 0,
                        page: 1,
                        pageSize: 20,
                    })),
                ]);

                if (active) {
                    setApplication(appRes);
                    setRealLots(lotsRes?.items || []);
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
    }, []);

    const approvedLots = realLots.filter(
        (l) => l.approvalStatus?.toLowerCase() === "approved",
    );
    const pendingLots = realLots.filter(
        (l) => l.approvalStatus?.toLowerCase() === "pending",
    );

    if (loading) {
        return (
            <div style={{ maxWidth: 1400, margin: "0 auto", paddingBottom: 48 }}>
                <OwnerPageHeader
                    breadcrumb="/owner/dashboard"
                    title="Tổng quan vận hành"
                    subtitle="Quản lý doanh thu, bãi xe và hoạt động kinh doanh"
                />
                <div
                    style={{
                        padding: "60px 20px",
                        textAlign: "center",
                        color: "var(--pw-dark-muted)",
                        background: "#fff",
                        borderRadius: 16,
                        border: "1px solid #e3e8f2",
                        marginTop: 20,
                    }}
                >
                    <div style={{ fontSize: 24, marginBottom: 12 }}>⚡</div>
                    <p style={{ fontWeight: 600 }}>Đang tải dữ liệu vận hành bãi xe...</p>
                </div>
            </div>
        );
    }

    // Pending Owner Application Notice (Nếu chưa được duyệt chính thức)
    const isPendingApproval =
        application?.status === "Pending" &&
        session?.account?.role !== "ParkingLotOwner";

    return (
        <div style={{ maxWidth: 1400, margin: "0 auto", paddingBottom: 48 }}>
            <OwnerPageHeader
                breadcrumb="/owner/dashboard"
                title="Tổng quan vận hành"
                subtitle="Quản lý doanh thu, bãi xe và hoạt động kinh doanh"
            />

            {/* Thông báo nếu tài khoản còn đang chờ duyệt hồ sơ */}
            {isPendingApproval && (
                <div
                    style={{
                        background: "#fefce8",
                        border: "1.5px solid #fde047",
                        borderRadius: 14,
                        padding: "16px 20px",
                        marginBottom: 24,
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        flexWrap: "wrap",
                        gap: 12,
                    }}
                >
                    <div>
                        <div style={{ fontWeight: 800, color: "#854d0e", fontSize: 15 }}>
                            ⏳ Hồ sơ Đối tác đang chờ Ban quản trị ParkWise phê duyệt
                        </div>
                        <p style={{ margin: "4px 0 0", color: "#a16207", fontSize: 13.5 }}>
                            Hồ sơ pháp lý của cơ sở <strong>{application?.businessName || displayName}</strong> đang trong hàng đợi thẩm định. Bạn có thể xem bản mô phỏng hoặc theo dõi tiến trình.
                        </p>
                    </div>
                    <div style={{ display: "flex", gap: 10 }}>
                        <Link
                            href="/owner/onboarding"
                            style={{
                                background: "#ca8a04",
                                color: "#fff",
                                padding: "8px 16px",
                                borderRadius: 8,
                                fontWeight: 700,
                                fontSize: 13,
                                textDecoration: "none",
                            }}
                        >
                            Theo dõi hồ sơ
                        </Link>
                        <Link
                            href="/owner/simulation"
                            style={{
                                background: "#fff",
                                border: "1px solid #ca8a04",
                                color: "#854d0e",
                                padding: "8px 16px",
                                borderRadius: 8,
                                fontWeight: 700,
                                fontSize: 13,
                                textDecoration: "none",
                            }}
                        >
                            Xem bản mô phỏng 🎮
                        </Link>
                    </div>
                </div>
            )}

            {/* 4 Thẻ KPI Thực tế */}
            <div
                style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
                    gap: 16,
                    marginBottom: 24,
                }}
            >
                <div
                    style={{
                        background: "#fff",
                        borderRadius: 16,
                        border: "1px solid #e3e8f2",
                        padding: "20px 22px",
                        boxShadow: "0 2px 6px rgba(15,27,61,0.04)",
                    }}
                >
                    <div style={{ fontSize: 13, color: "#64748b", fontWeight: 700, textTransform: "uppercase" }}>
                        Tổng số bãi xe
                    </div>
                    <div style={{ fontSize: 32, fontWeight: 800, color: "#0f172a", marginTop: 8 }}>
                        {realLots.length} <span style={{ fontSize: 16, fontWeight: 600, color: "#64748b" }}>bãi</span>
                    </div>
                    <div style={{ fontSize: 12.5, color: realLots.length > 0 ? "#16a34a" : "#64748b", marginTop: 6, fontWeight: 600 }}>
                        {realLots.length > 0 ? `${approvedLots.length} bãi đang hoạt động` : "Chưa có bãi xe nào"}
                    </div>
                </div>

                <div
                    style={{
                        background: "#fff",
                        borderRadius: 16,
                        border: "1px solid #e3e8f2",
                        padding: "20px 22px",
                        boxShadow: "0 2px 6px rgba(15,27,61,0.04)",
                    }}
                >
                    <div style={{ fontSize: 13, color: "#64748b", fontWeight: 700, textTransform: "uppercase" }}>
                        Bãi xe đang hoạt động
                    </div>
                    <div style={{ fontSize: 32, fontWeight: 800, color: "#16a34a", marginTop: 8 }}>
                        {approvedLots.length}
                    </div>
                    <div style={{ fontSize: 12.5, color: "#64748b", marginTop: 6 }}>
                        Đã được duyệt & sẵn sàng nhận xe
                    </div>
                </div>

                <div
                    style={{
                        background: "#fff",
                        borderRadius: 16,
                        border: "1px solid #e3e8f2",
                        padding: "20px 22px",
                        boxShadow: "0 2px 6px rgba(15,27,61,0.04)",
                    }}
                >
                    <div style={{ fontSize: 13, color: "#64748b", fontWeight: 700, textTransform: "uppercase" }}>
                        Bãi xe chờ duyệt
                    </div>
                    <div style={{ fontSize: 32, fontWeight: 800, color: "#d97706", marginTop: 8 }}>
                        {pendingLots.length}
                    </div>
                    <div style={{ fontSize: 12.5, color: "#64748b", marginTop: 6 }}>
                        Đang chờ Ban quản trị phê duyệt
                    </div>
                </div>

                <div
                    style={{
                        background: "#fff",
                        borderRadius: 16,
                        border: "1px solid #e3e8f2",
                        padding: "20px 22px",
                        boxShadow: "0 2px 6px rgba(15,27,61,0.04)",
                    }}
                >
                    <div style={{ fontSize: 13, color: "#64748b", fontWeight: 700, textTransform: "uppercase" }}>
                        Doanh thu hôm nay
                    </div>
                    <div style={{ fontSize: 32, fontWeight: 800, color: "#0f172a", marginTop: 8 }}>
                        0 <span style={{ fontSize: 18, fontWeight: 700, color: "#64748b" }}>₫</span>
                    </div>
                    <div style={{ fontSize: 12.5, color: "#64748b", marginTop: 6 }}>
                        Chưa phát sinh phiên thanh toán
                    </div>
                </div>
            </div>

            {/* TRƯỜNG HỢP 1: CHỦ BÃI CHƯA CÓ BÃI XE NÀO (CHÀO MỪNG ĐỐI TÁC MỚI) */}
            {realLots.length === 0 && (
                <div
                    style={{
                        background: "#fff",
                        borderRadius: 20,
                        border: "1px solid #e3e8f2",
                        padding: "36px 32px",
                        boxShadow: "0 4px 16px rgba(15,27,61,0.04)",
                        marginBottom: 28,
                    }}
                >
                    <div
                        style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: 6,
                            background: "#def7ec",
                            color: "#0e9f6e",
                            padding: "6px 14px",
                            borderRadius: 999,
                            fontSize: 12.5,
                            fontWeight: 700,
                            marginBottom: 16,
                        }}
                    >
                        <span>✓</span> Tài khoản Chủ bãi chính thức (Active)
                    </div>

                    <h2
                        style={{
                            fontSize: 24,
                            fontWeight: 800,
                            color: "#0f172a",
                            margin: "0 0 10px",
                        }}
                    >
                        Chào mừng {displayName} đến với ParkWise Partner!
                    </h2>
                    <p
                        style={{
                            fontSize: 15,
                            color: "#475569",
                            lineHeight: 1.6,
                            maxWidth: 780,
                            margin: "0 0 24px",
                        }}
                    >
                        Tư cách Chủ bãi của cơ sở{" "}
                        <strong>{application?.businessName || displayName}</strong>{" "}
                        đã được thẩm định thành công. Hiện tại bạn chưa có cơ sở bãi đỗ xe nào trong hệ thống. Hãy đăng ký bãi xe đầu tiên để bắt đầu cấu hình sơ đồ ô đỗ 2D/3D, cài đặt bảng giá và đón khách hàng.
                    </p>

                    <div style={{ display: "flex", gap: 14, flexWrap: "wrap", marginBottom: 36 }}>
                        <Link
                            href="/owner/parking-lots/new"
                            style={{
                                background: "#E6F85E",
                                color: "#09090B",
                                padding: "12px 24px",
                                borderRadius: 10,
                                fontWeight: 800,
                                fontSize: 14.5,
                                textDecoration: "none",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 8,
                                boxShadow: "0 2px 8px rgba(230,248,94,0.3)",
                            }}
                        >
                            + Đăng ký bãi đỗ xe đầu tiên →
                        </Link>
                        <Link
                            href="/owner/simulation"
                            style={{
                                background: "#f8fafc",
                                border: "1px solid #cbd5e1",
                                color: "#334155",
                                padding: "12px 20px",
                                borderRadius: 10,
                                fontWeight: 700,
                                fontSize: 14,
                                textDecoration: "none",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 8,
                            }}
                        >
                            🎮 Khám phá Bản mô phỏng mẫu (Sandbox)
                        </Link>
                    </div>

                    {/* Lộ trình 4 bước khởi tạo bãi xe */}
                    <div style={{ borderTop: "1px solid #f1f5f9", paddingTop: 28 }}>
                        <h4 style={{ fontSize: 15, fontWeight: 800, color: "#0f172a", marginBottom: 16 }}>
                            Lộ trình 4 bước chuẩn bị vận hành cơ sở bãi xe:
                        </h4>
                        <div
                            style={{
                                display: "grid",
                                gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
                                gap: 16,
                            }}
                        >
                            <div style={{ background: "#f8fafc", padding: 18, borderRadius: 12, border: "1px solid #e2e8f0" }}>
                                <div style={{ fontSize: 20, marginBottom: 8 }}>📍</div>
                                <div style={{ fontWeight: 700, fontSize: 14, color: "#0f172a", marginBottom: 4 }}>
                                    1. Khai báo địa điểm
                                </div>
                                <div style={{ fontSize: 12.5, color: "#64748b", lineHeight: 1.5 }}>
                                    Nhập tên bãi xe, địa chỉ thực tế, toạ độ GPS bản đồ và sức chứa thiết kế.
                                </div>
                            </div>

                            <div style={{ background: "#f8fafc", padding: 18, borderRadius: 12, border: "1px solid #e2e8f0" }}>
                                <div style={{ fontSize: 20, marginBottom: 8 }}>📐</div>
                                <div style={{ fontWeight: 700, fontSize: 14, color: "#0f172a", marginBottom: 4 }}>
                                    2. Thiết lập sơ đồ
                                </div>
                                <div style={{ fontSize: 12.5, color: "#64748b", lineHeight: 1.5 }}>
                                    Vẽ sơ đồ ô đỗ 2D/3D trực quan, phân chia khu vực đỗ xe ô tô và xe máy.
                                </div>
                            </div>

                            <div style={{ background: "#f8fafc", padding: 18, borderRadius: 12, border: "1px solid #e2e8f0" }}>
                                <div style={{ fontSize: 20, marginBottom: 8 }}>💵</div>
                                <div style={{ fontWeight: 700, fontSize: 14, color: "#0f172a", marginBottom: 4 }}>
                                    3. Cài đặt bảng giá
                                </div>
                                <div style={{ fontSize: 12.5, color: "#64748b", lineHeight: 1.5 }}>
                                    Cấu hình biểu phí giữ xe theo giờ, phí qua đêm, tiền cọc và thời gian mở cửa 24/7.
                                </div>
                            </div>

                            <div style={{ background: "#f8fafc", padding: 18, borderRadius: 12, border: "1px solid #e2e8f0" }}>
                                <div style={{ fontSize: 20, marginBottom: 8 }}>🛡️</div>
                                <div style={{ fontWeight: 700, fontSize: 14, color: "#0f172a", marginBottom: 4 }}>
                                    4. Thẩm duyệt bãi xe
                                </div>
                                <div style={{ fontSize: 12.5, color: "#64748b", lineHeight: 1.5 }}>
                                    Gửi bãi xe để Admin duyệt từng cơ sở. Bãi xe sau khi duyệt sẽ lập tức mở đón khách.
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* TRƯỜNG HỢP 2: ĐÃ CÓ BÃI XE TRONG HỆ THỐNG */}
            {realLots.length > 0 && (
                <div style={{ marginBottom: 24 }}>
                    <div
                        style={{
                            display: "flex",
                            justifyContent: "space-between",
                            alignItems: "center",
                            marginBottom: 16,
                            flexWrap: "wrap",
                            gap: 12,
                        }}
                    >
                        <h3 style={{ fontSize: 18, fontWeight: 800, color: "#0f172a", margin: 0 }}>
                            Danh sách bãi đỗ xe của bạn ({realLots.length})
                        </h3>
                        <Link
                            href="/owner/parking-lots/new"
                            style={{
                                background: "#0f172a",
                                color: "#fff",
                                padding: "9px 18px",
                                borderRadius: 10,
                                fontWeight: 700,
                                fontSize: 13,
                                textDecoration: "none",
                            }}
                        >
                            + Thêm bãi xe mới
                        </Link>
                    </div>

                    <div
                        style={{
                            display: "grid",
                            gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))",
                            gap: 16,
                        }}
                    >
                        {realLots.map((lot) => {
                            const status = lot.approvalStatus?.toLowerCase();
                            const isApproved = status === "approved";
                            const isPending = status === "pending";
                            const statusBg = isApproved ? "#def7ec" : isPending ? "#fef3c7" : "#fee2e2";
                            const statusColor = isApproved ? "#0e9f6e" : isPending ? "#b45309" : "#dc2626";
                            const statusLabel = isApproved ? "Đang hoạt động" : isPending ? "Chờ duyệt" : "Bị từ chối";

                            return (
                                <div
                                    key={lot.id}
                                    style={{
                                        background: "#fff",
                                        borderRadius: 16,
                                        border: "1px solid #e2e8f0",
                                        padding: 20,
                                        boxShadow: "0 2px 6px rgba(15,27,61,0.04)",
                                    }}
                                >
                                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 8 }}>
                                        <h4 style={{ margin: 0, fontSize: 16, fontWeight: 800, color: "#0f172a" }}>
                                            {lot.name}
                                        </h4>
                                        <span
                                            style={{
                                                background: statusBg,
                                                color: statusColor,
                                                padding: "3px 10px",
                                                borderRadius: 999,
                                                fontSize: 11.5,
                                                fontWeight: 700,
                                            }}
                                        >
                                            {statusLabel}
                                        </span>
                                    </div>
                                    <p style={{ fontSize: 13, color: "#64748b", margin: "0 0 14px", lineHeight: 1.4 }}>
                                        {lot.address}
                                    </p>

                                    {lot.rejectionReason && (
                                        <div
                                            style={{
                                                background: "#fef2f2",
                                                border: "1px solid #fecaca",
                                                borderRadius: 8,
                                                padding: "8px 12px",
                                                fontSize: 12,
                                                color: "#991b1b",
                                                marginBottom: 12,
                                            }}
                                        >
                                            Lý do: {lot.rejectionReason}
                                        </div>
                                    )}

                                    <div
                                        style={{
                                            display: "flex",
                                            gap: 8,
                                            flexWrap: "wrap",
                                            borderTop: "1px solid #f1f5f9",
                                            paddingTop: 12,
                                        }}
                                    >
                                        <Link
                                            href={`/owner/layout/3d`}
                                            style={{
                                                fontSize: 12,
                                                fontWeight: 700,
                                                color: "#0284c7",
                                                textDecoration: "none",
                                                padding: "4px 8px",
                                                background: "#f0f9ff",
                                                borderRadius: 6,
                                            }}
                                        >
                                            Sơ đồ 2D/3D
                                        </Link>
                                        <Link
                                            href={`/owner/opening-hours`}
                                            style={{
                                                fontSize: 12,
                                                fontWeight: 700,
                                                color: "#475569",
                                                textDecoration: "none",
                                                padding: "4px 8px",
                                                background: "#f8fafc",
                                                borderRadius: 6,
                                            }}
                                        >
                                            Giờ hoạt động
                                        </Link>
                                        <Link
                                            href={`/owner/pricing`}
                                            style={{
                                                fontSize: 12,
                                                fontWeight: 700,
                                                color: "#475569",
                                                textDecoration: "none",
                                                padding: "4px 8px",
                                                background: "#f8fafc",
                                                borderRadius: 6,
                                            }}
                                        >
                                            Bảng giá & Cọc
                                        </Link>
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>
            )}
        </div>
    );
}
