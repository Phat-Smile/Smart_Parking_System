import { create } from 'zustand';

export interface LotPrices {
  carFirstHour: number;
  carNextHour: number;
  overnight: number;
  dailyMax: number;
  motorbikePerVisit: number;
  carMonthlyPass: number;
  reservationDeposit: number;
}

export interface LotInfo {
  openedAt?: string; // ISO date e.g. "2023-03-12"
  submittedAt?: string; // ISO date e.g. "2026-10-08"
  rejectedAt?: string; // ISO date e.g. "2026-10-10"
  rejectReason?: string;
  openingHours: string;
  phone: string;
  prices: Partial<LotPrices>;
}

export const INITIAL_LOT_INFOS: Record<number, LotInfo> = {
  1: {
    openedAt: '2023-03-12',
    openingHours: '24/7',
    phone: '0901 234 567',
    prices: {
      carFirstHour: 25000,
      carNextHour: 15000,
      overnight: 120000,
      dailyMax: 200000,
      motorbikePerVisit: 5000,
      carMonthlyPass: 2500000,
      reservationDeposit: 50000,
    },
  },
  2: {
    openedAt: '2024-06-01',
    openingHours: '05:00 – 23:30',
    phone: '0902 345 678',
    prices: {
      carFirstHour: 20000,
      carNextHour: 12000,
      overnight: 100000,
      dailyMax: 160000,
      motorbikePerVisit: 5000,
      carMonthlyPass: 2200000,
    },
  },
  3: {
    submittedAt: '2026-10-08',
    openingHours: '24/7',
    phone: '0903 456 789',
    prices: {
      carFirstHour: 15000,
      carNextHour: 10000,
      overnight: 80000,
      dailyMax: 120000,
      motorbikePerVisit: 4000,
      carMonthlyPass: 1800000,
    },
  },
  4: {
    rejectedAt: '2026-10-10',
    rejectReason: 'Thiếu giấy phép phòng cháy chữa cháy. Vui lòng bổ sung hồ sơ.',
    openingHours: '06:00 – 22:00',
    phone: '0904 567 890',
    prices: {
      carFirstHour: 18000,
      carNextHour: 12000,
      overnight: 90000,
      dailyMax: 140000,
      motorbikePerVisit: 4000,
      carMonthlyPass: 2000000,
    },
  },
};

interface LotInfoStoreState {
  infoMap: Record<number, LotInfo>;
  setLotInfo: (id: number, info: Partial<LotInfo>) => void;
  resubmitLot: (id: number) => void;
}

export const useLotInfoStore = create<LotInfoStoreState>((set) => ({
  infoMap: INITIAL_LOT_INFOS,
  setLotInfo: (id, newInfo) =>
    set((state) => ({
      infoMap: {
        ...state.infoMap,
        [id]: {
          ...(state.infoMap[id] || {
            openingHours: 'Chưa cập nhật',
            phone: 'Chưa cập nhật',
            prices: {},
          }),
          ...newInfo,
        },
      },
    })),
  resubmitLot: (id) =>
    set((state) => {
      const today = new Date().toISOString().split('T')[0];
      const existing = state.infoMap[id] || {
        openingHours: 'Chưa cập nhật',
        phone: 'Chưa cập nhật',
        prices: {},
      };
      return {
        infoMap: {
          ...state.infoMap,
          [id]: {
            ...existing,
            submittedAt: today,
            rejectedAt: undefined,
            rejectReason: undefined,
          },
        },
      };
    }),
}));

/**
 * Selector trả về thông tin chi tiết của bãi đỗ xe theo ID
 */
export function useLotInfo(id: number | null | undefined): LotInfo | undefined {
  const infoMap = useLotInfoStore((s) => s.infoMap);
  if (!id) return undefined;
  return infoMap[id];
}

/**
 * Định dạng tiền tệ VND với chữ đ, hiển thị "Chưa cập nhật" nếu rỗng / 0
 */
export function formatVnd(value?: number): string {
  if (value === undefined || value === null || isNaN(value) || value <= 0) {
    return 'Chưa cập nhật';
  }
  return new Intl.NumberFormat('vi-VN').format(value) + ' đ';
}

/**
 * Định dạng ngày dd/mm/yyyy từ chuỗi YYYY-MM-DD
 */
export function formatDateVi(dateStr?: string): string {
  if (!dateStr) return 'Chưa cập nhật';
  const parts = dateStr.split('-');
  if (parts.length === 3) {
    return `${parts[2]}/${parts[1]}/${parts[0]}`;
  }
  return dateStr;
}

/**
 * Tính khoảng thời gian tính từ ngày trong quá khứ đến hôm nay (vd: "3 năm 7 tháng" hoặc "dưới 1 tháng")
 */
export function formatDurationSince(dateStr?: string): string {
  if (!dateStr) return '';
  const start = new Date(dateStr);
  const now = new Date();
  if (isNaN(start.getTime())) return '';

  const totalMonths =
    (now.getFullYear() - start.getFullYear()) * 12 + (now.getMonth() - start.getMonth());

  if (totalMonths < 1) {
    return 'dưới 1 tháng';
  }

  const years = Math.floor(totalMonths / 12);
  const months = totalMonths % 12;

  if (years > 0) {
    if (months > 0) {
      return `${years} năm ${months} tháng`;
    }
    return `${years} năm`;
  }
  return `${months} tháng`;
}
