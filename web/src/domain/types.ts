export type SlotStatus = 'available' | 'assigned' | 'occupied' | 'maintenance' | 'needs_verification' | 'unavailable';

export interface Vehicle {
  plate: string;
  kind?: 'Sedan' | 'SUV' | 'EV';
  type?: 'sedan' | 'suv' | 'truck' | 'motorcycle' | string;
  [key: string]: unknown;
}

export interface Slot {
  id: string;
  code?: string;
  floorId: string;
  zoneId?: string;
  row: number;
  index: number;
  status: SlotStatus;
  vehicleType?: string;
  vehicle?: Vehicle;
  [key: string]: unknown;
}

export interface ParkingLot {
  id: number | string;
  name: string;
  address: string;
  location?: { lat: number; lng: number };
  operatingHours?: { open: string; close: string; is24Hours: boolean };
  capacity?: number;
  floors?: number | Array<{ id: string; level: number; label: string }>;
  [key: string]: unknown;
}
