import { getGatewayBaseUrl } from "@/shared/config/env";
import {
    readPublic,
    countLot,
    emptyOccupancy,
    type PublicLayout,
    type PublicUtilization,
    type OccupancyCounts,
} from "./ownerOccupancy";

export interface ParkingLotStatistics {
    lotId: string;
    totalRevenue: number;
    netRevenue: number;
    todaySessionsCount: number;
    activeReservationsCount: number;
    occupancyRatePercent?: number;
    hourlyTraffic?: Array<{
        time: string;
        inCount: number;
        outCount: number;
    }>;
    recentSessions?: Array<{
        sessionId: string;
        plateNumber: string;
        vehicleType: string;
        slotCode?: string | null;
        entryTime: string;
        durationMinutes: number;
        interimFee: number;
        status: string;
        paymentMethod?: string;
    }>;
}

export interface AggregatedOwnerDashboardStats {
    totalRevenue: number;
    netRevenue: number;
    todaySessionsCount: number;
    activeReservationsCount: number;
    occupancy: OccupancyCounts;
    occupancyRatePercent: number;
    hasRealActivity: boolean;
    recentSessions: Array<{
        sessionId: string;
        plateNumber: string;
        vehicleType: string;
        slotCode?: string | null;
        entryTime: string;
        durationMinutes: number;
        interimFee: number;
        status: string;
        paymentMethod?: string;
    }>;
    hourlyTraffic: Array<{
        time: string;
        inCount: number;
        outCount: number;
    }>;
}

/**
 * Tra cứu thống kê thời gian thực của bãi xe theo GUID (GET /api/parking/lots/{id}/statistics).
 * Trả về null nếu endpoint chưa được cấu hình hoặc bãi chưa phát sinh phiên đỗ.
 */
export async function getLotStatisticsApi(
    lotId: string,
    signal?: AbortSignal,
): Promise<ParkingLotStatistics | null> {
    try {
        const response = await fetch(
            `${getGatewayBaseUrl()}/api/parking/lots/${encodeURIComponent(lotId)}/statistics`,
            { cache: "no-store", signal },
        );
        if (!response.ok) return null;
        return (await response.json()) as ParkingLotStatistics;
    } catch {
        return null;
    }
}

/**
 * Tổng hợp dữ liệu vận hành từ tất cả các bãi đỗ xe đã được phê duyệt của chủ bãi.
 * Kết hợp số liệu từ API statistics và zone-utilization/layout thực tế.
 */
export async function loadAggregatedOwnerStats(
    lotIds: string[],
    signal?: AbortSignal,
): Promise<AggregatedOwnerDashboardStats> {
    const combinedOccupancy = emptyOccupancy();
    let totalRevenue = 0;
    let netRevenue = 0;
    let todaySessionsCount = 0;
    let activeReservationsCount = 0;
    const allRecentSessions: AggregatedOwnerDashboardStats["recentSessions"] =
        [];
    const hourlyTrafficMap = new Map<
        string,
        { inCount: number; outCount: number }
    >();

    await Promise.all(
        lotIds.map(async (id) => {
            // 1. Fetch layout & zone utilization to get exact real spaces
            try {
                const [layout, usage] = await Promise.all([
                    readPublic<PublicLayout>(id, "layout", signal),
                    readPublic<PublicUtilization>(
                        id,
                        "zone-utilization",
                        signal,
                    ),
                ]);
                const floors = countLot(layout, usage);
                for (const fl of floors) {
                    combinedOccupancy.total += fl.total;
                    combinedOccupancy.available += fl.available;
                    combinedOccupancy.reserved += fl.reserved;
                    combinedOccupancy.occupied += fl.occupied;
                    combinedOccupancy.maintenance += fl.maintenance;
                }
            } catch {
                // Ignore single lot layout failure
            }

            // 2. Fetch statistics endpoint
            const stats = await getLotStatisticsApi(id, signal);
            if (stats) {
                totalRevenue += stats.totalRevenue || 0;
                netRevenue += stats.netRevenue || 0;
                todaySessionsCount += stats.todaySessionsCount || 0;
                activeReservationsCount += stats.activeReservationsCount || 0;

                if (stats.recentSessions?.length) {
                    allRecentSessions.push(...stats.recentSessions);
                }

                if (stats.hourlyTraffic?.length) {
                    for (const pt of stats.hourlyTraffic) {
                        const existing = hourlyTrafficMap.get(pt.time) || {
                            inCount: 0,
                            outCount: 0,
                        };
                        existing.inCount += pt.inCount;
                        existing.outCount += pt.outCount;
                        hourlyTrafficMap.set(pt.time, existing);
                    }
                }
            }
        }),
    );

    // If reservation count from stats is 0, fallback to reserved spaces from occupancy
    if (activeReservationsCount === 0 && combinedOccupancy.reserved > 0) {
        activeReservationsCount = combinedOccupancy.reserved;
    }

    const occupiedOrReserved =
        combinedOccupancy.occupied + combinedOccupancy.reserved;
    const occupancyRatePercent =
        combinedOccupancy.total > 0
            ? Math.round((occupiedOrReserved / combinedOccupancy.total) * 100)
            : 0;

    const hasRealActivity =
        totalRevenue > 0 ||
        todaySessionsCount > 0 ||
        allRecentSessions.length > 0;

    const hourlyTraffic = Array.from(hourlyTrafficMap.entries()).map(
        ([time, counts]) => ({
            time,
            inCount: counts.inCount,
            outCount: counts.outCount,
        }),
    );

    return {
        totalRevenue,
        netRevenue,
        todaySessionsCount,
        activeReservationsCount,
        occupancy: combinedOccupancy,
        occupancyRatePercent,
        hasRealActivity,
        recentSessions: allRecentSessions.slice(0, 10),
        hourlyTraffic,
    };
}
