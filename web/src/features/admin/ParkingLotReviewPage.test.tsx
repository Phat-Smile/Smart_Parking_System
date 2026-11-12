import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { LanguageProvider } from "@/shared/i18n/LanguageContext";
import { ParkingLotReviewPage } from "./ParkingLotReviewPage";
import * as api from "./reviewParkingLot";

const LOT = "10000000-0000-0000-0000-000000000001";

// Demo 2 step D02.7: the admin reviews the lot as Parking stores it, then records a decision.
describe("ParkingLotReviewPage", () => {
    beforeEach(() => {
        window.localStorage.clear();
        vi.spyOn(api, "getAdminParkingLots").mockResolvedValue([]);
        vi.spyOn(api, "getAdminParkingLotDetail").mockResolvedValue({
            id: LOT,
            ownerUserId: "20000000-0000-0000-0000-000000000001",
            name: "Bãi Lê Lợi",
            address: "1 Lê Lợi, Quận 1",
            latitude: 10.77,
            longitude: 106.7,
            type: "Outdoor",
            approvalStatus: "Pending",
            rejectionReason: null,
            openingHours: null,
        });
        vi.spyOn(api, "getAdminParkingLotLayout").mockResolvedValue({
            lotId: LOT,
            version: 3,
            floors: [
                {
                    id: "f1",
                    name: "Tầng 1",
                    zones: [
                        {
                            id: "z1",
                            code: "M1",
                            vehicleGroup: "Motorbike",
                            capacity: 50,
                            currentVehicleCount: 0,
                            slots: [],
                        },
                    ],
                },
            ],
        });
        vi.spyOn(api, "getAdminParkingLotPricing").mockImplementation(
            async (_lot, vehicleType) =>
                vehicleType === "Motorbike"
                    ? {
                          lotId: LOT,
                          vehicleType: "MOTORBIKE",
                          version: 1,
                          billingMode: "Block",
                          blockMinutes: 240,
                          price: 5000,
                          overnightFee: 0,
                          effectiveFromUtc: "2026-10-29T00:00:00Z",
                      }
                    : null,
        );
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

    function renderPage() {
        render(
            <LanguageProvider>
                <ParkingLotReviewPage id={LOT} />
            </LanguageProvider>,
        );
    }

    it("shows the stored detail, layout and prices of the lot", async () => {
        renderPage();

        expect(
            await screen.findByRole("heading", { name: "Bãi Lê Lợi" }),
        ).toBeInTheDocument();
        expect(screen.getByText("M1")).toBeInTheDocument();
        expect(screen.getByText("Layout (version 3)")).toBeInTheDocument();
        expect(screen.getByText(/240 min block/)).toBeInTheDocument();
        // No car price yet: said plainly, nothing invented.
        expect(screen.getAllByText("Not set yet").length).toBeGreaterThan(0);
    });

    it("a failed decision is an error, never a preview-mode success", async () => {
        const user = userEvent.setup();
        vi.spyOn(api, "approveParkingLot").mockRejectedValue(
            new TypeError("Failed to fetch"),
        );
        renderPage();

        await user.click(
            await screen.findByRole("button", { name: /Approve/ }),
        );
        await user.click(screen.getByRole("button", { name: "Yes, approve" }));

        expect(await screen.findByRole("alert")).toBeInTheDocument();
        expect(screen.queryByText(/preview mode/i)).not.toBeInTheDocument();
        // The lot is still waiting for a decision.
        await waitFor(() =>
            expect(document.getElementById("review-approve")).not.toBeNull(),
        );
    });
});
