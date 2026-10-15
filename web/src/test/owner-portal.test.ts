import { describe, it, expect } from 'vitest';
import {
  dayRev,
  monRev,
  yrRev,
  hourly,
  dim,
  createInitialSpots,
  K,
  ST,
} from '../types/owner';
import {
  useOwnerStore,
  getSpotCounts,
  getVehicleCounts,
  getFloorCounts,
} from '../store/useOwnerStore';

describe('Owner Portal - Revenue Calculations', () => {
  it('aggregates hourly revenue into day revenue exactly', () => {
    const testCases = [
      { y: 2025, m: 10, d: 20 },
      { y: 2025, m: 1, d: 1 },
      { y: 2026, m: 6, d: 15 },
    ];

    for (const { y, m, d } of testCases) {
      const expectedDay = dayRev(y, m, d);
      const hours = hourly(y, m, d);
      expect(hours).toHaveLength(24);
      const sumHours = hours.reduce((acc, val) => acc + val, 0);
      expect(Math.abs(sumHours - expectedDay)).toBeLessThan(0.01);
    }
  });

  it('aggregates daily revenue into monthly revenue exactly', () => {
    const y = 2025;
    const m = 10;
    const expectedMonth = monRev(y, m);
    const numDays = dim(y, m);
    expect(numDays).toBe(31);

    let sumDays = 0;
    for (let d = 1; d <= numDays; d++) {
      sumDays += dayRev(y, m, d);
    }
    expect(Math.abs(sumDays - expectedMonth)).toBeLessThan(0.01);
  });

  it('aggregates monthly revenue into yearly revenue exactly', () => {
    const y = 2025;
    const expectedYear = yrRev(y);

    let sumMonths = 0;
    for (let m = 1; m <= 12; m++) {
      sumMonths += monRev(y, m);
    }
    expect(Math.abs(sumMonths - expectedYear)).toBeLessThan(0.01);
  });

  it('generates daily revenue in 26M - 46M VND range (weekdays) or higher (weekends)', () => {
    const rev = dayRev(2025, 10, 20); // 2025-10-20 is a Monday
    expect(rev).toBeGreaterThanOrEqual(26_000_000);
    expect(rev).toBeLessThanOrEqual(46_000_000);
  });
});

describe('Owner Portal - Domain & Store State', () => {
  it('creates exactly 500 spots across 5 floors (T1..T5) x 4 rows x 25 bays', () => {
    const spots = createInitialSpots();
    expect(spots).toHaveLength(500);

    const counts = getSpotCounts(spots);
    const totalCounted = K.reduce((acc, k) => acc + counts[k], 0);
    expect(totalCounted).toBe(500);
    expect(counts.available).toBeGreaterThan(0);
    expect(counts.occupied).toBeGreaterThan(0);
    expect(counts.reserved).toBeGreaterThan(0);

    // Verify vehicle breakdown matches occupied count
    const vCounts = getVehicleCounts(spots);
    expect(vCounts.Sedan + vCounts.SUV + vCounts.EV).toBe(counts.occupied);

    // Verify floor counts
    const floors = getFloorCounts(spots);
    expect(floors).toHaveLength(5);
    for (const f of floors) {
      expect(f.count + f.free).toBe(100);
    }
  });

  it('updates slot status and maintains consistency in Zustand store', () => {
    const store = useOwnerStore.getState();
    const testSlotId = 'T1-A1';

    store.updateSpotStatus(testSlotId, 'maintenance');
    const updatedSlot = useOwnerStore.getState().spots.find((s) => s.id === testSlotId);
    expect(updatedSlot?.s).toBe('maintenance');

    store.selectSpot(testSlotId);
    expect(useOwnerStore.getState().selectedId).toBe(testSlotId);

    store.setActiveFloor('T2');
    expect(useOwnerStore.getState().activeFloor).toBe('T2');

    // Simulate tick randomly changes slots among available/reserved/occupied
    store.simulateTick();
    const afterTickSpots = useOwnerStore.getState().spots;
    expect(afterTickSpots).toHaveLength(500);
    for (const s of afterTickSpots) {
      expect(ST[s.s]).toBeDefined();
    }
  });

  it('manages parking lots and zones', () => {
    const store = useOwnerStore.getState();
    const initialLotsCount = store.lots.length;

    // Add new lot
    store.addLot({
      id: 99,
      name: 'Bãi xe thử nghiệm',
      addr: '123 Test St',
      gps: '10.7700, 106.7000',
      op: '07:00',
      cl2: '22:00',
      vc: true,
      vm: false,
      cc: 50,
      cm: 0,
      fls: 1,
      pc: 25000,
      pm: 0,
      dep: 15000,
      res: 'on',
      hold: 15,
      maxr: 10,
      fc: 10,
      ap: 'pending',
      zones: [{ n: 'Khu A', t: 'car', c: 50, s: 'open' }],
    });

    expect(useOwnerStore.getState().lots.length).toBe(initialLotsCount + 1);

    // Add zone to lot 99
    store.addZone(99, { n: 'Khu B', t: 'car', c: 20, s: 'open' });
    const lot99 = useOwnerStore.getState().lots.find((l) => l.id === 99);
    expect(lot99?.zones).toHaveLength(2);

    // Delete zone
    store.deleteZone(99, 1);
    const lot99After = useOwnerStore.getState().lots.find((l) => l.id === 99);
    expect(lot99After?.zones).toHaveLength(1);
  });
});
