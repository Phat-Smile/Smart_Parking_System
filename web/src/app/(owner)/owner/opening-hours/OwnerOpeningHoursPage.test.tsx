import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import OwnerOpeningHoursPage from "./page";
import * as ownerApi from "@/features/owner/api/ownerApi";

vi.mock("@/components/layout/OwnerPageHeader", () => ({
    OwnerPageHeader: ({
        title,
        subtitle,
        actions,
    }: {
        title: string;
        subtitle: string;
        actions: ReactNode;
    }) => (
        <div>
            <h1>{title}</h1>
            <p>{subtitle}</p>
            <div>{actions}</div>
        </div>
    ),
}));

const LOT_GUID = "10000000-0000-0000-0000-000000000001";
const { lotsRef } = vi.hoisted(() => ({
    lotsRef: {
        current: [] as Array<{
            id: number;
            name: string;
            ap: string;
            backendId?: string;
        }>,
    },
}));
vi.mock("@/features/owner/lots/useOwnerMapStore", () => ({
    useAllLots: () => lotsRef.current,
}));

describe("OwnerOpeningHoursPage", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        localStorage.clear();
        lotsRef.current = [
            {
                id: 1,
                name: "Bãi xe Nguyễn Huệ",
                ap: "approved",
                backendId: LOT_GUID,
            },
            {
                id: 2,
                name: "Bãi xe Lê Lợi",
                ap: "pending",
                backendId: "10000000-0000-0000-0000-000000000002",
            },
        ];
        vi.spyOn(ownerApi, "getOpeningHoursApi").mockResolvedValue({
            days: [
                {
                    dayOfWeek: 0,
                    isClosed: false,
                    is24Hours: false,
                    opensAt: "06:00",
                    closesAt: "22:00",
                },
                {
                    dayOfWeek: 1,
                    isClosed: false,
                    is24Hours: false,
                    opensAt: "06:00",
                    closesAt: "23:00",
                },
                {
                    dayOfWeek: 2,
                    isClosed: false,
                    is24Hours: false,
                    opensAt: "06:00",
                    closesAt: "23:00",
                },
                {
                    dayOfWeek: 3,
                    isClosed: false,
                    is24Hours: false,
                    opensAt: "06:00",
                    closesAt: "23:00",
                },
                {
                    dayOfWeek: 4,
                    isClosed: false,
                    is24Hours: false,
                    opensAt: "06:00",
                    closesAt: "23:00",
                },
                {
                    dayOfWeek: 5,
                    isClosed: false,
                    is24Hours: false,
                    opensAt: "06:00",
                    closesAt: "23:00",
                },
                {
                    dayOfWeek: 6,
                    isClosed: false,
                    is24Hours: false,
                    opensAt: "06:00",
                    closesAt: "23:00",
                },
            ],
            isConfigured: true,
        });
    });

    it("renders lot selector and 7-day schedule with interactive time dropdowns", async () => {
        vi.spyOn(ownerApi, "getOpeningHoursApi").mockResolvedValue({
            days: [
                {
                    dayOfWeek: 0,
                    isClosed: false,
                    is24Hours: false,
                    opensAt: "06:00",
                    closesAt: "22:00",
                },
                {
                    dayOfWeek: 1,
                    isClosed: false,
                    is24Hours: false,
                    opensAt: "06:00",
                    closesAt: "23:00",
                },
                {
                    dayOfWeek: 2,
                    isClosed: false,
                    is24Hours: false,
                    opensAt: "06:00",
                    closesAt: "23:00",
                },
                {
                    dayOfWeek: 3,
                    isClosed: false,
                    is24Hours: false,
                    opensAt: "06:00",
                    closesAt: "23:00",
                },
                {
                    dayOfWeek: 4,
                    isClosed: false,
                    is24Hours: false,
                    opensAt: "06:00",
                    closesAt: "23:00",
                },
                {
                    dayOfWeek: 5,
                    isClosed: false,
                    is24Hours: false,
                    opensAt: "06:00",
                    closesAt: "23:00",
                },
                {
                    dayOfWeek: 6,
                    isClosed: false,
                    is24Hours: false,
                    opensAt: "06:00",
                    closesAt: "23:00",
                },
            ],
            isConfigured: true,
        });

        render(<OwnerOpeningHoursPage />);

        expect(
            screen.getByRole("heading", { name: "Giờ hoạt động bãi xe" }),
        ).toBeInTheDocument();
        expect(
            await screen.findByText("Bãi xe Nguyễn Huệ"),
        ).toBeInTheDocument();
        expect(screen.getByText("Thứ Hai")).toBeInTheDocument();
        expect(screen.getByText("Chủ Nhật")).toBeInTheDocument();

        // Verify time dropdowns exist for Thứ Hai
        const mondayOpenSelect = screen.getByLabelText("Giờ mở cửa Thứ Hai");
        expect(mondayOpenSelect).toHaveValue("06:00");

        const mondayCloseSelect = screen.getByLabelText("Giờ đóng cửa Thứ Hai");
        expect(mondayCloseSelect).toHaveValue("23:00");
    });

    it("allows changing hours via time dropdowns and updates duration", async () => {
        render(<OwnerOpeningHoursPage />);

        const mondayOpenSelect =
            await screen.findByLabelText("Giờ mở cửa Thứ Hai");
        fireEvent.change(mondayOpenSelect, { target: { value: "07:00" } });
        expect(mondayOpenSelect).toHaveValue("07:00");

        const mondayCloseSelect = screen.getByLabelText("Giờ đóng cửa Thứ Hai");
        fireEvent.change(mondayCloseSelect, { target: { value: "22:00" } });
        expect(mondayCloseSelect).toHaveValue("22:00");

        // 07:00 to 22:00 = 15 hours
        expect(screen.getByText("⏳ 15 tiếng/ngày")).toBeInTheDocument();
    });

    it("allows switching day mode to 24/7 or Closed", async () => {
        render(<OwnerOpeningHoursPage />);
        await screen.findByText("Lịch hoạt động chi tiết từng ngày");

        // Click 24/7 for Monday (index 1 of days)
        const modeButtons = screen.getAllByRole("button", {
            name: /Mở 24\/7/i,
        });
        expect(modeButtons.length).toBeGreaterThan(1);
        fireEvent.click(modeButtons[1]);

        expect(
            await screen.findByText(/Phục vụ liên tục cả ngày và đêm/i),
        ).toBeInTheDocument();
    });

    it("applies bulk hours across weekdays T2-T6", async () => {
        render(<OwnerOpeningHoursPage />);
        await screen.findByText("Lịch hoạt động chi tiết từng ngày");

        const bulkOpen = screen.getByLabelText("Giờ mở cửa hàng loạt");
        fireEvent.change(bulkOpen, { target: { value: "08:00" } });

        const bulkClose = screen.getByLabelText("Giờ đóng cửa hàng loạt");
        fireEvent.change(bulkClose, { target: { value: "21:00" } });

        fireEvent.click(
            screen.getByRole("button", { name: /Áp dụng Thứ 2 - Thứ 6/i }),
        );

        expect(
            await screen.findByText(
                /Đã áp dụng khung giờ 08:00 - 21:00 cho các ngày từ Thứ 2 đến Thứ 6/i,
            ),
        ).toBeInTheDocument();

        expect(screen.getByLabelText("Giờ mở cửa Thứ Hai")).toHaveValue(
            "08:00",
        );
        expect(screen.getByLabelText("Giờ đóng cửa Thứ Hai")).toHaveValue(
            "21:00",
        );
    });

    it("saves opening hours successfully when clicking Save button", async () => {
        const saveSpy = vi
            .spyOn(ownerApi, "configureOpeningHoursApi")
            .mockResolvedValue({
                days: [],
            });

        render(<OwnerOpeningHoursPage />);

        const saveButtons = screen.getAllByRole("button", {
            name: /Lưu giờ hoạt động/i,
        });
        fireEvent.click(saveButtons[0]);

        await waitFor(() => {
            expect(saveSpy).toHaveBeenCalledWith(
                LOT_GUID,
                expect.objectContaining({
                    days: expect.any(Array),
                }),
            );
        });

        expect(
            await screen.findByText(
                /Đã lưu và đồng bộ lịch hoạt động 7 ngày lên máy chủ bãi xe thành công!/i,
            ),
        ).toBeInTheDocument();
    });

    it("a lost connection while saving is an error, not a saved schedule", async () => {
        vi.spyOn(ownerApi, "configureOpeningHoursApi").mockRejectedValue(
            new TypeError("Failed to fetch"),
        );

        render(<OwnerOpeningHoursPage />);
        fireEvent.click(
            screen.getAllByRole("button", { name: /Lưu giờ hoạt động/i })[0],
        );

        expect(
            await screen.findByText(/Giờ hoạt động chưa được lưu/i),
        ).toBeInTheDocument();
        expect(
            screen.queryByText(/lưu giờ hoạt động vào hệ thống thành công/i),
        ).not.toBeInTheDocument();
        expect(localStorage.length).toBe(0);
    });

    it("a design lot without a Parking id is never sent to the backend", async () => {
        lotsRef.current = [{ id: 7, name: "Bãi mẫu", ap: "approved" }];
        const saveSpy = vi.spyOn(ownerApi, "configureOpeningHoursApi");

        render(<OwnerOpeningHoursPage />);
        fireEvent.click(
            screen.getAllByRole("button", { name: /Lưu giờ hoạt động/i })[0],
        );

        expect(
            await screen.findByText(/chưa có trên hệ thống/i),
        ).toBeInTheDocument();
        expect(saveSpy).not.toHaveBeenCalled();
        expect(ownerApi.getOpeningHoursApi).not.toHaveBeenCalled();
    });
});
