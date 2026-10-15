import { create } from 'zustand';

export type HeroState = 'empty' | 'holding' | 'confirmed' | 'active' | 'paid' | 'overstay';

interface DashboardStore {
    heroState: HeroState;
    setHeroState: (state: HeroState) => void;
    // mock data for states
    vehicleType: 'car' | 'motorcycle';
    setVehicleType: (type: 'car' | 'motorcycle') => void;
    countdown: number; // in seconds
    setCountdown: (seconds: number) => void;
}

export const useDashboardStore = create<DashboardStore>((set) => ({
    heroState: 'empty',
    setHeroState: (state) => set({ heroState: state }),
    vehicleType: 'car',
    setVehicleType: (type) => set({ vehicleType: type }),
    countdown: 600, // 10 mins
    setCountdown: (seconds) => set({ countdown: seconds }),
}));
