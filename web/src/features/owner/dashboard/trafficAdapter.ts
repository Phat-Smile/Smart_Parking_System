/**
 * trafficAdapter – Cung cấp số liệu xe vào / ra và lượt đỗ theo Ngày / Tuần / Tháng.
 */

export interface TrafficPoint {
    label: string;
    inCount: number;
    outCount: number;
    isPeak?: boolean;
}

export interface TrafficData {
    period: "day" | "week" | "month";
    points: TrafficPoint[];
    totalIn: number;
    totalOut: number;
    dateRangeText: string;
    peakDayLabel: string;
    maxInLabel: string;
    maxOutLabel: string;
}

export interface WeeklyParkingItem {
    day: string;
    reserved: number;
    walkIn: number;
    total: number;
    isPeak?: boolean;
}

export function getTrafficData(period: "day" | "week" | "month"): TrafficData {
    if (period === "day") {
        // 24 giờ: 00h đến 23h
        const points: TrafficPoint[] = Array.from({ length: 24 }, (_, h) => ({
            label: `${String(h).padStart(2, "0")}h`,
            inCount: 0,
            outCount: 0,
            isPeak: false,
        }));

        return {
            period: "day",
            points,
            totalIn: 0,
            totalOut: 0,
            dateRangeText: "Hôm nay · 24 giờ",
            peakDayLabel: "—",
            maxInLabel: "Chưa có dữ liệu",
            maxOutLabel: "Chưa có dữ liệu",
        };
    }

    if (period === "month") {
        // 12 tháng: T1 đến T12
        const points: TrafficPoint[] = Array.from({ length: 12 }, (_, i) => ({
            label: `T${i + 1}`,
            inCount: 0,
            outCount: 0,
            isPeak: false,
        }));

        return {
            period: "month",
            points,
            totalIn: 0,
            totalOut: 0,
            dateRangeText: `Năm ${new Date().getFullYear()}`,
            peakDayLabel: "—",
            maxInLabel: "Chưa có dữ liệu",
            maxOutLabel: "Chưa có dữ liệu",
        };
    }

    // Mặc định: Tuần (T2 đến CN)
    const points: TrafficPoint[] = [
        { label: "T2", inCount: 0, outCount: 0 },
        { label: "T3", inCount: 0, outCount: 0 },
        { label: "T4", inCount: 0, outCount: 0 },
        { label: "T5", inCount: 0, outCount: 0 },
        { label: "T6", inCount: 0, outCount: 0 },
        { label: "T7", inCount: 0, outCount: 0 },
        { label: "CN", inCount: 0, outCount: 0 },
    ];

    return {
        period: "week",
        points,
        totalIn: 0,
        totalOut: 0,
        dateRangeText: "Tuần hiện tại",
        peakDayLabel: "—",
        maxInLabel: "Chưa có dữ liệu",
        maxOutLabel: "Chưa có dữ liệu",
    };
}

export function getWeeklyParkingData(): WeeklyParkingItem[] {
    return [
        { day: "T2", reserved: 0, walkIn: 0, total: 0 },
        { day: "T3", reserved: 0, walkIn: 0, total: 0 },
        { day: "T4", reserved: 0, walkIn: 0, total: 0 },
        { day: "T5", reserved: 0, walkIn: 0, total: 0 },
        { day: "T6", reserved: 0, walkIn: 0, total: 0 },
        { day: "T7", reserved: 0, walkIn: 0, total: 0 },
        { day: "CN", reserved: 0, walkIn: 0, total: 0 },
    ];
}
