import { describe, it, expect, vi, beforeEach } from "vitest";
import React from "react";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import LotPoliciesPage from "./page";
import * as ownerApi from "@/features/owner/api/ownerApi";
import { useOwnerStore } from "@/store/useOwnerStore";

vi.mock("next/navigation", () => ({
    useParams: () => ({ id: "1" }),
    useRouter: () => ({ push: vi.fn() }),
}));

vi.mock("@/features/owner/lots/ownerLotsSync", () => ({
    backendLotId: () => "lot-guid-12345",
}));

describe("LotPoliciesPage Component", () => {
    beforeEach(() => {
        vi.clearAllMocks();

        useOwnerStore.setState({
            lots: [
                {
                    id: 1,
                    backendId: "lot-guid-12345",
                    name: "Bãi xe Landmark 81",
                    addr: "720A Điện Biên Phủ, P.22, Bình Thạnh",
                    gps: "10.795, 106.721",
                    op: "06:00",
                    cl2: "23:00",
                    vc: true,
                    vm: true,
                    cc: 80,
                    cm: 20,
                    fls: 3,
                    pc: 30000,
                    pm: 5000,
                    dep: 50000,
                    res: "on",
                    hold: 15,
                    maxr: 30,
                    fc: 5,
                    ap: "approved",
                    zones: [],
                },
            ],
        });

        vi.spyOn(ownerApi, "getReservationAvailabilityApi").mockResolvedValue({
            lotId: "lot-guid-12345",
            reservationEnabled: true,
            isConfigured: true,
            updatedAtUtc: "2026-10-30T10:00:00Z",
        });

        vi.spyOn(ownerApi, "getReservationRatioPolicyApi").mockResolvedValue({
            lotId: "lot-guid-12345",
            reservationRatioPercent: 30,
            isConfigured: true,
            updatedAtUtc: "2026-10-30T10:00:00Z",
        });

        vi.spyOn(ownerApi, "getDepositPolicyApi").mockResolvedValue({
            lotId: "lot-guid-12345",
            holdDepositVnd: 50000,
            isConfigured: true,
            updatedAtUtc: "2026-10-30T10:00:00Z",
        });

        vi.spyOn(
            ownerApi,
            "configureReservationAvailabilityApi",
        ).mockResolvedValue({
            lotId: "lot-guid-12345",
            reservationEnabled: false,
            isConfigured: true,
            updatedAtUtc: "2026-10-30T11:00:00Z",
        });

        vi.spyOn(
            ownerApi,
            "configureReservationRatioPolicyApi",
        ).mockResolvedValue({
            lotId: "lot-guid-12345",
            reservationRatioPercent: 40,
            isConfigured: true,
            updatedAtUtc: "2026-10-30T11:00:00Z",
        });

        vi.spyOn(ownerApi, "configureDepositPolicyApi").mockResolvedValue({
            lotId: "lot-guid-12345",
            holdDepositVnd: 100000,
            isConfigured: true,
            updatedAtUtc: "2026-10-30T11:00:00Z",
        });
    });

    it("renders page header and loads initial 3 policies", async () => {
        render(<LotPoliciesPage />);

        expect(
            screen.getByText(/Chính sách đặt chỗ & cọc · Bãi xe Landmark 81/i),
        ).toBeInTheDocument();

        await waitFor(() => {
            expect(ownerApi.getReservationAvailabilityApi).toHaveBeenCalledWith(
                "lot-guid-12345",
            );
            expect(ownerApi.getReservationRatioPolicyApi).toHaveBeenCalledWith(
                "lot-guid-12345",
            );
            expect(ownerApi.getDepositPolicyApi).toHaveBeenCalledWith(
                "lot-guid-12345",
            );
        });

        // Availability badge
        expect(screen.getByText(/● Đang nhận đặt trước/i)).toBeInTheDocument();

        // Ratio distribution
        expect(
            screen.getByText(/2. Tỷ lệ Suất Giữ chỗ \(30%\)/i),
        ).toBeInTheDocument();
        expect(screen.getByText(/● Đặt trước: 30 ô/i)).toBeInTheDocument();
        expect(screen.getByText(/● Vãng lai: 70 ô/i)).toBeInTheDocument();

        // Deposit value
        const depositInput = screen.getByTestId(
            "deposit-vnd-input",
        ) as HTMLInputElement;
        expect(depositInput.value).toBe("50000");
    });

    it("toggles reservation availability checkbox", async () => {
        render(<LotPoliciesPage />);

        await waitFor(() => {
            expect(
                screen.getByTestId("reservation-enabled-checkbox"),
            ).toBeInTheDocument();
        });

        const checkbox = screen.getByTestId(
            "reservation-enabled-checkbox",
        ) as HTMLInputElement;
        expect(checkbox.checked).toBe(true);

        fireEvent.click(checkbox);
        expect(checkbox.checked).toBe(false);
        expect(screen.getByText(/○ Tạm ngưng đặt trước/i)).toBeInTheDocument();
    });

    it("adjusts ratio slider and recalculates reserved slots preview", async () => {
        render(<LotPoliciesPage />);

        await waitFor(() => {
            expect(
                screen.getByTestId("ratio-percent-slider"),
            ).toBeInTheDocument();
        });

        const slider = screen.getByTestId("ratio-percent-slider");
        fireEvent.change(slider, { target: { value: "40" } });

        expect(
            screen.getByText(/2. Tỷ lệ Suất Giữ chỗ \(40%\)/i),
        ).toBeInTheDocument();
        expect(screen.getByText(/● Đặt trước: 40 ô/i)).toBeInTheDocument();
        expect(screen.getByText(/● Vãng lai: 60 ô/i)).toBeInTheDocument();
    });

    it("clicks deposit preset chip and updates deposit input", async () => {
        render(<LotPoliciesPage />);

        await waitFor(() => {
            expect(screen.getByText("100.000 đ")).toBeInTheDocument();
        });

        const preset100k = screen.getByText("100.000 đ");
        fireEvent.click(preset100k);

        const depositInput = screen.getByTestId(
            "deposit-vnd-input",
        ) as HTMLInputElement;
        expect(depositInput.value).toBe("100000");
    });

    it("successfully submits updated policies to backend", async () => {
        render(<LotPoliciesPage />);

        await waitFor(() => {
            expect(
                screen.getByTestId("save-policies-button"),
            ).toBeInTheDocument();
        });

        // Change availability to false
        const checkbox = screen.getByTestId("reservation-enabled-checkbox");
        fireEvent.click(checkbox);

        // Change ratio to 40%
        const slider = screen.getByTestId("ratio-percent-slider");
        fireEvent.change(slider, { target: { value: "40" } });

        // Change deposit to 100k
        const preset100k = screen.getByText("100.000 đ");
        fireEvent.click(preset100k);

        // Submit form
        const submitBtn = screen.getByTestId("save-policies-button");
        fireEvent.click(submitBtn);

        await waitFor(() => {
            expect(
                ownerApi.configureReservationAvailabilityApi,
            ).toHaveBeenCalledWith("lot-guid-12345", {
                reservationEnabled: false,
            });
            expect(
                ownerApi.configureReservationRatioPolicyApi,
            ).toHaveBeenCalledWith("lot-guid-12345", {
                reservationRatioPercent: 40,
            });
            expect(ownerApi.configureDepositPolicyApi).toHaveBeenCalledWith(
                "lot-guid-12345",
                {
                    holdDepositVnd: 100000,
                },
            );
        });

        expect(
            await screen.findByTestId("policies-success-banner"),
        ).toHaveTextContent(/Đã lưu và đồng bộ thành công cả 3 chính sách/i);
    });

    it("displays error banner when API call fails", async () => {
        vi.spyOn(ownerApi, "configureDepositPolicyApi").mockRejectedValue(
            new Error("Lỗi đường truyền mạng"),
        );

        render(<LotPoliciesPage />);

        await waitFor(() => {
            expect(
                screen.getByTestId("save-policies-button"),
            ).toBeInTheDocument();
        });

        const submitBtn = screen.getByTestId("save-policies-button");
        fireEvent.click(submitBtn);

        expect(
            await screen.findByTestId("policies-error-banner"),
        ).toHaveTextContent(/Lỗi kết nối máy chủ khi lưu chính sách/i);
    });
});
