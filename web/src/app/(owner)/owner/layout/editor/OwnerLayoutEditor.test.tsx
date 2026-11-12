import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import React from "react";
import OwnerLayoutEditorPage from "./page";
import * as layoutApi from "@/features/owner/layout/ownerLayoutApi";
import { ApiError } from "@/shared/errors/api-error";

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

const LOT_ID = "20000000-0000-0000-0000-00000000000b";
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

describe("OwnerLayoutEditorPage", () => {
    beforeEach(() => {
        vi.restoreAllMocks();
        lotsRef.current = [
            {
                id: 101,
                backendId: LOT_ID,
                name: "Bãi xe Nguyễn Huệ",
                ap: "approved",
            },
        ];
    });

    it("renders page title and loads existing layout from backend", async () => {
        vi.spyOn(layoutApi, "getOwnerParkingLayout").mockResolvedValue({
            lotId: LOT_ID,
            version: 3,
            floors: [
                {
                    id: "floor-uuid-1",
                    name: "Tầng Trệt",
                    zones: [
                        {
                            id: "zone-uuid-1",
                            code: "CAR-A",
                            vehicleGroup: "Car",
                            capacity: 5,
                            hasSlotSensor: true,
                            slots: [
                                {
                                    id: "slot-uuid-1",
                                    slotCode: "A-01",
                                    position: { x: 0, y: 0 },
                                },
                            ],
                        },
                    ],
                },
            ],
        });

        render(<OwnerLayoutEditorPage />);

        expect(
            screen.getByText("Chỉnh sửa sơ đồ mặt bằng"),
        ).toBeInTheDocument();

        await waitFor(() => {
            expect(screen.getByText("Tầng Trệt (1 khu)")).toBeInTheDocument();
            expect(screen.getByText("Khu: CAR-A")).toBeInTheDocument();
        });
    });

    it("adds a new floor when clicking '+ Thêm tầng'", async () => {
        vi.spyOn(layoutApi, "getOwnerParkingLayout").mockResolvedValue({
            lotId: LOT_ID,
            version: 0,
            floors: [layoutApi.createDefaultFloor(1)],
        });

        render(<OwnerLayoutEditorPage />);

        await waitFor(() => {
            expect(screen.getByText("Tầng 1 (2 khu)")).toBeInTheDocument();
        });

        const addFloorBtn = screen.getByText("+ Thêm tầng");
        fireEvent.click(addFloorBtn);

        await waitFor(() => {
            expect(screen.getByText("Tầng 2 (2 khu)")).toBeInTheDocument();
        });
    });

    it("saves layout successfully and displays success message", async () => {
        vi.spyOn(layoutApi, "getOwnerParkingLayout").mockResolvedValue({
            lotId: LOT_ID,
            version: 1,
            floors: [
                {
                    id: "floor-uuid-1",
                    name: "Tầng 1",
                    zones: [
                        {
                            id: "zone-uuid-car",
                            code: "A",
                            vehicleGroup: "Car",
                            capacity: 2,
                            hasSlotSensor: false,
                            slots: [
                                {
                                    id: "slot-uuid-1",
                                    slotCode: "A-01",
                                    position: { x: 0, y: 0 },
                                },
                            ],
                        },
                    ],
                },
            ],
        });

        const saveSpy = vi
            .spyOn(layoutApi, "saveOwnerParkingLayout")
            .mockResolvedValue({
                lotId: LOT_ID,
                version: 2,
                floors: [
                    {
                        id: "floor-uuid-1",
                        name: "Tầng 1",
                        zones: [
                            {
                                id: "zone-uuid-car",
                                code: "A",
                                vehicleGroup: "Car",
                                capacity: 2,
                                hasSlotSensor: false,
                                slots: [
                                    {
                                        id: "slot-uuid-1",
                                        slotCode: "A-01",
                                        position: { x: 0, y: 0 },
                                    },
                                ],
                            },
                        ],
                    },
                ],
            });

        render(<OwnerLayoutEditorPage />);

        await waitFor(() => {
            expect(screen.getByText(/Tầng 1/)).toBeInTheDocument();
        });

        const saveBtn = screen.getByText("Lưu sơ đồ");
        fireEvent.click(saveBtn);

        await waitFor(() => {
            expect(saveSpy).toHaveBeenCalledWith(
                LOT_ID,
                expect.objectContaining({
                    expectedVersion: 1,
                }),
            );
            expect(screen.getByRole("status")).toHaveTextContent(
                "Lưu sơ đồ mặt bằng thành công!",
            );
        });
    });

    it("displays error when server returns 409 stale_version", async () => {
        vi.spyOn(layoutApi, "getOwnerParkingLayout").mockResolvedValue({
            lotId: LOT_ID,
            version: 1,
            floors: [layoutApi.createDefaultFloor(1)],
        });

        vi.spyOn(layoutApi, "saveOwnerParkingLayout").mockRejectedValue(
            new ApiError(409, {
                errorCode: "parking.layout.stale_version",
                title: "Layout has changed",
            }),
        );

        render(<OwnerLayoutEditorPage />);

        await waitFor(() => {
            expect(screen.getByText(/Tầng 1/)).toBeInTheDocument();
        });

        const saveBtn = screen.getByText("Lưu sơ đồ");
        fireEvent.click(saveBtn);

        await waitFor(() => {
            expect(screen.getByRole("alert")).toHaveTextContent(
                "phiên bản mới nhất",
            );
        });
    });
});
