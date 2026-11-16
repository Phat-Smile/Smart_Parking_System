"use client";

import React, { useEffect, useMemo, useState } from "react";
import { OwnerPageHeader } from "@/components/layout/OwnerPageHeader";
import { ExcelExportButton } from "@/components/common/ExcelExportButton";
import { useOwnerStore } from "@/store/useOwnerStore";
import { getRevenueReportData } from "@/features/owner/reports/revenueAdapter";
import { ReportPeriodPicker } from "@/features/owner/reports/ReportPeriodPicker";
import { RevenueKpiCards } from "@/features/owner/reports/RevenueKpiCards";
import { RevenueBarChart } from "@/features/owner/reports/RevenueBarChart";
import { FloorRevenueBars } from "@/features/owner/reports/FloorRevenueBars";
import { PaymentPieChart } from "@/features/owner/reports/PaymentPieChart";
import { PeakTimeList } from "@/features/owner/reports/PeakTimeList";
import { ReportDetailTable } from "@/features/owner/reports/ReportDetailTable";
import { DashboardEmptyState } from "@/features/owner/dashboard/DashboardEmptyState";
import {
    getOwnerParkingLotsApi,
    type OwnerParkingLotItem,
} from "@/features/owner/api/ownerApi";
import {
    loadAggregatedOwnerStats,
    getLotStatisticsApi,
    type AggregatedOwnerDashboardStats,
    type ParkingLotStatistics,
} from "@/features/owner/dashboard/ownerStatisticsApi";

