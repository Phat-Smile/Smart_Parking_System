"use client";

import React, { useMemo } from "react";
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

export default function ReportsPage() {
    const reportFilter = useOwnerStore((s) => s.reportFilter);
    const setReportFilter = useOwnerStore((s) => s.setReportFilter);

    const mode = reportFilter.mode || "day";
    const dateStr = reportFilter.date || "2025-10-20";

    const reportData = useMemo(
        () => getRevenueReportData(mode, dateStr),
        [mode, dateStr],
    );

    return (
        <div style={{ maxWidth: 1400, margin: "0 auto", paddingBottom: 48 }}>
            <OwnerPageHeader
                breadcrumb="/owner/reports"
                title="Báo cáo doanh thu"
                subtitle="Chi tiết nguồn thu, phương thức thanh toán và lưu lượng vận hành"
                actions={
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 12,
                        }}
                    >
                        <ExcelExportButton data={reportData.excelData} />
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

            {/* 4 Thẻ KPI doanh thu */}
            <RevenueKpiCards kpis={reportData.kpis} />

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
                    gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))",
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
        </div>
    );
}
