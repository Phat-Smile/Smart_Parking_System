import { create } from 'zustand';
import { ParkingLot, ApprovalStatus } from '@/types/owner';
import { useOwnerStore } from '@/store/useOwnerStore';

// Danh sách 4 bãi chuẩn xác theo thiết kế đã duyệt (ảnh 3)
const DESIGN_LOTS: ParkingLot[] = [
  {
    id: 1,
    name: 'Bãi xe Nguyễn Huệ',
    addr: 'Quận 1, TP.HCM',
    gps: '10.7745, 106.7035',
    op: '06:00',
    cl2: '23:00',
    vc: true,
    vm: true,
    cc: 500,
    cm: 200,
    fls: 5,
    pc: 35000,
    pm: 6000,
    dep: 20000,
    res: 'on',
    hold: 30,
    maxr: 10,
    fc: 10,
    ap: 'approved',
    zones: [{ n: 'Khu A', t: 'car', c: 500, s: 'open' }],
  },
  {
    id: 2,
    name: 'Bãi xe Bến Thành',
    addr: 'Quận 1, TP.HCM',
    gps: '10.7725, 106.6982',
    op: '06:00',
    cl2: '22:00',
    vc: true,
    vm: false,
    cc: 320,
    cm: 0,
    fls: 3,
    pc: 30000,
    pm: 5000,
    dep: 15000,
    res: 'on',
    hold: 20,
    maxr: 8,
    fc: 8,
    ap: 'approved',
    zones: [{ n: 'Khu A', t: 'car', c: 320, s: 'open' }],
  },
  {
    id: 3,
    name: 'Bãi xe Thủ Thiêm',
    addr: 'TP. Thủ Đức, TP.HCM',
    gps: '10.7760, 106.7195',
    op: '05:30',
    cl2: '23:00',
    vc: true,
    vm: true,
    cc: 240,
    cm: 100,
    fls: 3,
    pc: 25000,
    pm: 5000,
    dep: 10000,
    res: 'on',
    hold: 15,
    maxr: 5,
    fc: 5,
    ap: 'pending',
    why: 'Đang chờ phê duyệt giấy phép PCCC.',
    zones: [{ n: 'Khu A', t: 'car', c: 240, s: 'open' }],
  },
  {
    id: 4,
    name: 'Bãi xe Phú Mỹ Hưng',
    addr: 'Quận 7, TP.HCM',
    gps: '10.7320, 106.7150',
    op: '06:00',
    cl2: '22:00',
    vc: true,
    vm: false,
    cc: 180,
    cm: 0,
    fls: 2,
    pc: 28000,
    pm: 5000,
    dep: 15000,
    res: 'off',
    hold: 15,
    maxr: 0,
    fc: 0,
    ap: 'rejected',
    why: 'Sơ đồ mặt bằng chưa khớp ranh giới quy hoạch đô thị.',
    zones: [{ n: 'Khu A', t: 'car', c: 180, s: 'open' }],
  },
];

interface OwnerMapState {
  overlayLots: ParkingLot[];
  selectedLotId: number | null;
  filterStatus: 'all' | 'approved' | 'pending' | 'rejected';
  mapSearchQuery: string;
  isAddModalOpen: boolean;

  setSelectedLotId: (id: number | null) => void;
  setFilterStatus: (s: 'all' | 'approved' | 'pending' | 'rejected') => void;
  setMapSearchQuery: (q: string) => void;
  setAddModalOpen: (open: boolean) => void;
  addOverlayLot: (lot: ParkingLot) => void;
  updateLotStatus: (lotId: number, status: ApprovalStatus) => void;
}

export const useOwnerMapStore = create<OwnerMapState>((set) => ({
  overlayLots: DESIGN_LOTS,
  selectedLotId: null,
  filterStatus: 'all',
  mapSearchQuery: '',
  isAddModalOpen: false,

  setSelectedLotId: (id) => set({ selectedLotId: id }),
  setFilterStatus: (s) => set({ filterStatus: s }),
  setMapSearchQuery: (q) => set({ mapSearchQuery: q }),
  setAddModalOpen: (open) => set({ isAddModalOpen: open }),
  addOverlayLot: (lot) =>
    set((state) => ({ overlayLots: [lot, ...state.overlayLots] })),
  updateLotStatus: (lotId, status) =>
    set((state) => ({
      overlayLots: state.overlayLots.map((l) =>
        l.id === lotId ? { ...l, ap: status } : l
      ),
    })),
}));

/**
 * Selector useAllLots():
 * Hợp nhất danh sách bãi từ useOwnerStore và useOwnerMapStore.
 * Mọi component hiển thị danh sách bãi đều dùng selector này.
 */
export function useAllLots(): ParkingLot[] {
  const storeLots = useOwnerStore((s) => s.lots);
  const overlayLots = useOwnerMapStore((s) => s.overlayLots);

  if (overlayLots && overlayLots.length > 0) {
    return overlayLots;
  }
  return storeLots;
}