export default function ReportsPage() {
    const reportFilter = useOwnerStore((s) => s.reportFilter);
    const setReportFilter = useOwnerStore((s) => s.setReportFilter);

    const mode = reportFilter.mode || "day";
    const dateStr =
        reportFilter.date || new Date().toISOString().slice(0, 10);

    const [lots, setLots] = useState<OwnerParkingLotItem[]>([]);
    const [selectedLotId, setSelectedLotId] = useState<string>("all");
    const [stats, setStats] = useState<
        AggregatedOwnerDashboardStats | ParkingLotStatistics | null
    >(null);
    const [loading, setLoading] = useState(true);

    // Fetch real lots & statistics on mount or when selected lot changes
    useEffect(() => {
        let active = true;

        async function fetchReportStats() {
            setLoading(true);
            try {
                const lotsRes = await getOwnerParkingLotsApi({
                    page: 1,
                    pageSize: 100,
                }).catch(() => ({
                    items: [],
                    totalCount: 0,
                    page: 1,
                    pageSize: 100,
                }));

                if (!active) return;
                const items = lotsRes?.items || [];
                setLots(items);

                const approved = items.filter(
                    (l) => l.approvalStatus?.toLowerCase() === "approved",
                );

                if (approved.length === 0) {
                    setStats(null);
                    setLoading(false);
                    return;
                }

                if (selectedLotId === "all") {
                    const approvedIds = approved.map((l) => l.id);
                    const aggregated = await loadAggregatedOwnerStats(
                        approvedIds,
                    ).catch(() => null);
                    if (active) setStats(aggregated);
                } else {
                    const singleStat = await getLotStatisticsApi(
                        selectedLotId,
                    ).catch(() => null);
                    if (active) setStats(singleStat);
                }
            } catch {
                if (active) setStats(null);
            } finally {
                if (active) setLoading(false);
            }
        }

        void fetchReportStats();
        return () => {
            active = false;
        };
    }, [selectedLotId]);

    const approvedLots = lots.filter(
        (l) => l.approvalStatus?.toLowerCase() === "approved",
    );

    const currentLotName =
        selectedLotId === "all"
            ? "Tất cả cơ sở bãi xe"
            : lots.find((l) => l.id === selectedLotId)?.name ||
              "Bãi xe đã chọn";

    const reportData = useMemo(
        () =>
            getRevenueReportData(mode, dateStr, {
                stats,
                lotName: currentLotName,
            }),
        [mode, dateStr, stats, currentLotName],
    );

    return (
        <div style={{ maxWidth: 1400, margin: "0 auto", paddingBottom: 48 }}>
            <OwnerPageHeader
                breadcrumb="/owner/reports"
                title="Báo cáo doanh thu"
                subtitle="Chi tiết nguồn thu, phương thức thanh toán và lưu lượng vận hành thực tế"
                actions={
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 12,
                            flexWrap: "wrap",
                        }}
                    >
                        {/* Bộ chọn cơ sở bãi xe */}
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 6,
                            }}
                        >
                            <label
                                htmlFor="lot-select"
                                style={{
                                    fontSize: 12.5,
                                    fontWeight: 700,
                                    color: "#475569",
                                }}
                            >
                                Cơ sở:
                            </label>
                            <select
                                id="lot-select"
                                value={selectedLotId}
                                onChange={(e) =>
                                    setSelectedLotId(e.target.value)
                                }
                                aria-label="Chọn cơ sở bãi xe"
                                style={{
                                    background: "#fff",
                                    border: "1px solid #cbd5e1",
                                    borderRadius: 10,
                                    padding: "7px 12px",
                                    fontSize: 13,
                                    fontWeight: 600,
                                    color: "#0f172a",
                                    outline: "none",
                                    cursor: "pointer",
                                    minWidth: 200,
                                }}
                            >
                                <option value="all">
                                    Tất cả cơ sở ({approvedLots.length} bãi hoạt
                                    động)
                                </option>
                                {approvedLots.map((l) => (
                                    <option key={l.id} value={l.id}>
                                        {l.name}
                                    </option>
                                ))}
                            </select>
                        </div>

                        <ExcelExportButton
                            data={reportData.excelData}
                            filename={`bao-cao-doanh-thu-${mode}-${dateStr}.xlsx`}
                            title="Xuất Excel / CSV"
                        />
                    </div>
                }
            />

            {/* Bộ lọc kỳ báo cáo */}
            <div style={{ marginBottom: 20 }}>
                <ReportPeriodPicker
                    mode={mode}
                    dateStr={dateStr}
                    onModeChange={(newMode) =>
                        setReportFilter({ mode: newMode })
                    }
                    onDateChange={(newDate) =>
                        setReportFilter({ date: newDate })
                    }
                />
            </div>

            {loading ? (
                <div
                    style={{
                        padding: "60px 20px",
                        textAlign: "center",
                        background: "#fff",
                        borderRadius: 18,
                        border: "1px solid #e2e8f0",
                        marginBottom: 24,
                    }}
                >
                    <div
                        style={{
                            width: 32,
                            height: 32,
                            border: "3px solid #e2e8f0",
                            borderTopColor: "#19301d",
                            borderRadius: "50%",
                            margin: "0 auto 12px",
                            animation: "spin 0.8s linear infinite",
                        }}
                    />
                    <div
                        style={{
                            fontSize: 14.5,
                            fontWeight: 700,
                            color: "#0f172a",
                        }}
                    >
                        Đang đối soát số liệu doanh thu thực tế...
                    </div>
                    <style jsx>{`
                        @keyframes spin {
                            to {
                                transform: rotate(360deg);
                            }
                        }
                    `}</style>
                </div>
            ) : (
                <>
                    {/* 4 Thẻ KPI doanh thu */}
                    <RevenueKpiCards kpis={reportData.kpis} />

                    {/* Trạng thái rỗng khi bãi xe chưa phát sinh giao dịch thật */}
                    {!reportData.hasRealActivity && (
                        <div style={{ marginBottom: 24 }}>
                            <DashboardEmptyState
                                title="Chưa phát sinh giao dịch doanh thu"
                                description="Hệ thống bốt kiểm soát & Camera OCR sẽ tự động hiển thị phiên gửi xe và cập nhật doanh thu thời gian thực ngay khi có phương tiện check-in và thanh toán."
                                actionHref="/owner/parking-lots"
                                actionLabel="Quản lý cơ sở bãi xe →"
                            />
                        </div>
                    )}

                    {/* Biểu đồ cột doanh thu */}
                    <div style={{ marginBottom: 20 }}>
                        <RevenueBarChart
                            items={reportData.barItems}
                            title={`Biểu đồ doanh thu (${mode === "day" ? "24 giờ" : mode === "month" ? "Các ngày trong tháng" : "12 tháng trong năm"})`}
                        />
                    </div>

                    {/* Hàng 3 cột phân tích: Theo tầng (1/3) + Phương thức thanh toán (1/3) + Giờ cao điểm (1/3) */}
                    <div
                        style={{
                            display: "grid",
                            gridTemplateColumns:
                                "repeat(auto-fit, minmax(320px, 1fr))",
                            gap: 16,
                            marginBottom: 20,
                        }}
                    >
                        <FloorRevenueBars items={reportData.floorItems} />
                        <PaymentPieChart items={reportData.paymentMethods} />
                        <PeakTimeList items={reportData.peakTimes} />
                    </div>

                    {/* Bảng chi tiết báo cáo doanh thu */}
                    <ReportDetailTable rows={reportData.tableRows} />
                </>
            )}
        </div>
    );
}
