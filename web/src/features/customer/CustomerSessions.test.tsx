import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/shared/errors/api-error";
import * as api from "./mySessionsApi";
import { ActiveSessionCard, CustomerSessionsList } from "./CustomerSessions";

// Demo 4 step D04.5: the customer sees the open session and its session QR, as Parking returns it
// (GET /api/parking/sessions/me), never sample data.
const SESSION: api.MyParkingSession = {
    sessionId: "30000000-0000-0000-0000-000000000001",
    lotId: "10000000-0000-0000-0000-000000000001",
    plateNumber: "59A-12345",
    vehicleType: "Motorbike",
    status: "Active",
    entryTime: "2026-10-30T01:00:00Z",
    qrToken: "qr-token-1",
    zoneCode: "M1",
    floorName: "Tầng 1",
};

afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
});

describe("mySessionsApi", () => {
    it("reads the open sessions and the lot name of each session", async () => {
        const fetchMock = vi
            .fn()
            .mockResolvedValueOnce(
                new Response(JSON.stringify([SESSION]), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            )
            .mockResolvedValueOnce(
                new Response(
                    JSON.stringify({
                        id: SESSION.lotId,
                        name: "Bãi Lê Lợi",
                        address: "1 Lê Lợi",
                    }),
                    {
                        status: 200,
                        headers: { "Content-Type": "application/json" },
                    },
                ),
            );
        vi.stubGlobal("fetch", fetchMock);

        expect(await api.getMyParkingSessions()).toEqual([SESSION]);
        expect(await api.getSessionLot(SESSION.lotId)).toEqual({
            name: "Bãi Lê Lợi",
            address: "1 Lê Lợi",
        });
        expect(String(fetchMock.mock.calls[0][0])).toContain(
            "/api/parking/sessions/me",
        );
        expect(String(fetchMock.mock.calls[1][0])).toContain(
            `/api/parking/lots/${SESSION.lotId}`,
        );
    });

    it("a lot that is not public any more has no name instead of an error", async () => {
        vi.stubGlobal(
            "fetch",
            vi.fn().mockResolvedValue(
                new Response(JSON.stringify({ status: 404 }), {
                    status: 404,
                    headers: { "Content-Type": "application/problem+json" },
                }),
            ),
        );
        expect(await api.getSessionLot(SESSION.lotId)).toBeNull();
    });
});

describe("CustomerSessionsList", () => {
    it("shows each open session with its zone and session QR", async () => {
        vi.spyOn(api, "getMyParkingSessions").mockResolvedValue([SESSION]);
        vi.spyOn(api, "getSessionLot").mockResolvedValue({
            name: "Bãi Lê Lợi",
            address: "1 Lê Lợi",
        });

        render(<CustomerSessionsList />);

        expect(await screen.findByText("Bãi Lê Lợi")).toBeInTheDocument();
        expect(screen.getByText(/59A-12345/)).toBeInTheDocument();
        expect(screen.getByText(/M1/)).toBeInTheDocument();
        expect(
            screen.getByTestId(`session-qr-${SESSION.sessionId}`),
        ).toBeInTheDocument();
    });

    it("says so when there is no open session", async () => {
        vi.spyOn(api, "getMyParkingSessions").mockResolvedValue([]);

        render(<CustomerSessionsList />);

        expect(
            await screen.findByText(/chưa có phiên đỗ nào đang mở/i),
        ).toBeInTheDocument();
    });

    it("shows an error when the sessions cannot be read", async () => {
        vi.spyOn(api, "getMyParkingSessions").mockRejectedValue(
            new ApiError(500),
        );

        render(<CustomerSessionsList />);

        expect(await screen.findByRole("alert")).toBeInTheDocument();
    });
});

describe("ActiveSessionCard", () => {
    it("shows no sample session when the customer has none", async () => {
        vi.spyOn(api, "getMyParkingSessions").mockResolvedValue([]);

        render(<ActiveSessionCard />);

        expect(
            await screen.findByText(/chưa có phiên đỗ nào đang mở/i),
        ).toBeInTheDocument();
        expect(screen.queryByText(/Demo preview/i)).not.toBeInTheDocument();
    });

    it("shows the first open session with its QR", async () => {
        vi.spyOn(api, "getMyParkingSessions").mockResolvedValue([SESSION]);
        vi.spyOn(api, "getSessionLot").mockResolvedValue(null);

        render(<ActiveSessionCard />);

        expect(
            await screen.findByTestId(`session-qr-${SESSION.sessionId}`),
        ).toBeInTheDocument();
        expect(screen.getByText(/59A-12345/)).toBeInTheDocument();
    });
});
