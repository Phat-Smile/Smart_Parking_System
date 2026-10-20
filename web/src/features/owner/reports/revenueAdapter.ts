/**
 * revenueAdapter.ts – Tính toán doanh thu, phân bổ theo tầng, phương thức, khung giờ.
 */

import { pd, p2, dim, dayRev, monRev, yrRev, vnd, short } from '@/types/owner';

export interface RevenueKpi {
  totalRevenue: number;
  totalRevenueText: string;
  avgDaily: number;
  avgDailyText: string;
  totalTransactions: number;
  onlineRatePct: number;
}

export interface RevenueBarItem {
  label: string;
  revenue: number;
  revenueText: string;
  isPeak?: boolean;
}

export interface FloorRevenueItem {
  floor: string;
  name: string;
  revenue: number;
  revenueText: string;
  pct: number;
}

export interface PaymentMethodItem {
  name: string;
  pct: number;
  color: string;
  amount: number;
  amountText: string;
}

export interface PeakTimeItem {
  timeRange: string;
  sessions: number;
  revenue: number;
  revenueText: string;
}

export interface ReportTableRow {
  time: string;
  sessions: number;
  revenue: number;
  revenueText: string;
  popularMethod: string;
  status: string;
}

export function getRevenueReportData(mode: 'day' | 'month' | 'year', dateStr: string) {
  const [y, m, d] = pd(dateStr || '2025-10-20');

  let totalRev = 0;
  let barItems: RevenueBarItem[] = [];

  if (mode === 'day') {
    totalRev = dayRev(y, m, d);
    // 24 hours
    barItems = Array.from({ length: 24 }, (_, h) => {
      const isPeak = (h >= 8 && h <= 10) || (h >= 17 && h <= 19);
      const ratio = isPeak ? 0.08 : 0.025;
      const rev = Math.round(totalRev * ratio * (0.8 + ((h * 7) % 5) * 0.1));
      return {
        label: `${p2(h)}h`,
        revenue: rev,
        revenueText: short(rev),
        isPeak: h === 18,
      };
    });
  } else if (mode === 'month') {
    totalRev = monRev(y, m);
    const numDays = dim(y, m);
    barItems = Array.from({ length: numDays }, (_, i) => {
      const dayNum = i + 1;
      const rev = dayRev(y, m, dayNum);
      return {
        label: `${p2(dayNum)}`,
        revenue: rev,
        revenueText: short(rev),
        isPeak: dayNum === 15 || dayNum === 20,
      };
    });
  } else {
    // Year
    totalRev = yrRev(y);
    barItems = Array.from({ length: 12 }, (_, i) => {
      const monthNum = i + 1;
      const rev = monRev(y, monthNum);
      return {
        label: `T${monthNum}`,
        revenue: rev,
        revenueText: short(rev),
        isPeak: monthNum === 10 || monthNum === 12,
      };
    });
  }

  // KPIs
  const totalTransactions = Math.round(totalRev / 35000);
  const avgDaily = mode === 'day' ? totalRev : Math.round(totalRev / (mode === 'month' ? dim(y, m) : 365));

  const kpis: RevenueKpi = {
    totalRevenue: totalRev,
    totalRevenueText: short(totalRev),
    avgDaily,
    avgDailyText: short(avgDaily),
    totalTransactions,
    onlineRatePct: 82,
  };

  // Floor revenue breakdown (T1 to T5)
  const floorWeights = [0.28, 0.24, 0.18, 0.16, 0.14];
  const floorItems: FloorRevenueItem[] = ['T1', 'T2', 'T3', 'T4', 'T5'].map((f, i) => {
    const rev = Math.round(totalRev * floorWeights[i]);
    return {
      floor: f,
      name: `Tầng ${i + 1}`,
      revenue: rev,
      revenueText: short(rev),
      pct: Math.round(floorWeights[i] * 100),
    };
  });

  // Payment methods
  const paymentMethods: PaymentMethodItem[] = [
    { name: 'Ví Smart Parking', pct: 45, color: '#2563eb', amount: Math.round(totalRev * 0.45), amountText: short(totalRev * 0.45) },
    { name: 'VNPay QR', pct: 32, color: '#16a36a', amount: Math.round(totalRev * 0.32), amountText: short(totalRev * 0.32) },
    { name: 'MoMo', pct: 15, color: '#d946ef', amount: Math.round(totalRev * 0.15), amountText: short(totalRev * 0.15) },
    { name: 'Tiền mặt', pct: 8, color: '#f59e0b', amount: Math.round(totalRev * 0.08), amountText: short(totalRev * 0.08) },
  ];

  // Peak times
  const peakTimes: PeakTimeItem[] = [
    { timeRange: '17:00 – 19:00', sessions: 284, revenue: Math.round(totalRev * 0.26), revenueText: short(totalRev * 0.26) },
    { timeRange: '08:00 – 10:00', sessions: 235, revenue: Math.round(totalRev * 0.22), revenueText: short(totalRev * 0.22) },
    { timeRange: '11:30 – 13:30', sessions: 180, revenue: Math.round(totalRev * 0.18), revenueText: short(totalRev * 0.18) },
    { timeRange: '19:00 – 21:00', sessions: 156, revenue: Math.round(totalRev * 0.15), revenueText: short(totalRev * 0.15) },
  ];

  // Detail table data
  const tableRows: ReportTableRow[] = barItems.slice(0, 15).map((b) => ({
    time: mode === 'day' ? `${b.label}:00` : mode === 'month' ? `Ngày ${b.label}` : `Tháng ${b.label}`,
    sessions: Math.round(b.revenue / 32000),
    revenue: b.revenue,
    revenueText: vnd(b.revenue),
    popularMethod: 'Ví Smart Parking',
    status: 'Hoàn thành',
  }));

  // Excel export table format (2D array of rows)
  const excelData: (string | number)[][] = [
    ['BÁO CÁO DOANH THU THÔNG MINH PARKING SYSTEM'],
    ['Kỳ báo cáo:', mode === 'day' ? `Ngày ${p2(d)}/${p2(m)}/${y}` : mode === 'month' ? `Tháng ${p2(m)}/${y}` : `Năm ${y}`],
    ['Tổng doanh thu:', vnd(totalRev)],
    ['Tổng lượt giao dịch:', totalTransactions],
    ['Tỷ lệ thanh toán online:', '82%'],
    [],
    ['Thời gian', 'Lượt đỗ xe', 'Doanh thu (VNĐ)', 'Phương thức phổ biến', 'Trạng thái'],
    ...tableRows.map((r) => [r.time, r.sessions, r.revenue, r.popularMethod, r.status]),
  ];

  return {
    mode,
    y,
    m,
    d,
    totalRev,
    kpis,
    barItems,
    floorItems,
    paymentMethods,
    peakTimes,
    tableRows,
    excelData,
  };
}
