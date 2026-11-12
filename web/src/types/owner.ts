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
    const spots: ParkingSpot[] = [];
    FLOORS.forEach((f, fi) => {
        (["A", "B", "C", "D"] as const).forEach((r, ri) => {
            for (let n = 0; n < 25; n++) {
                const i = fi * 100 + ri * 25 + n;
                spots.push({
                    id: `${f}-${r}${n + 1}`,
                    f,
                    r,
                    n,
                    i,
                    s: pat[(i * 5 + fi) % 12],
                    p: plate(i),
                });
            }
        });
    });
    return spots;
}

// Initial Lots - Empty by default; loaded dynamically from GET /api/parking/owner/lots
export const INITIAL_LOTS: ParkingLot[] = [];

// Initial Reservations
export const INITIAL_RESERVATIONS: Reservation[] = [
    {
        id: "R-1042",
        type: "Đặt trước",
        plate: "51A-123.45",
        slot: "T1-A3",
        time: "14:00 – 16:00",
        status: "Đã xác nhận",
        paid: true,
    },
    {
        id: "R-1043",
        type: "Giữ chỗ",
        plate: "59X1-678.90",
        slot: "T2-B5",
        time: "14:30 – 15:00",
        status: "Đang giữ",
        paid: false,
    },
    {
        id: "R-1044",
        type: "Đặt trước",
        plate: "30H-555.12",
        slot: "T1-B2",
        time: "17:00 – 19:00",
        status: "Đã xác nhận",
        paid: true,
    },
    {
        id: "R-1045",
        type: "Đặt trước",
        plate: "51F-222.33",
        slot: "T3-A8",
        time: "18:00 – 20:00",
        status: "Chờ thanh toán",
        paid: false,
    },
    {
        id: "R-1039",
        type: "Giữ chỗ",
        plate: "72A-111.22",
        slot: "T2-A1",
        time: "09:00 – 10:00",
        status: "Hết hạn",
        paid: false,
    },
    {
        id: "R-1036",
        type: "Đặt trước",
        plate: "50Z-999.99",
        slot: "T1-A9",
        time: "08:00 – 10:00",
        status: "Đã hủy",
        paid: false,
    },
];

// Initial Activities
export const INITIAL_ACTIVITIES: ActivityItem[] = [
    {
        plate: "51A-123.45",
        slot: "T1-A3",
        method: "Ví Smart Parking",
        time: "2 phút",
        statusOrAmount: "Đang đỗ",
    },
    {
        plate: "59X1-678.90",
        slot: "T2-B5",
        method: "Thẻ **** 0224",
        time: "8 phút",
        statusOrAmount: "+20.000 đ",
    },
    {
        plate: "30H-555.12",
        slot: "T1-B2",
        method: "Tiền mặt",
        time: "15 phút",
        statusOrAmount: "+45.000 đ",
    },
    {
        plate: "51F-222.33",
        slot: "T3-A8",
        method: "Ví Smart Parking",
        time: "32 phút",
        statusOrAmount: "+30.000 đ",
    },
    {
        plate: "72A-111.22",
        slot: "T2-A1",
        method: "Thẻ **** 4432",
        time: "1 giờ",
        statusOrAmount: "+60.000 đ",
    },
];
