/**
 * trafficAdapter – Cung cấp số liệu xe vào / ra và lượt đỗ theo Ngày / Tuần / Tháng.
 */

export interface TrafficPoint {
  label: string;
  inCount: number;
  outCount: number;
  inLotCount?: number;
  isPeak?: boolean;
}

export interface TrafficData {
  period: 'day' | 'week' | 'month';
  points: TrafficPoint[];
  totalIn: number;
  totalOut: number;
  dateRangeText: string;
  peakPeriodTitle: string; // "Giờ cao điểm", "Ngày cao điểm", "Tháng cao điểm"
  peakLabel: string;
  peakDetails: string;    // "Lúc đó: vào X · ra Y"
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
  let points: TrafficPoint[] = [];
  let dateRangeText = '';
  let peakPeriodTitle = '';

  if (period === 'day') {
    dateRangeText = 'Hôm nay · 24 giờ';
    peakPeriodTitle = 'Giờ cao điểm';
    // 24 giờ: 00h đến 23h
    points = Array.from({ length: 24 }, (_, h) => {
      const isPeakHour = h === 18;
      const isRush = (h >= 7 && h <= 9) || (h >= 17 && h <= 19);
      const inCount = isPeakHour ? 118 : isRush ? 85 + (h % 3) * 12 : 18 + (h % 5) * 8;
      const outCount = isPeakHour ? 104 : isRush ? 72 + ((h + 1) % 3) * 10 : 15 + ((h + 2) % 4) * 6;
      const inLotCount = Math.max(20, Math.min(100, Math.round(50 + (inCount - outCount) * 1.5)));
      return {
        label: `${String(h).padStart(2, '0')}h`,
        inCount,
        outCount,
        inLotCount,
      };
    });
  } else if (period === 'month') {
    dateRangeText = 'Năm 2026';
    peakPeriodTitle = 'Tháng cao điểm';
    // 12 tháng: T1 đến T12 (Tháng 12 đỉnh cao nhất)
    points = [
      { label: 'T1', inCount: 28400, outCount: 27900, inLotCount: 78 },
      { label: 'T2', inCount: 26200, outCount: 25800, inLotCount: 72 },
      { label: 'T3', inCount: 29800, outCount: 29100, inLotCount: 80 },
      { label: 'T4', inCount: 31200, outCount: 30800, inLotCount: 84 },
      { label: 'T5', inCount: 32500, outCount: 31900, inLotCount: 86 },
      { label: 'T6', inCount: 30100, outCount: 29700, inLotCount: 79 },
      { label: 'T7', inCount: 33400, outCount: 32800, inLotCount: 88 },
      { label: 'T8', inCount: 34100, outCount: 33600, inLotCount: 90 },
      { label: 'T9', inCount: 33800, outCount: 33200, inLotCount: 89 },
      { label: 'T10', inCount: 35200, outCount: 34600, inLotCount: 92 },
      { label: 'T11', inCount: 32000, outCount: 31400, inLotCount: 85 },
      { label: 'T12', inCount: 36500, outCount: 35800, inLotCount: 95 },
    ];
  } else {
    // Mặc định: Tuần (T2 đến CN)
    dateRangeText = '12.10.2026 – 18.10.2026';
    peakPeriodTitle = 'Ngày cao điểm';
    // T7 đông nhất về lưu lượng (vào 1.340, ra 1.290), vượt CN
    points = [
      { label: 'T2', inCount: 820, outCount: 780, inLotCount: 65 },
      { label: 'T3', inCount: 911, outCount: 880, inLotCount: 70 },
      { label: 'T4', inCount: 943, outCount: 915, inLotCount: 72 },
      { label: 'T5', inCount: 1035, outCount: 990, inLotCount: 78 },
      { label: 'T6', inCount: 1103, outCount: 1060, inLotCount: 82 },
      { label: 'T7', inCount: 1340, outCount: 1290, inLotCount: 95 },
      { label: 'CN', inCount: 1250, outCount: 1210, inLotCount: 88 },
    ];
  }

