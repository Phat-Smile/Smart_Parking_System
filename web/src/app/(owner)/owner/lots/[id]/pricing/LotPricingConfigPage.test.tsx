import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import LotPricingConfigPage from "./page";
import * as ownerApi from "@/features/owner/api/ownerApi";
import { useOwnerStore } from "@/store/useOwnerStore";

let mockParams: { id: string } = {
    id: "01931b67-e984-7a1b-bc54-68f44d5c8001",
};

vi.mock("next/navigation", () => ({
    useParams: () => mockParams,
    useRouter: () => ({ push: vi.fn() }),
}));

describe("LotPricingConfigPage Component with GUID Lot Identifier", () => {
    const guidLotId = "01931b67-e984-7a1b-bc54-68f44d5c8001";

    beforeEach(() => {
        vi.clearAllMocks();
        mockParams = { id: guidLotId };

        useOwnerStore.setState({
            lots: [
                {
                    id: 101,
                    backendId: guidLotId,
                    name: "Bãi xe ParkWise Landmark Hub",
                    addr: "720A Điện Biên Phủ, P.22, Bình Thạnh",
                    gps: "10.795, 106.721",
                    op: "06:00",
                    cl2: "23:00",
                    vc: true,
                    vm: true,
                    cc: 120,
                    cm: 50,
                    fls: 3,
                    pc: 35000,
                    pm: 6000,
                    dep: 25000,
                    res: "on",
                    hold: 15,
                    maxr: 30,
                    fc: 5,
                    ap: "approved",
                    zones: [],
                },
            ],
        });

        vi.spyOn(ownerApi, "getDepositPolicyApi").mockResolvedValue({
            lotId: guidLotId,
            holdDepositVnd: 25000,
            isConfigured: true,
            updatedAtUtc: "2026-11-01T08:00:00Z",
        });

        vi.spyOn(ownerApi, "getLotPricingApi").mockResolvedValue({
            lotId: guidLotId,
            items: [
                {
                    lotId: guidLotId,
                    vehicleType: "CAR",
                    version: 1,
                    billingMode: "Block",
                    blockMinutes: 60,
                    price: 35000,
                    overnightFee: 160000,
                    effectiveFromUtc: "2026-11-01T08:00:00Z",
                },
                {
                    lotId: guidLotId,
                    vehicleType: "MOTORBIKE",
                    version: 1,
                    billingMode: "Block",
                    blockMinutes: 60,
                    price: 6000,
                    overnightFee: 35000,
                    effectiveFromUtc: "2026-11-01T08:00:00Z",
                },
            ],
        });

        vi.spyOn(ownerApi, "configurePricingApi").mockResolvedValue({
            isSuccess: true,
            statusCode: 200,
            message: "Configure pricing successfully",
            result: { version: 2, isNewVersionCreated: true },
        });

        vi.spyOn(ownerApi, "configureDepositPolicyApi").mockResolvedValue({
            lotId: guidLotId,
            holdDepositVnd: 25000,
            isConfigured: true,
            updatedAtUtc: "2026-11-01T09:00:00Z",
        });
    });

    it("correctly identifies GUID parking lot and queries real pricing APIs", async () => {
        render(<LotPricingConfigPage />);

        expect(
            screen.getByText(/Cấu hình bảng giá · Bãi xe ParkWise Landmark Hub/i),
        ).toBeInTheDocument();

        await waitFor(() => {
            expect(ownerApi.getDepositPolicyApi).toHaveBeenCalledWith(guidLotId);
            expect(ownerApi.getLotPricingApi).toHaveBeenCalledWith(guidLotId);
        });

        // Navigation back link targets lot detail
        const backLink = screen.getByRole("link", { name: /Về chi tiết bãi/i });
        expect(backLink).toHaveAttribute("href", "/owner/parking-lots/101");
    });

    it("loads existing rates from backend and updates price preview calculator", async () => {
        render(<LotPricingConfigPage />);

        await waitFor(() => {
            expect(ownerApi.getLotPricingApi).toHaveBeenCalled();
        });

        // Default simulator: 3 hours, car, block duration 60 mins -> 3 blocks * 35.000 = 105.000 đ
        expect(screen.getByText(/105\.000/)).toBeInTheDocument();
    });

    it("saves updated pricing policy using GUID lot identifier", async () => {
        render(<LotPricingConfigPage />);

        await waitFor(() => {
            expect(ownerApi.getDepositPolicyApi).toHaveBeenCalled();
        });

        // Click save button
        const saveBtn = screen.getByRole("button", { name: /Lưu bảng giá/i });
        fireEvent.click(saveBtn);

        await waitFor(() => {
            expect(ownerApi.configurePricingApi).toHaveBeenCalledWith(
                guidLotId,
                "CAR",
                expect.objectContaining({
                    billingMode: "Block",
                    price: 35000,
                    blockMinutes: 60,
                    overnightFee: 160000,
                }),
            );
            expect(ownerApi.configurePricingApi).toHaveBeenCalledWith(
                guidLotId,
                "MOTORBIKE",
                expect.objectContaining({
                    billingMode: "Block",
                    price: 6000,
                    blockMinutes: 60,
                    overnightFee: 35000,
                }),
            );
            expect(ownerApi.configureDepositPolicyApi).toHaveBeenCalledWith(
                guidLotId,
                expect.objectContaining({
                    holdDepositVnd: 25000,
                }),
            );
        });

        expect(
            await screen.findByText(/Đã lưu và áp dụng biểu phí & tiền cọc lên hệ thống máy chủ/i),
        ).toBeInTheDocument();
    });

    it("handles direct GUID route even if lot is not yet present in client memory store", async () => {
        // Clear client store
        useOwnerStore.setState({ lots: [] });

        render(<LotPricingConfigPage />);

        await waitFor(() => {
            // isGuid detects the route GUID and uses it directly as lotKey
            expect(ownerApi.getDepositPolicyApi).toHaveBeenCalledWith(guidLotId);
            expect(ownerApi.getLotPricingApi).toHaveBeenCalledWith(guidLotId);
        });

        const saveBtn = screen.getByRole("button", { name: /Lưu bảng giá/i });
        fireEvent.click(saveBtn);

        await waitFor(() => {
            expect(ownerApi.configurePricingApi).toHaveBeenCalledWith(
                guidLotId,
                "CAR",
                expect.anything(),
            );
        });
    });

    it("shows error when lotId is neither in store nor a valid GUID", async () => {
        mockParams = { id: "invalid-mock-lot-999" };
        useOwnerStore.setState({ lots: [] });

        render(<LotPricingConfigPage />);

        const saveBtn = screen.getByRole("button", { name: /Lưu bảng giá/i });
        fireEvent.click(saveBtn);

        expect(
            await screen.findByText(/Bãi này chưa có trên hệ thống nên không lưu được/i),
        ).toBeInTheDocument();
    });
});
