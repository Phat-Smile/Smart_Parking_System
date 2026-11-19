// Domain types and mathematical models for Smart Parking System Owner Portal

export type SlotStatusKey =
    "available" | "reserved" | "occupied" | "maintenance" | "verify";

export const ST: Record<SlotStatusKey, [string, string]> = {
    available: ["Trống", "#2f9e44"],
    reserved: ["Đã đặt trước", "#2F80ED"],
    occupied: ["Có xe", "#E5484D"],
    maintenance: ["Bảo trì", "#F2A007"],
    verify: ["Cần xác minh", "#8A5CF6"],
};

export const K: SlotStatusKey[] = [
    "available",
    "reserved",
    "occupied",
    "maintenance",
    "verify",
];

export const FLOORS = ["T1", "T2", "T3", "T4", "T5"] as const;
export type Floor = (typeof FLOORS)[number];

export const AC = "#0E9F6E";

export const KIND = [
    "Sedan",
    "SUV",
    "EV",
    "Sedan",
    "EV",
    "SUV",
    "Sedan",
    "EV",
    "SUV",
];

export const p2 = (n: number) => String(n).padStart(2, "0");

export const plate = (i: number) =>
    ["51A", "59X", "30H", "51F", "72A", "50Z"][i % 6] +
    "-" +
    (100 + ((i * 37) % 900)) +
    "." +
    p2(10 + ((i * 13) % 90));

export const pat: SlotStatusKey[] = [
    "available",
    "occupied",
    "occupied",
    "reserved",
    "available",
    "occupied",
    "maintenance",
    "occupied",
    "reserved",
    "available",
    "occupied",
    "verify",
];

export interface ParkingSpot {
    id: string; // e.g. "T1-A1"
    f: Floor;
    r: "A" | "B" | "C" | "D";
    n: number; // 0..24
    i: number; // 0..499
    s: SlotStatusKey;
    p: string; // license plate
    sensorCode?: string; // e.g. "SN-CAR-A01"
}

export type ApprovalStatus = "approved" | "pending" | "rejected" | "draft";

export const AP: Record<ApprovalStatus, [string, string]> = {
    approved: ["Đã duyệt · Hoạt động", "#1a7f37"],
    pending: ["Chờ duyệt", "#b7791f"],
    rejected: ["Bị từ chối", "#d23c42"],
    draft: ["Bản nháp", "#66665f"],
};

export const HC: Record<ApprovalStatus, [string, string]> = {
    approved: ["#c9efd8", "#7fcfa3"],
    pending: ["#fdebbd", "#f4c969"],
    rejected: ["#fcd3d3", "#f19a9a"],
    draft: ["#e4e4e1", "#cfcfca"],
};

export interface ParkingZone {
    n: string;
    t: "car" | "moto";
    c: number;
    s: "open" | "closed";
}

export interface ParkingLot {
    id: number;
    /** Parking's GUID for a real lot; design lots have none. */
    backendId?: string;
    name: string;
    addr: string;
    gps: string;
    op: string;
    cl2: string;
    vc: boolean;
    vm: boolean;
    cc: number;
    cm: number;
    fls: number;
    pc: number;
    pm: number;
    dep: number;
    res: "on" | "off";
    hold: number;
    maxr: number;
    fc: number;
    ap: ApprovalStatus;
    why?: string;
    zones: ParkingZone[];
    features?: {
        supportsElectricVehicles?: boolean;
        supportsLargeVehicles?: boolean;
        hasEvCharging?: boolean;
        isCovered?: boolean;
        hasCctv?: boolean;
    };
}

export interface Reservation {
    id: string;
    type: string;
    plate: string;
    slot: string;
    time: string;
    status:
        "Đã xác nhận" | "Đang giữ" | "Chờ thanh toán" | "Hết hạn" | "Đã hủy";
    paid: boolean;
}

export const RC: Record<Reservation["status"], string> = {
    "Đã xác nhận": "#1a7f37",
    "Đang giữ": "#2F80ED",
    "Chờ thanh toán": "#b7791f",
    "Hết hạn": "#66665f",
    "Đã hủy": "#d23c42",
};

export interface ActivityItem {
    plate: string;
    slot: string;
    method: string;
    time: string;
    statusOrAmount: string;
}

// Currency and numbers
export const vnd = (n: number) => Math.round(n).toLocaleString("vi-VN") + " đ";
export const short = (n: number) =>
    n >= 1e9
        ? (n / 1e9).toFixed(2).replace(".", ",") + " tỷ"
        : (n / 1e6).toFixed(1).replace(".", ",") + " tr";

// Revenue calculation functions (strictly deterministic per date)
export const hs = (s: string) => {
    let x = 7;
    for (let i = 0; i < s.length; i++) {
        x = (x * 33 + s.charCodeAt(i)) % 10007;
    }
    return x / 10007;
};

export const pd = (s: string) => s.split("-").map(Number);
export const dim = (y: number, m: number) => new Date(y, m, 0).getDate();

export const dayRev = (y: number, m: number, d: number) => {
    const w = new Date(y, m - 1, d).getDay();
    return (
        (2.6e7 + hs(`${y}-${m}-${d}`) * 2e7) *
        (w === 0 || w === 6 ? 1.3 : 1) *
        (1 + (y - 2025) * 0.1)
    );
};

export const monRev = (y: number, m: number) => {
    let t = 0;
    const totalDays = dim(y, m);
    for (let d = 1; d <= totalDays; d++) {
        t += dayRev(y, m, d);
    }
    return t;
};

export const yrRev = (y: number) => {
    let t = 0;
    for (let m = 1; m <= 12; m++) {
        t += monRev(y, m);
    }
    return t;
};

export const HW = [
    1, 1, 1, 1, 1, 2, 4, 7, 9, 8, 7, 8, 9, 8, 7, 7, 8, 10, 9, 7, 5, 3, 2, 1,
];

export const hourly = (y: number, m: number, d: number) => {
    const t = dayRev(y, m, d);
    const s = HW.reduce((a, b) => a + b, 0);
    return HW.map((w) => (t * w) / s);
};

// Seed 500 spots
export function createInitialSpots(): ParkingSpot[] {
    return [];
}

// Initial Lots - Empty by default; loaded dynamically from GET /api/parking/owner/lots
export const INITIAL_LOTS: ParkingLot[] = [];

// Initial Reservations
export const INITIAL_RESERVATIONS: Reservation[] = [];

// Initial Activities
export const INITIAL_ACTIVITIES: ActivityItem[] = [];
