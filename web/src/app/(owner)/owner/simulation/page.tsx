"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { OwnerPageHeader } from "@/components/layout/OwnerPageHeader";
import { BentoDashboardSamplePreview } from "@/features/owner/dashboard/BentoDashboardSamplePreview";
import { useOwnerStore } from "@/store/useOwnerStore";
import { getMyOwnerApplication } from "@/features/owner/api/ownerLegalApi";
import { setOwnerViewMode } from "@/features/owner/mode/useOwnerViewMode";
import "./simulation.css";

interface SimulationConfig {
    lotName: string;
    address: string;
    openTime: string;
    closeTime: string;
    is24h: boolean;
    carCapacity: number;
    motoCapacity: number;
    carHourlyRate: number;
    motoHourlyRate: number;
    carOvernightRate: number;
    motoOvernightRate: number;
    depositPercent: number;
}

interface SimSpot {
    id: string;
    name: string;
    type: "car" | "moto";
    status: "available" | "reserved" | "occupied";
    floor: string;
}

const DEFAULT_CONFIG_TEMPLATE: SimulationConfig = {
    lotName: "Bãi đỗ xe Thông minh ParkWise (Cơ sở Thử nghiệm)",
    address:
        "Số 123 Đường Điện Biên Phủ, Phường 15, Quận Bình Thạnh, TP. Hồ Chí Minh",
    openTime: "06:00",
    closeTime: "23:00",
    is24h: true,
    carCapacity: 120,
    motoCapacity: 350,
    carHourlyRate: 25000,
    motoHourlyRate: 5000,
    carOvernightRate: 80000,
    motoOvernightRate: 15000,
    depositPercent: 30,
};

const INITIAL_SIM_SPOTS: SimSpot[] = [
    {
        id: "A-01",
        name: "A-01",
        type: "car",
        status: "occupied",
        floor: "Tầng 1",
    },
    {
        id: "A-02",
        name: "A-02",
        type: "car",
        status: "available",
        floor: "Tầng 1",
    },
    {
        id: "A-03",
        name: "A-03",
        type: "car",
        status: "reserved",
        floor: "Tầng 1",
    },
    {
        id: "A-04",
        name: "A-04",
        type: "car",
        status: "available",
        floor: "Tầng 1",
    },
    {
        id: "A-05",
        name: "A-05",
        type: "car",
        status: "occupied",
        floor: "Tầng 1",
    },
    {
        id: "A-06",
        name: "A-06",
        type: "car",
        status: "available",
        floor: "Tầng 1",
    },
    {
        id: "B-01",
        name: "B-01",
        type: "car",
        status: "available",
        floor: "Tầng 1",
    },
    {
        id: "B-02",
        name: "B-02",
        type: "car",
        status: "occupied",
        floor: "Tầng 1",
    },
    {
        id: "B-03",
        name: "B-03",
        type: "car",
        status: "available",
        floor: "Tầng 1",
    },
    {
        id: "B-04",
        name: "B-04",
        type: "car",
        status: "reserved",
        floor: "Tầng 1",
    },
    {
        id: "M-01",
        name: "M-01",
        type: "moto",
        status: "occupied",
        floor: "Tầng 1",
    },
    {
        id: "M-02",
        name: "M-02",
        type: "moto",
        status: "occupied",
        floor: "Tầng 1",
    },
    {
        id: "M-03",
        name: "M-03",
        type: "moto",
        status: "available",
        floor: "Tầng 1",
    },
    {
        id: "M-04",
        name: "M-04",
        type: "moto",
        status: "available",
        floor: "Tầng 1",
    },
];

const STORAGE_KEY_SIM = "parkwise_owner_sandbox_data_v1";

