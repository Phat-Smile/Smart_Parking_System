import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
    translations,
    type SupportedLanguage,
} from "@/shared/i18n/translations";

const { mockState, apiMocks } = vi.hoisted(() => ({
    mockState: { language: "vi" as SupportedLanguage },
    apiMocks: {
        findSessionByQr: vi.fn(),
        payParkingSessionCash: vi.fn(),
        completeParkingSession: vi.fn(),
    },
}));

vi.mock("@/shared/i18n/LanguageContext", () => ({
    useLanguage: () => ({
        language: mockState.language,
        t: (key: keyof (typeof translations)["vi"]) =>
            translations[mockState.language][key] ?? key,
    }),
}));

vi.mock("./RealCameraFeed", () => ({
    RealCameraFeed: () => <div data-testid="real-camera-feed" />,
}));

vi.mock("./audio", () => ({
    useGateVoice: () => ({
        announceCheckOut: vi.fn(),
        announceWarning: vi.fn(),
    }),
}));

vi.mock("./staffData", () => ({
    useStaffData: () => ({
        assignedLot: { lotId: "lot-1", lotName: "Bãi Test" },
        completeExit: vi.fn(),
        activeSessions: [],
        fetchAssignedLot: vi.fn().mockResolvedValue(null),
    }),
}));

vi.mock("./staffApi", async (importOriginal) => {
    const actual = await importOriginal<typeof import("./staffApi")>();
    return {
        ...actual,
        findSessionByQr: apiMocks.findSessionByQr,
        payParkingSessionCash: apiMocks.payParkingSessionCash,
        completeParkingSession: apiMocks.completeParkingSession,
    };
});

import { StaffExitPage } from "./StaffExitPage";

const SESSION_ID = "11111111-1111-1111-1111-111111111111";

function unpaidSession() {
    return {
        sessionId: SESSION_ID,
        lotId: "lot-1",
        plateNumber: "51B-999.88",
        vehicleType: "Car",
        customerType: "Registered",
        status: "Active",
        entryTime: "2026-11-12T01:00:00Z",
        qrToken: "qr-abc",
        totalFeeVnd: 15000,
        amountDueVnd: 15000,
    };
}

async function scanQr() {
    fireEvent.change(document.getElementById("staff-qr-input")!, {
        target: { value: "qr-abc" },
    });
    fireEvent.click(screen.getByText(translations.vi.exitVerifyQrBtn));
    await waitFor(() =>
        expect(apiMocks.findSessionByQr).toHaveBeenCalledWith("qr-abc"),
    );
}

describe("StaffExitPage payment and exit", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mockState.language = "vi";
        apiMocks.findSessionByQr.mockResolvedValue(unpaidSession());
    });

    it("the exit button stays disabled while the session is unpaid", async () => {
        render(<StaffExitPage />);
        await scanQr();

        const exit = await screen.findByText(translations.vi.exitAuthorizeBtn);
        expect(exit.closest("button")).toBeDisabled();
        expect(apiMocks.completeParkingSession).not.toHaveBeenCalled();
    });

    it("cash collection calls Payment with the quoted amount and then allows the exit", async () => {
        apiMocks.payParkingSessionCash.mockResolvedValue({
            transactionId: "t1",
            sessionId: SESSION_ID,
            amountPaid: 15000,
        });
        render(<StaffExitPage />);
        await scanQr();

        fireEvent.click(
            await screen.findByRole("button", { name: /Thu tiền mặt/ }),
        );
        fireEvent.click(
            await screen.findByText(translations.vi.exitConfirmCashPayment),
        );

        await waitFor(() =>
            expect(apiMocks.payParkingSessionCash).toHaveBeenCalledWith(
                SESSION_ID,
                15000,
            ),
        );
        await waitFor(() =>
            expect(
                screen
                    .getByText(translations.vi.exitAuthorizeBtn)
                    .closest("button"),
            ).toBeEnabled(),
        );
    });

    it("a refused cash collection does not mark the session paid", async () => {
        apiMocks.payParkingSessionCash.mockRejectedValue(new Error("boom"));
        render(<StaffExitPage />);
        await scanQr();

        fireEvent.click(
            await screen.findByRole("button", { name: /Thu tiền mặt/ }),
        );
        fireEvent.click(
            await screen.findByText(translations.vi.exitConfirmCashPayment),
        );

        await waitFor(() =>
            expect(apiMocks.payParkingSessionCash).toHaveBeenCalled(),
        );
        expect(
            screen
                .getByText(translations.vi.exitAuthorizeBtn)
                .closest("button"),
        ).toBeDisabled();
    });

    it("the exit sends the plate and the session QR the server requires", async () => {
        apiMocks.findSessionByQr.mockResolvedValue({
            ...unpaidSession(),
            status: "Paid",
            amountDueVnd: 0,
        });
        apiMocks.completeParkingSession.mockResolvedValue({
            sessionId: SESSION_ID,
            status: "Completed",
        });
        render(<StaffExitPage />);
        await scanQr();

        const exit = await screen.findByText(translations.vi.exitAuthorizeBtn);
        await waitFor(() => expect(exit.closest("button")).toBeEnabled());
        fireEvent.click(exit);

        await waitFor(() =>
            expect(apiMocks.completeParkingSession).toHaveBeenCalledWith(
                SESSION_ID,
                { plateNumber: "51B-999.88", qrToken: "qr-abc" },
            ),
        );
    });
});
