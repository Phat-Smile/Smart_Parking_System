import { NextResponse } from "next/server";
import fs from "fs";
import path from "path";

export interface ShiftRecord {
    lotId: string;
    accountId: string;
    shift: "morning" | "afternoon" | "night" | "allday";
    shiftName: string;
    hours: string;
    assignedAt: string;
}

// Default initial shift assignments (persisted in file or memory)
const DATA_FILE = path.join(process.cwd(), ".next", "staff-shifts-store.json");

function readStore(): ShiftRecord[] {
    try {
        if (fs.existsSync(DATA_FILE)) {
            const raw = fs.readFileSync(DATA_FILE, "utf-8");
            return JSON.parse(raw);
        }
    } catch {
        // Fallback to defaults
    }
    return [
        {
            lotId: "10000000-0000-0000-0000-000000000002",
            accountId: "88888888-8888-8888-8888-888888888881",
            shift: "morning",
            shiftName: "Ca 1 - Sáng",
            hours: "06:00 - 14:00",
            assignedAt: new Date().toISOString(),
        },
        {
            lotId: "10000000-0000-0000-0000-000000000002",
            accountId: "88888888-8888-8888-8888-888888888882",
            shift: "afternoon",
            shiftName: "Ca 2 - Chiều",
            hours: "14:00 - 22:00",
            assignedAt: new Date().toISOString(),
        },
        {
            lotId: "10000000-0000-0000-0000-000000000002",
            accountId: "88888888-8888-8888-8888-888888888883",
            shift: "night",
            shiftName: "Ca 3 - Đêm",
            hours: "22:00 - 06:00",
            assignedAt: new Date().toISOString(),
        },
    ];
}

function writeStore(records: ShiftRecord[]) {
    try {
        const dir = path.dirname(DATA_FILE);
        if (!fs.existsSync(dir)) {
            fs.mkdirSync(dir, { recursive: true });
        }
        fs.writeFileSync(DATA_FILE, JSON.stringify(records, null, 2), "utf-8");
    } catch {
        // Silent if cannot write
    }
}

export async function GET(request: Request) {
    const { searchParams } = new URL(request.url);
    const lotId = searchParams.get("lotId");
    const accountId = searchParams.get("accountId");

    const store = readStore();

    if (lotId && accountId) {
        const found = store.find(
            (r) =>
                r.lotId.toLowerCase() === lotId.toLowerCase() &&
                r.accountId.toLowerCase() === accountId.toLowerCase(),
        );
        return NextResponse.json(found || null);
    }

    if (lotId) {
        const list = store.filter(
            (r) => r.lotId.toLowerCase() === lotId.toLowerCase(),
        );
        return NextResponse.json(list);
    }

    if (accountId) {
        const list = store.filter(
            (r) => r.accountId.toLowerCase() === accountId.toLowerCase(),
        );
        return NextResponse.json(list);
    }

    return NextResponse.json(store);
}

export async function POST(request: Request) {
    try {
        const body = (await request.json()) as Partial<ShiftRecord>;
        if (!body.lotId || !body.accountId || !body.shift) {
            return NextResponse.json(
                { error: "lotId, accountId and shift are required." },
                { status: 400 },
            );
        }

        const shiftKey = body.shift;
        let shiftName = body.shiftName;
        let hours = body.hours;

        if (!shiftName || !hours) {
            switch (shiftKey) {
                case "morning":
                    shiftName = "Ca 1 - Sáng";
                    hours = "06:00 - 14:00";
                    break;
                case "afternoon":
                    shiftName = "Ca 2 - Chiều";
                    hours = "14:00 - 22:00";
                    break;
                case "night":
                    shiftName = "Ca 3 - Đêm";
                    hours = "22:00 - 06:00";
                    break;
                case "allday":
                default:
                    shiftName = "Cả ngày";
                    hours = "00:00 - 23:59";
                    break;
            }
        }

        const newRecord: ShiftRecord = {
            lotId: body.lotId,
            accountId: body.accountId,
            shift: shiftKey,
            shiftName,
            hours,
            assignedAt: new Date().toISOString(),
        };

        const store = readStore();
        // 1 staff 1 lot policy
        const updated = store.filter((r) => r.accountId.toLowerCase() !== body.accountId!.toLowerCase());
        updated.push(newRecord);
        writeStore(updated);

        return NextResponse.json({ success: true, record: newRecord });
    } catch {
        return NextResponse.json(
            { error: "Invalid JSON body" },
            { status: 400 },
        );
    }
}

export async function DELETE(request: Request) {
    const { searchParams } = new URL(request.url);
    const lotId = searchParams.get("lotId");
    const accountId = searchParams.get("accountId");

    if (!lotId || !accountId) {
        return NextResponse.json(
            { error: "lotId and accountId are required." },
            { status: 400 },
        );
    }

    const store = readStore();
    const updated = store.filter(
        (r) =>
            !(
                r.lotId.toLowerCase() === lotId.toLowerCase() &&
                r.accountId.toLowerCase() === accountId.toLowerCase()
            ),
    );
    writeStore(updated);

    return NextResponse.json({ success: true });
}