  // Định nghĩa cao điểm = chỉ số có max(inCount, outCount) lớn nhất
  let peakIdx = 0;
  let maxTrafficVal = -1;
  let maxInIdx = 0;
  let maxOutIdx = 0;

  points.forEach((p, idx) => {
    const localMax = Math.max(p.inCount, p.outCount);
    if (localMax > maxTrafficVal) {
      maxTrafficVal = localMax;
      peakIdx = idx;
    }
    if (p.inCount > points[maxInIdx].inCount) {
      maxInIdx = idx;
    }
    if (p.outCount > points[maxOutIdx].outCount) {
      maxOutIdx = idx;
    }
  });

  points[peakIdx].isPeak = true;
  const peakPt = points[peakIdx];

  const totalIn = points.reduce((s, p) => s + p.inCount, 0);
  const totalOut = points.reduce((s, p) => s + p.outCount, 0);

  const peakDisplayLabel =
    period === 'day'
      ? `${peakPt.label} – ${String(parseInt(peakPt.label, 10) + 1).padStart(2, '0')}h`
      : peakPt.label;

  return {
    period,
    points,
    totalIn,
    totalOut,
    dateRangeText,
    peakPeriodTitle,
    peakLabel: peakDisplayLabel,
    peakDetails: `Lúc đó: vào ${peakPt.inCount.toLocaleString('vi-VN')} · ra ${peakPt.outCount.toLocaleString('vi-VN')}`,
    maxInLabel: `${points[maxInIdx].label} (${points[maxInIdx].inCount.toLocaleString('vi-VN')} lượt)`,
    maxOutLabel: `${points[maxOutIdx].label} (${points[maxOutIdx].outCount.toLocaleString('vi-VN')} lượt)`,
  };
}

export function getWeeklyParkingData(week: string = 'current'): WeeklyParkingItem[] {
  let baseItems: WeeklyParkingItem[];

  if (week === 'last') {
    baseItems = [
      { day: 'T2', reserved: 420, walkIn: 440, total: 860 },
      { day: 'T3', reserved: 450, walkIn: 460, total: 910 },
      { day: 'T4', reserved: 480, walkIn: 570, total: 1050 },
      { day: 'T5', reserved: 460, walkIn: 520, total: 980 },
      { day: 'T6', reserved: 510, walkIn: 550, total: 1060 },
      { day: 'T7', reserved: 550, walkIn: 670, total: 1220 },
      { day: 'CN', reserved: 520, walkIn: 560, total: 1080 },
    ];
  } else if (week === '2weeks') {
    baseItems = [
      { day: 'T2', reserved: 390, walkIn: 420, total: 810 },
      { day: 'T3', reserved: 430, walkIn: 440, total: 870 },
      { day: 'T4', reserved: 470, walkIn: 540, total: 1010 },
      { day: 'T5', reserved: 440, walkIn: 500, total: 940 },
      { day: 'T6', reserved: 490, walkIn: 530, total: 1020 },
      { day: 'T7', reserved: 530, walkIn: 640, total: 1170 },
      { day: 'CN', reserved: 500, walkIn: 530, total: 1030 },
    ];
  } else {
    // Current week: Đặt trước tổng ~3.987, Vãng lai tổng ~4.225
    baseItems = [
      { day: 'T2', reserved: 450, walkIn: 461, total: 911 },
      { day: 'T3', reserved: 470, walkIn: 473, total: 943 },
      { day: 'T4', reserved: 510, walkIn: 616, total: 1126 },
      { day: 'T5', reserved: 490, walkIn: 545, total: 1035 },
      { day: 'T6', reserved: 520, walkIn: 583, total: 1103 },
      { day: 'T7', reserved: 650, walkIn: 750, total: 1400 },
      { day: 'CN', reserved: 540, walkIn: 584, total: 1124 },
    ];
  }

  // Ngày có tổng cao nhất được đánh dấu peak
  let maxTotal = -1;
  let peakIdx = 0;
  baseItems.forEach((it, i) => {
    if (it.total > maxTotal) {
      maxTotal = it.total;
      peakIdx = i;
    }
  });

  return baseItems.map((it, i) => ({
    ...it,
    isPeak: i === peakIdx,
  }));
}
