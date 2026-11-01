"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import QRCode from "react-qr-code";
import {
    getMyParkingSessions,
    getSessionLot,
    type MyParkingSession,
    type SessionLot,
} from "./mySessionsApi";

type SessionsState =
    | { kind: "loading" }
    | { kind: "error" }
    | {
          kind: "ready";
          sessions: MyParkingSession[];
          lots: Record<string, SessionLot | null>;
      };

// Reads the open sessions and the name of each session's lot once.
function useMyParkingSessions(): SessionsState {
    const [state, setState] = useState<SessionsState>({ kind: "loading" });

    useEffect(() => {
        let active = true;
        (async () => {
            try {
                const sessions = await getMyParkingSessions();
                const lotIds = [...new Set(sessions.map((s) => s.lotId))];
                const lots: Record<string, SessionLot | null> = {};
                await Promise.all(
                    lotIds.map(async (lotId) => {
                        lots[lotId] = await getSessionLot(lotId).catch(
                            () => null,
                        );
                    }),
                );
                if (active) setState({ kind: "ready", sessions, lots });
            } catch {
                if (active) setState({ kind: "error" });
            }
        })();
        return () => {
            active = false;
        };
    }, []);

    return state;
}

function formatEntryTime(value: string) {
    return new Date(value).toLocaleString("vi-VN", {
        hour: "2-digit",
        minute: "2-digit",
        day: "2-digit",
        month: "2-digit",
    });
}

function zoneText(session: MyParkingSession) {
    const parts = [session.floorName, session.zoneCode].filter(Boolean);
    return parts.length > 0 ? parts.join(" · ") : "Chưa có khu";
}

function SessionQr({ session }: { session: MyParkingSession }) {
    return (
        <div
            data-testid={`session-qr-${session.sessionId}`}
            style={{
                background: "#fff",
                padding: 8,
                borderRadius: 12,
                width: 132,
            }}
        >
            <QRCode
                value={session.qrToken}
                size={116}
                style={{ height: "auto", maxWidth: "100%", width: "100%" }}
                viewBox="0 0 116 116"
            />
            <small style={{ display: "block", textAlign: "center" }}>
                QR phiên đỗ
            </small>
        </div>
    );
}

function SessionDetails({
    session,
    lot,
}: {
    session: MyParkingSession;
    lot: SessionLot | null;
}) {
    return (
        <div>
            <h3 style={{ margin: "0 0 4px" }}>{lot?.name ?? "Bãi đỗ xe"}</h3>
            {lot?.address && <p style={{ margin: "0 0 8px" }}>{lot.address}</p>}
            <dl style={{ margin: 0, display: "grid", gap: 4 }}>
                <div>
                    <dt style={{ display: "inline" }}>Xe: </dt>
                    <dd style={{ display: "inline", margin: 0 }}>
                        {session.vehicleType} · {session.plateNumber}
                    </dd>
                </div>
                <div>
                    <dt style={{ display: "inline" }}>Giờ vào: </dt>
                    <dd style={{ display: "inline", margin: 0 }}>
                        {formatEntryTime(session.entryTime)}
                    </dd>
                </div>
                <div>
                    <dt style={{ display: "inline" }}>Khu: </dt>
                    <dd style={{ display: "inline", margin: 0 }}>
                        {zoneText(session)}
                    </dd>
                </div>
            </dl>
        </div>
    );
}

const EMPTY_TEXT = "Bạn chưa có phiên đỗ nào đang mở.";
const ERROR_TEXT =
    "Không tải được phiên đỗ của bạn. Vui lòng thử lại sau ít phút.";

/** Every open session of the customer, each with its session QR (D04.5). */
export function CustomerSessionsList() {
    const state = useMyParkingSessions();

    if (state.kind === "loading") return <p>Đang tải phiên đỗ...</p>;
    if (state.kind === "error") return <p role="alert">{ERROR_TEXT}</p>;
    if (state.sessions.length === 0)
        return (
            <p>
                {EMPTY_TEXT} <Link href="/search">Tìm bãi đỗ</Link>
            </p>
        );

    return (
        <ul style={{ listStyle: "none", padding: 0, display: "grid", gap: 16 }}>
            {state.sessions.map((session) => (
                <li
                    key={session.sessionId}
                    style={{
                        display: "flex",
                        gap: 16,
                        alignItems: "center",
                        flexWrap: "wrap",
                        padding: 16,
                        borderRadius: 16,
                        border: "1px solid #e3e8f2",
                    }}
                >
                    <SessionQr session={session} />
                    <SessionDetails
                        session={session}
                        lot={state.lots[session.lotId] ?? null}
                    />
                </li>
            ))}
        </ul>
    );
}

/** Dashboard card: the first open session, or a plain empty state. */
export function ActiveSessionCard() {
    const state = useMyParkingSessions();

    if (state.kind === "loading") return <p>Đang tải phiên đỗ...</p>;
    if (state.kind === "error") return <p role="alert">{ERROR_TEXT}</p>;
    const [session] = state.sessions;
    if (!session)
        return (
            <p>
                {EMPTY_TEXT} <Link href="/search">Tìm bãi đỗ</Link>
            </p>
        );

    return (
        <div
            style={{
                display: "flex",
                gap: 16,
                alignItems: "center",
                flexWrap: "wrap",
            }}
        >
            <SessionDetails
                session={session}
                lot={state.lots[session.lotId] ?? null}
            />
            <SessionQr session={session} />
            {state.sessions.length > 1 && (
                <Link href="/customer/sessions">
                    Xem tất cả {state.sessions.length} phiên
                </Link>
            )}
        </div>
    );
}
