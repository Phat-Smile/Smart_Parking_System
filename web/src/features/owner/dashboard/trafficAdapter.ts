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
  period: 'day' | 'week' | 'month';
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

export function getTrafficData(period: 'day' | 'week' | 'month'): TrafficData {
  if (period === 'day') {
    // 24 giờ: 00h đến 23h
    const points: TrafficPoint[] = Array.from({ length: 24 }, (_, h) => {
      const isPeakHour = (h >= 8 && h <= 10) || (h >= 17 && h <= 19);
      const baseIn = isPeakHour ? 85 + (h % 3) * 15 : 20 + (h % 5) * 8;
      const baseOut = isPeakHour ? 75 + ((h + 1) % 3) * 14 : 18 + ((h + 2) % 4) * 7;
      return {
        label: `${String(h).padStart(2, '0')}h`,
        inCount: baseIn,
        outCount: baseOut,
        isPeak: h === 18,
      };
    });

    const totalIn = points.reduce((s, p) => s + p.inCount, 0);
    const totalOut = points.reduce((s, p) => s + p.outCount, 0);

    return {
      period: 'day',
      points,
      totalIn,
      totalOut,
      dateRangeText: 'Hôm nay · 24 giờ',
      peakDayLabel: '18h (Cao điểm)',
      maxInLabel: '18h (115 lượt)',
      maxOutLabel: '17h (103 lượt)',
    };
  }

  if (period === 'month') {
    // 12 tháng: T1 đến T12
    const points: TrafficPoint[] = [
      { label: 'T1', inCount: 28400, outCount: 27900 },
      { label: 'T2', inCount: 26200, outCount: 25800 },
      { label: 'T3', inCount: 29800, outCount: 29100 },
      { label: 'T4', inCount: 31200, outCount: 30800 },
      { label: 'T5', inCount: 32500, outCount: 31900 },
      { label: 'T6', inCount: 30100, outCount: 29700 },
      { label: 'T7', inCount: 33400, outCount: 32800 },
      { label: 'T8', inCount: 34100, outCount: 33600 },
      { label: 'T9', inCount: 33800, outCount: 33200 },
      { label: 'T10', inCount: 35900, outCount: 35100, isPeak: true },
      { label: 'T11', inCount: 32000, outCount: 31400 },
      { label: 'T12', inCount: 36500, outCount: 35800 },
    ];

    const totalIn = points.reduce((s, p) => s + p.inCount, 0);
    const totalOut = points.reduce((s, p) => s + p.outCount, 0);

    return {
      period: 'month',
      points,
      totalIn,
      totalOut,
      dateRangeText: 'Năm 2026',
      peakDayLabel: 'T12',
      maxInLabel: 'T12 (36.500)',
      maxOutLabel: 'T12 (35.800)',
    };
  }

  // Mặc định: Tuần (T2 đến CN)
  const points: TrafficPoint[] = [
    { label: 'T2', inCount: 820, outCount: 780 },
    { label: 'T3', inCount: 911, outCount: 880 },
    { label: 'T4', inCount: 943, outCount: 915 },
    { label: 'T5', inCount: 1035, outCount: 990 },
    { label: 'T6', inCount: 1103, outCount: 1060 },
    { label: 'T7', inCount: 1187, outCount: 1114, isPeak: true },
    { label: 'CN', inCount: 1304, outCount: 1284 },
  ];

  const totalIn = points.reduce((s, p) => s + p.inCount, 0);
  const totalOut = points.reduce((s, p) => s + p.outCount, 0);

  return {
    period: 'week',
    points,
    totalIn,
    totalOut,
    dateRangeText: '12.10.2026 – 18.10.2026',
    peakDayLabel: 'T7',
    maxInLabel: 'T7 (1.187)',
    maxOutLabel: 'T7 (1.114)',
  };
}

export function getWeeklyParkingData(): WeeklyParkingItem[] {
  return [
    { day: 'T2', reserved: 450, walkIn: 461, total: 911 },
    { day: 'T3', reserved: 470, walkIn: 473, total: 943 },
    { day: 'T4', reserved: 510, walkIn: 616, total: 1126 },
    { day: 'T5', reserved: 490, walkIn: 545, total: 1035 },
    { day: 'T6', reserved: 520, walkIn: 583, total: 1103 },
    { day: 'T7', reserved: 560, walkIn: 691, total: 1251, isPeak: true },
    { day: 'CN', reserved: 540, walkIn: 584, total: 1124 },
  ];
}