export default function OwnerSimulationPage() {
    const simulateTick = useOwnerStore((s) => s.simulateTick);

    const [activeTab, setActiveTab] = useState<
        "overview" | "config" | "pricing" | "spots"
    >("overview");
    const [config, setConfig] = useState<SimulationConfig>(
        DEFAULT_CONFIG_TEMPLATE,
    );
    const [spots, setSpots] = useState<SimSpot[]>(INITIAL_SIM_SPOTS);
    const [showModal, setShowModal] = useState(false);
    const [toastMsg, setToastMsg] = useState<string | null>(null);
    const [ownerBusinessName, setOwnerBusinessName] = useState("");

    // Ticker for traffic demo in overview
    useEffect(() => {
        const timer = setInterval(() => {
            simulateTick();
        }, 6000);
        return () => clearInterval(timer);
    }, [simulateTick]);

    // Load clean initial state or isolated session state
    useEffect(() => {
        setOwnerViewMode("sample");
        async function initSandbox() {
            try {
                const app = await getMyOwnerApplication();
                if (app.businessName) {
                    setOwnerBusinessName(app.businessName);
                }

                const raw =
                    typeof window !== "undefined"
                        ? window.sessionStorage.getItem(STORAGE_KEY_SIM)
                        : null;
                if (raw) {
                    const parsed = JSON.parse(raw);
                    if (parsed.config) setConfig(parsed.config);
                    if (parsed.spots) setSpots(parsed.spots);
                } else {
                    // Fresh template seeded specifically for this owner
                    const fresh = {
                        ...DEFAULT_CONFIG_TEMPLATE,
                        lotName: app.businessName
                            ? `Bãi đỗ xe ${app.businessName}`
                            : DEFAULT_CONFIG_TEMPLATE.lotName,
                    };
                    setConfig(fresh);
                }
            } catch {
                // Fallback to fresh template
            }
        }
        initSandbox();
    }, []);

    function showToast(msg: string) {
        setToastMsg(msg);
        setTimeout(() => {
            setToastMsg((cur) => (cur === msg ? null : cur));
        }, 4000);
    }

    // Rollback function: Resets all modifications to fresh pristine state
    function handleRollback() {
        try {
            if (typeof window !== "undefined") {
                window.sessionStorage.removeItem(STORAGE_KEY_SIM);
            }
        } catch {}

        const fresh = {
            ...DEFAULT_CONFIG_TEMPLATE,
            lotName: ownerBusinessName
                ? `Bãi đỗ xe ${ownerBusinessName}`
                : DEFAULT_CONFIG_TEMPLATE.lotName,
        };
        setConfig(fresh);
        setSpots(INITIAL_SIM_SPOTS);
        showToast(
            "🔄 Đã rollback toàn bộ cấu hình về trạng thái nguyên bản sạch sẽ!",
        );
    }

    // Save in sandbox session
    function handleSaveSimulationConfig() {
        try {
            if (typeof window !== "undefined") {
                window.sessionStorage.setItem(
                    STORAGE_KEY_SIM,
                    JSON.stringify({
                        config,
                        spots,
                        savedAt: new Date().toISOString(),
                    }),
                );
            }
        } catch {}

        showToast(
            "✓ Đã lưu cấu hình vào phiên mô phỏng tạm thời! (Sẽ tự động rollback khi kết thúc)",
        );
    }

    // Cycle spot status interactively
    function toggleSpotStatus(spotId: string) {
        const nextStatus: Record<SimSpot["status"], SimSpot["status"]> = {
            available: "reserved",
            reserved: "occupied",
            occupied: "available",
        };

        setSpots((prev) =>
            prev.map((sp) =>
                sp.id === spotId
                    ? { ...sp, status: nextStatus[sp.status] }
                    : sp,
            ),
        );
    }

    const availableCount = spots.filter((s) => s.status === "available").length;
    const occupiedCount = spots.filter((s) => s.status === "occupied").length;
    const reservedCount = spots.filter((s) => s.status === "reserved").length;

    return (
        <div className="pw-sim-wrapper">
            {/* ── STICKY SIMULATION BANNER ── */}
            <aside
                className="pw-sim-banner"
                role="alert"
                aria-label="Thông báo chế độ mô phỏng"
            >
                <div className="pw-sim-banner-top">
                    <div className="pw-sim-badge">
                        <span className="pw-sim-badge-dot" />
                        <span>Màn hình mô phỏng (Sandbox Preview)</span>
                    </div>
                    <div className="pw-sim-banner-actions">
                        <button
                            type="button"
                            className="pw-sim-btn-rollback"
                            onClick={handleRollback}
                            title="Khôi phục toàn bộ dữ liệu về mặc định"
                        >
                            Rollback về mặc định
                        </button>
                        <button
                            type="button"
                            className="pw-sim-btn-info"
                            onClick={() => setShowModal(true)}
                        >
                            Vì sao ở chế độ mô phỏng?
                        </button>
                        <Link
                            href="/owner/dashboard"
                            onClick={() => setOwnerViewMode("live")}
                            className="pw-sim-btn-back"
                            style={{
                                background: "#0f172a",
                                color: "#eafe89",
                                fontWeight: 800,
                                textDecoration: "none",
                            }}
                        >
                            Về Dashboard Chính →
                        </Link>
                        <Link
                            href="/owner/onboarding"
                            className="pw-sim-btn-back"
                        >
                            Xem tiến độ duyệt hồ sơ →
                        </Link>
                    </div>
                </div>

                <h2 className="pw-sim-banner-title">
                    Môi trường trải nghiệm thử nghiệm dành cho Chủ bãi xe
                </h2>
                <p className="pw-sim-banner-desc">
                    Hồ sơ của cơ sở{" "}
                    <strong>{ownerBusinessName || "đối tác"}</strong> đang được
                    Ban quản trị ParkWise thẩm định. Bạn đang sử dụng phiên làm
                    việc mô phỏng độc lập với dữ liệu mẫu sạch. Mọi thao tác cấu
                    hình, chỉnh sửa tại đây chỉ nhằm mục đích làm quen hệ thống
                    và <strong>SẼ TỰ ĐỘNG ROLLBACK</strong> về trạng thái ban
                    đầu để không ảnh hưởng đến cơ sở dữ liệu thực tế và người
                    dùng khác.
                </p>
            </aside>

            {/* Header */}
            <OwnerPageHeader
                breadcrumb="/owner/simulation"
                title="Dashboard Mô phỏng Đối tác ParkWise"
                subtitle="Khám phá báo cáo doanh thu, lưu lượng xe và thử nghiệm cấu hình bãi xe"
            />

            {/* ── TABS NAVIGATION ── */}
            <div className="pw-sim-tabs">
                <button
                    type="button"
                    className={`pw-sim-tab-btn ${activeTab === "overview" ? "active" : ""}`}
                    onClick={() => setActiveTab("overview")}
                >
                    Màn hình Tổng quan (Giống trang chính)
                </button>
                <button
                    type="button"
                    className={`pw-sim-tab-btn ${activeTab === "config" ? "active" : ""}`}
                    onClick={() => setActiveTab("config")}
                >
                    Thử nghiệm Cấu hình Bãi xe
                </button>
                <button
                    type="button"
                    className={`pw-sim-tab-btn ${activeTab === "pricing" ? "active" : ""}`}
                    onClick={() => setActiveTab("pricing")}
                >
                    Thử nghiệm Bảng giá & Đặt cọc
                </button>
                <button
                    type="button"
                    className={`pw-sim-tab-btn ${activeTab === "spots" ? "active" : ""}`}
                    onClick={() => setActiveTab("spots")}
                >
                    Thao tác Ô đỗ tương tác ({availableCount} trống)
                </button>
            </div>

            {/* ── TAB 1: OVERVIEW DASHBOARD (BENTO GRID MOCKUP PREVIEW) ── */}
            {activeTab === "overview" && (
                <div className="pw-sim-overview-content" style={{ padding: 0 }}>
                    <BentoDashboardSamplePreview />
                </div>
            )}

            {/* ── TAB 2: PARKING LOT CONFIGURATION SIMULATOR ── */}
            {activeTab === "config" && (
                <div className="pw-sim-card">
                    <div className="pw-sim-card-header">
                        <div>
                            <h3 className="pw-sim-card-title">
                                Cấu hình thông tin bãi đỗ xe mô phỏng
                            </h3>
                            <p className="pw-sim-card-desc">
                                Thử nghiệm điều chỉnh thông số hoạt động của bãi
                                đỗ xe. Thao tác tại đây được lưu trong phiên
                                sandbox và sẽ rollback khi reset.
                            </p>
                        </div>
                        <button
                            type="button"
                            className="pw-btn-primary"
                            onClick={handleSaveSimulationConfig}
                        >
                            Lưu thử nghiệm cấu hình
                        </button>
                    </div>

                    <div
                        style={{
                            display: "grid",
                            gridTemplateColumns:
                                "repeat(auto-fit, minmax(280px, 1fr))",
                            gap: 18,
                        }}
                    >
                        <div>
                            <label
                                style={{
                                    display: "block",
                                    fontSize: 13,
                                    fontWeight: 700,
                                    color: "#1e293b",
                                    marginBottom: 6,
                                }}
                            >
                                Tên cơ sở bãi đỗ xe
                            </label>
                            <input
                                type="text"
                                className="pw-input"
                                value={config.lotName}
                                onChange={(e) =>
                                    setConfig({
                                        ...config,
                                        lotName: e.target.value,
                                    })
                                }
                            />
                        </div>

                        <div>
                            <label
                                style={{
                                    display: "block",
                                    fontSize: 13,
                                    fontWeight: 700,
                                    color: "#1e293b",
                                    marginBottom: 6,
                                }}
                            >
                                Địa chỉ mặt bằng khai thác
                            </label>
                            <input
                                type="text"
                                className="pw-input"
                                value={config.address}
                                onChange={(e) =>
                                    setConfig({
                                        ...config,
                                        address: e.target.value,
                                    })
                                }
                            />
                        </div>

                        <div>
                            <label
                                style={{
                                    display: "block",
                                    fontSize: 13,
                                    fontWeight: 700,
                                    color: "#1e293b",
                                    marginBottom: 6,
                                }}
                            >
                                Sức chứa Ô tô (Slots)
                            </label>
                            <input
                                type="number"
                                className="pw-input"
                                value={config.carCapacity}
                                onChange={(e) =>
                                    setConfig({
                                        ...config,
                                        carCapacity: Number(e.target.value),
                                    })
                                }
                            />
                        </div>

                        <div>
                            <label
                                style={{
                                    display: "block",
                                    fontSize: 13,
                                    fontWeight: 700,
                                    color: "#1e293b",
                                    marginBottom: 6,
                                }}
                            >
                                Sức chứa Xe máy (Slots)
                            </label>
                            <input
                                type="number"
                                className="pw-input"
                                value={config.motoCapacity}
                                onChange={(e) =>
                                    setConfig({
                                        ...config,
                                        motoCapacity: Number(e.target.value),
                                    })
                                }
                            />
                        </div>

                        <div>
                            <label
                                style={{
                                    display: "block",
                                    fontSize: 13,
                                    fontWeight: 700,
                                    color: "#1e293b",
                                    marginBottom: 6,
                                }}
                            >
                                Giờ mở cửa
                            </label>
                            <input
                                type="time"
                                className="pw-input"
                                value={config.openTime}
                                disabled={config.is24h}
                                onChange={(e) =>
                                    setConfig({
                                        ...config,
                                        openTime: e.target.value,
                                    })
                                }
                            />
                        </div>

                        <div>
                            <label
                                style={{
                                    display: "block",
                                    fontSize: 13,
                                    fontWeight: 700,
                                    color: "#1e293b",
                                    marginBottom: 6,
                                }}
                            >
                                Giờ đóng cửa
                            </label>
                            <input
                                type="time"
                                className="pw-input"
                                value={config.closeTime}
                                disabled={config.is24h}
                                onChange={(e) =>
                                    setConfig({
                                        ...config,
                                        closeTime: e.target.value,
                                    })
                                }
                            />
                        </div>
                    </div>

                    <div
                        style={{
                            marginTop: 18,
                            display: "flex",
                            alignItems: "center",
                            gap: 10,
                        }}
                    >
                        <input
                            id="sim-24h"
                            type="checkbox"
                            checked={config.is24h}
                            onChange={(e) =>
                                setConfig({
                                    ...config,
                                    is24h: e.target.checked,
                                })
                            }
                            style={{ width: 18, height: 18 }}
                        />
                        <label
                            htmlFor="sim-24h"
                            style={{
                                fontSize: 14,
                                fontWeight: 600,
                                color: "#0f172a",
                                cursor: "pointer",
                            }}
                        >
                            Bãi đỗ xe mở cửa liên tục 24/7 (Cả ngày & đêm)
                        </label>
                    </div>
                </div>
            )}

            {/* ── TAB 3: PRICING CONFIGURATION SIMULATOR ── */}
            {activeTab === "pricing" && (
                <div className="pw-sim-card">
                    <div className="pw-sim-card-header">
                        <div>
                            <h3 className="pw-sim-card-title">
                                Cấu hình Bảng giá gửi xe & Đặt cọc mô phỏng
                            </h3>
                            <p className="pw-sim-card-desc">
                                Cài đặt mức thu phí dịch vụ trông giữ xe ban
                                ngày, qua đêm và mức tiền cọc khi khách hàng đặt
                                chỗ trước qua ứng dụng.
                            </p>
                        </div>
                        <button
                            type="button"
                            className="pw-btn-primary"
                            onClick={handleSaveSimulationConfig}
                        >
                            Lưu thử nghiệm bảng giá
                        </button>
                    </div>

                    {/* Banner liên kết sang Trang Bảng giá mẫu đầy đủ */}
                    <div
                        style={{
                            background: "#F6F8F4",
                            border: "1px solid #E1E7DE",
                            borderRadius: 12,
                            padding: "14px 18px",
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
                                    fontWeight: 750,
                                    color: "#172018",
                                    fontSize: 13.5,
                                }}
                            >
                                Giao diện Bảng giá & Chính sách cọc 5 thẻ chuyên
                                nghiệp
                            </div>
                            <p
                                style={{
                                    margin: "2px 0 0",
                                    color: "#718071",
                                    fontSize: 12.5,
                                }}
                            >
                                Trải nghiệm trang Bảng giá & Đặt cọc mô phỏng
                                độc lập với đầy đủ phụ thu giờ cao điểm, qua đêm
                                và công cụ tính toán tự động.
                            </p>
                        </div>
                        <Link
                            href="/owner/pricing?mode=sample"
                            style={{
                                background: "#19301D",
                                color: "#FFFFFF",
                                padding: "8px 16px",
                                borderRadius: 8,
                                fontSize: 12.5,
                                fontWeight: 700,
                                textDecoration: "none",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 6,
                            }}
                        >
                            <span>Mở Trang Bảng giá mẫu đầy đủ →</span>
                        </Link>
                    </div>

                    <div
                        style={{
                            display: "grid",
                            gridTemplateColumns:
                                "repeat(auto-fit, minmax(280px, 1fr))",
                            gap: 18,
                        }}
                    >
                        <div
                            style={{
                                background: "#f8fafc",
                                padding: 16,
                                borderRadius: 12,
                                border: "1px solid #e2e8f0",
                            }}
                        >
                            <div
                                style={{
                                    fontWeight: 800,
                                    fontSize: 14,
                                    color: "#0f172a",
                                    marginBottom: 12,
                                }}
                            >
                                Biểu phí xe máy
                            </div>
                            <div style={{ marginBottom: 12 }}>
                                <label
                                    style={{
                                        display: "block",
                                        fontSize: 12,
                                        fontWeight: 600,
                                        color: "#64748b",
                                        marginBottom: 4,
                                    }}
                                >
                                    Giá vé lượt theo giờ (VND / lượt)
                                </label>
                                <input
                                    type="number"
                                    className="pw-input"
                                    value={config.motoHourlyRate}
                                    onChange={(e) =>
                                        setConfig({
                                            ...config,
                                            motoHourlyRate: Number(
                                                e.target.value,
                                            ),
                                        })
                                    }
                                />
                            </div>
                            <div>
                                <label
                                    style={{
                                        display: "block",
                                        fontSize: 12,
                                        fontWeight: 600,
                                        color: "#64748b",
                                        marginBottom: 4,
                                    }}
                                >
                                    Giá gửi qua đêm (VND / đêm)
                                </label>
                                <input
                                    type="number"
                                    className="pw-input"
                                    value={config.motoOvernightRate}
                                    onChange={(e) =>
                                        setConfig({
                                            ...config,
                                            motoOvernightRate: Number(
                                                e.target.value,
                                            ),
                                        })
                                    }
                                />
                            </div>
                        </div>

                        <div
                            style={{
                                background: "#f8fafc",
                                padding: 16,
                                borderRadius: 12,
                                border: "1px solid #e2e8f0",
                            }}
                        >
                            <div
                                style={{
                                    fontWeight: 800,
                                    fontSize: 14,
                                    color: "#0f172a",
                                    marginBottom: 12,
                                }}
                            >
                                Biểu phí ô tô
                            </div>
                            <div style={{ marginBottom: 12 }}>
                                <label
                                    style={{
                                        display: "block",
                                        fontSize: 12,
                                        fontWeight: 600,
                                        color: "#64748b",
                                        marginBottom: 4,
                                    }}
                                >
                                    Giá vé lượt theo giờ (VND / lượt)
                                </label>
                                <input
                                    type="number"
                                    className="pw-input"
                                    value={config.carHourlyRate}
                                    onChange={(e) =>
                                        setConfig({
                                            ...config,
                                            carHourlyRate: Number(
                                                e.target.value,
                                            ),
                                        })
                                    }
                                />
                            </div>
                            <div>
                                <label
                                    style={{
                                        display: "block",
                                        fontSize: 12,
                                        fontWeight: 600,
                                        color: "#64748b",
                                        marginBottom: 4,
                                    }}
                                >
                                    Giá gửi qua đêm (VND / đêm)
                                </label>
                                <input
                                    type="number"
                                    className="pw-input"
                                    value={config.carOvernightRate}
                                    onChange={(e) =>
                                        setConfig({
                                            ...config,
                                            carOvernightRate: Number(
                                                e.target.value,
                                            ),
                                        })
                                    }
                                />
                            </div>
                        </div>

                        <div
                            style={{
                                background: "#f8fafc",
                                padding: 16,
                                borderRadius: 12,
                                border: "1px solid #e2e8f0",
                            }}
                        >
                            <div
                                style={{
                                    fontWeight: 800,
                                    fontSize: 14,
                                    color: "#0f172a",
                                    marginBottom: 12,
                                }}
                            >
                                Chính sách đặt cọc Online
                            </div>
                            <div style={{ marginBottom: 12 }}>
                                <label
                                    style={{
                                        display: "block",
                                        fontSize: 12,
                                        fontWeight: 600,
                                        color: "#64748b",
                                        marginBottom: 4,
                                    }}
                                >
                                    Tỷ lệ tiền cọc (%)
                                </label>
                                <input
                                    type="number"
                                    className="pw-input"
                                    value={config.depositPercent}
                                    onChange={(e) =>
                                        setConfig({
                                            ...config,
                                            depositPercent: Number(
                                                e.target.value,
                                            ),
                                        })
                                    }
                                />
                                <div
                                    style={{
                                        fontSize: 11,
                                        color: "#64748b",
                                        marginTop: 4,
                                    }}
                                >
                                    Khách hàng thanh toán trước{" "}
                                    {config.depositPercent}% tổng cước ước tính
                                    để giữ chỗ đỗ.
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* ── TAB 4: INTERACTIVE SPOT SIMULATOR ── */}
            {activeTab === "spots" && (
                <div className="pw-sim-card">
                    <div className="pw-sim-card-header">
                        <div>
                            <h3 className="pw-sim-card-title">
                                Thử nghiệm tương tác ô đỗ xe thời gian thực
                            </h3>
                            <p className="pw-sim-card-desc">
                                Bấm trực tiếp vào từng ô đỗ bên dưới để chuyển
                                đổi trạng thái: <strong>Trống</strong> →{" "}
                                <strong>Đã đặt</strong> →{" "}
                                <strong>Đang đỗ</strong>.
                            </p>
                        </div>
                        <div
                            style={{
                                display: "flex",
                                gap: 10,
                                alignItems: "center",
                            }}
                        >
                            <span
                                style={{
                                    fontSize: 12,
                                    color: "#16a34a",
                                    fontWeight: 700,
                                }}
                            >
                                Trống: {availableCount}
                            </span>
                            <span
                                style={{
                                    fontSize: 12,
                                    color: "#ca8a04",
                                    fontWeight: 700,
                                }}
                            >
                                Đã đặt: {reservedCount}
                            </span>
                            <span
                                style={{
                                    fontSize: 12,
                                    color: "#dc2626",
                                    fontWeight: 700,
                                }}
                            >
                                Đang đỗ: {occupiedCount}
                            </span>
                        </div>
                    </div>

                    <div className="pw-sim-spot-grid">
                        {spots.map((sp) => (
                            <div
                                key={sp.id}
                                className={`pw-sim-spot-item ${sp.status}`}
                                onClick={() => toggleSpotStatus(sp.id)}
                                title="Bấm để đổi trạng thái ô đỗ"
                            >
                                <div className="pw-sim-spot-name">
                                    {sp.name}
                                </div>
                                <div className="pw-sim-spot-status">
                                    {sp.status === "available"
                                        ? "Trống"
                                        : sp.status === "reserved"
                                          ? "Đã đặt"
                                          : "Đang đỗ"}
                                </div>
                                <div className="pw-sim-spot-hint">
                                    {sp.type === "car" ? "Ô tô" : "Xe máy"} •
                                    Click đổi
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}

            {/* ── EXPLAINER MODAL ── */}
            {showModal && (
                <div
                    className="pw-sim-modal-backdrop"
                    onClick={() => setShowModal(false)}
                >
                    <div
                        className="pw-sim-modal-dialog"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <h3 className="pw-sim-modal-title">
                            <span>ℹ️</span> Cơ chế hoạt động của Màn hình mô
                            phỏng (Sandbox)
                        </h3>
                        <div className="pw-sim-modal-body">
                            <div className="pw-sim-modal-box">
                                <h4>
                                    1. Vì sao chưa thay đổi được vào hệ thống
                                    chính thức?
                                </h4>
                                <p>
                                    Hồ sơ pháp lý của cơ sở đang trong hàng đợi
                                    kiểm duyệt của Ban pháp chế ParkWise. Để bảo
                                    đảm an toàn dữ liệu và quyền lợi khách hàng,
                                    hệ thống chỉ cho phép xuất bản bãi đỗ xe ra
                                    ứng dụng khi hồ sơ đã được duyệt chính thức.
                                </p>
                            </div>

                            <div className="pw-sim-modal-box">
                                <h4>
                                    2. Tính cô lập & Không bị ảnh hưởng bởi
                                    người khác
                                </h4>
                                <p>
                                    Mỗi tài khoản mới đăng ký khi truy cập vào
                                    đây đều nhận được một bản sao mô phỏng sạch
                                    sẽ và độc lập. Bạn sẽ không bị ảnh hưởng bởi
                                    bất kỳ thay đổi nào của người đăng ký trước
                                    hoặc người dùng khác.
                                </p>
                            </div>

                            <div className="pw-sim-modal-box">
                                <h4>3. Cơ chế Tự động Rollback</h4>
                                <p>
                                    Toàn bộ các thao tác thử nghiệm cài đặt giá
                                    vé, sơ đồ hoặc sức chứa tại đây chỉ lưu tạm
                                    thời trong phiên làm việc của bạn. Khi kết
                                    thúc phiên hoặc bấm nút{" "}
                                    <strong>Rollback về mặc định</strong>, dữ
                                    liệu sẽ được khôi phục về trạng thái xuất
                                    phát điểm.
                                </p>
                            </div>
                        </div>
                        <div className="pw-sim-modal-footer">
                            <button
                                type="button"
                                className="pw-sim-modal-close-btn"
                                onClick={() => setShowModal(false)}
                            >
                                Đã hiểu & Đóng
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* ── TOAST NOTIFICATION ── */}
            {toastMsg && (
                <div className="pw-sim-toast" role="status">
                    <span>🔔</span>
                    <span>{toastMsg}</span>
                </div>
            )}
        </div>
    );
}
