import { describe, expect, it } from "vitest";
import {
    dim,
    getRevenueReportData,
    p2,
    short,
    vnd,
} from "./revenueAdapter";
import { type AggregatedOwnerDashboardStats } from "@/features/owner/dashboard/ownerStatisticsApi";

describe("revenueAdapter utility functions", () => {
    it("p2 pads numbers with leading zero", () => {
        expect(p2(5)).toBe("05");
        expect(p2(12)).toBe("12");
    });

    it("dim returns correct days in month", () => {
        expect(dim(2026, 2)).toBe(28); // 2026 is not a leap year
        expect(dim(2024, 2)).toBe(29); // 2024 is leap year
        expect(dim(2026, 1)).toBe(31);
        expect(dim(2026, 4)).toBe(30);
    });

    it("vnd formats currency with Vietnamese locale and đ suffix", () => {
        const result = vnd(50000);
        expect(result).toContain("50.000");
        expect(result).toContain("đ");
    });

    it("short formats currency into compact readable strings", () => {
        expect(short(0)).toBe("0 đ");
        expect(short(-100)).toBe("0 đ");
        expect(short(500000)).toContain("500.000");
        expect(short(2500000)).toContain("2,5 tr");
        expect(short(3200000000)).toContain("3,20 tỷ");
    });
});

describe("getRevenueReportData - Empty State", () => {
    it("returns zeroed KPIs and empty state when stats is null or has no activity", () => {
        const report = getRevenueReportData("day", "2026-11-17", {
            stats: null,
            lotName: "Bãi đỗ xe mẫu",
        });

        expect(report.hasRealActivity).toBe(false);
        expect(report.totalRev).toBe(0);
        expect(report.kpis.totalRevenue).toBe(0);
        expect(report.kpis.totalRevenueText).toBe("0 đ");
        expect(report.kpis.totalTransactions).toBe(0);
        expect(report.kpis.onlineRatePct).toBe(0);
        expect(report.barItems.length).toBe(24);
        expect(report.barItems.every((item) => item.revenue === 0)).toBe(true);
        expect(report.excelData.length).toBeGreaterThan(5);
        expect(report.excelData[1][1]).toBe("Bãi đỗ xe mẫu");
    });

    it("generates correct number of monthly bars in empty state", () => {
        const report = getRevenueReportData("month", "2026-11-17", {
            stats: null,
        });
        expect(report.hasRealActivity).toBe(false);
        expect(report.barItems.length).toBe(30); // November has 30 days
    });

    it("generates 12 monthly bars in yearly empty state", () => {
        const report = getRevenueReportData("year", "2026-11-17", {
            stats: null,
        });
        expect(report.hasRealActivity).toBe(false);
        expect(report.barItems.length).toBe(12);
        expect(report.barItems[0].label).toBe("T1");
        expect(report.barItems[11].label).toBe("T12");
    });
});

describe("getRevenueReportData - Real Statistics", () => {
    const mockRealStats: AggregatedOwnerDashboardStats = {
        totalRevenue: 12500000,
        netRevenue: 11250000,
        todaySessionsCount: 30,
        activeReservationsCount: 5,
        occupancy: {
            total: 100,
            available: 55,
            reserved: 5,
            occupied: 40,
            maintenance: 0,
        },
        occupancyRatePercent: 45,
        hasRealActivity: true,
        recentSessions: [
            {
                sessionId: "sess-1",
                plateNumber: "51K-123.45",
                vehicleType: "Car",
                entryTime: "2026-11-17T08:00:00Z",
                status: "Completed",
                interimFee: 100000,
                durationMinutes: 60,
                paymentMethod: "VNPay",
                slotCode: "A-01",
            },
            {
                sessionId: "sess-2",
                plateNumber: "59A-999.88",
                vehicleType: "Motorbike",
                entryTime: "2026-11-17T09:30:00Z",
                status: "Completed",
                interimFee: 20000,
                durationMinutes: 45,
                paymentMethod: "Cash",
                slotCode: "M-02",
            },
            {
                sessionId: "sess-3",
                plateNumber: "30F-888.99",
                vehicleType: "Car",
                entryTime: "2026-11-17T11:15:00Z",
                status: "Completed",
                interimFee: 80000,
                durationMinutes: 90,
                paymentMethod: "VNPay",
                slotCode: "B-05",
            },
        ],
        hourlyTraffic: [
            { time: "08:00", inCount: 3, outCount: 2 },
            { time: "09:00", inCount: 7, outCount: 5 },
            { time: "11:00", inCount: 4, outCount: 4 },
        ],
    };

    it("correctly aggregates revenue, transactions, and online rate from real data", () => {
        const report = getRevenueReportData("day", "2026-11-17", {
            stats: mockRealStats,
            lotName: "Bãi xe Quận 1",
        });

        expect(report.hasRealActivity).toBe(true);
        expect(report.totalRev).toBe(12500000);
        expect(report.kpis.totalRevenue).toBe(12500000);
        expect(report.kpis.totalTransactions).toBe(30);
        // 2 out of 3 sessions are VNPay (non-cash) => ~67% online
        expect(report.kpis.onlineRatePct).toBe(67);
        expect(report.tableRows.length).toBe(3);
        expect(report.tableRows[0].plateNumber).toBe("51K-123.45");
        expect(report.tableRows[0].revenue).toBe(100000);
    });

    it("formats excel data with header metadata and structured transaction rows", () => {
        const report = getRevenueReportData("day", "2026-11-17", {
            stats: mockRealStats,
            lotName: "Bãi xe Quận 1",
            exportTimestamp: "17/11/2026 07:30:00",
        });

        const excel = report.excelData;
        expect(excel[0][0]).toContain("BÁO CÁO DOANH THU");
        expect(excel[1][1]).toBe("Bãi xe Quận 1");
        expect(excel[2][1]).toBe("17/11/2026 07:30:00");
        expect(excel[3][1]).toContain("17/11/2026");

        // Header row for data
        const headerIndex = excel.findIndex((row) => row[0] === "STT");
        expect(headerIndex).toBeGreaterThan(0);
        expect(excel[headerIndex][2]).toBe("Biển số xe");
        expect(excel[headerIndex][6]).toBe("Doanh thu (VNĐ)");

        // First row of data
        const firstDataRow = excel[headerIndex + 1];
        expect(firstDataRow[0]).toBe(1);
        expect(firstDataRow[2]).toBe("51K-123.45");
        expect(firstDataRow[6]).toBe(100000);
    });
});
