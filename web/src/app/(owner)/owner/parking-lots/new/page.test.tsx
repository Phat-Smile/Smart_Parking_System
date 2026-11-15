import React from "react";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import NewParkingLotPage from "./page";
import { ApiError } from "@/shared/errors/api-error";

const { apiMocks } = vi.hoisted(() => ({
    apiMocks: {
        registerParkingLotApi: vi.fn(),
        confirmOwnerLotEntrance: vi.fn(),
        saveInitialLotSetup: vi.fn(),
        loadOwnerLots: vi.fn(),
    },
}));

vi.mock("@/features/owner/api/ownerApi", async (importOriginal) => {
    const actual =
        await importOriginal<typeof import("@/features/owner/api/ownerApi")>();
    return {
        ...actual,
        registerParkingLotApi: apiMocks.registerParkingLotApi,
        confirmOwnerLotEntrance: apiMocks.confirmOwnerLotEntrance,
    };
});

vi.mock("@/features/owner/lots/initialLotSetup", () => ({
    saveInitialLotSetup: () => apiMocks.saveInitialLotSetup(),
}));

vi.mock("@/features/owner/lots/ownerLotsSync", () => ({
    loadOwnerLots: () => apiMocks.loadOwnerLots(),
    localLotId: () => 101,
}));

vi.mock("@/features/owner/components/OwnerLocationPicker", () => ({
    OwnerLocationPicker: (props: {
        address?: string;
        latitude?: number | null;
        longitude?: number | null;
        confirmedOnMap?: boolean;
        onChange: (coords: {
            address: string;
            latitude: number | null;
            longitude: number | null;
            confirmedOnMap?: boolean;
        }) => void;
        onEntranceConfirmed?: (token: string, prompt: string) => void;
    }) => (
        <div data-testid="mock-location-picker">
            <input
                data-testid="picker-address"
                value={props.address}
                onChange={(e) =>
                    props.onChange({
                        address: e.target.value,
                        latitude: props.latitude ?? null,
                        longitude: props.longitude ?? null,
                        confirmedOnMap: props.confirmedOnMap,
                    })
                }
            />
            {/* Giả lập thao tác Owner chạm/kéo ghim trên bản đồ */}
            <button
                type="button"
                data-testid="simulate-place-pin"
                onClick={() =>
                    props.onChange({
                        address: props.address ?? "",
                        latitude: 10.7769,
                        longitude: 106.7009,
                        confirmedOnMap: false,
                    })
                }
            >
                Place Pin
            </button>
            {/* Giả lập đặt ghim tại tọa độ (0, 0) */}
            <button
                type="button"
                data-testid="simulate-zero-coords"
                onClick={() =>
                    props.onChange({
                        address: props.address ?? "",
                        latitude: 0,
                        longitude: 0,
                        confirmedOnMap: false,
                    })
                }
            >
                Zero Coords
            </button>
            {/* Giả lập di chuyển ghim sang tọa độ mới */}
            <button
                type="button"
                data-testid="simulate-move-pin"
                onClick={() =>
                    props.onChange({
                        address: props.address ?? "",
                        latitude: 10.78,
                        longitude: 106.71,
                        confirmedOnMap: false,
                    })
                }
            >
                Move Pin
            </button>
        </div>
    ),
}));

describe("NewParkingLotPage with LocationConfirmationToken (WP-OW-1 / BE2 Spec Section 5)", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        apiMocks.registerParkingLotApi.mockResolvedValue({
            id: "lot-uuid-1",
            name: "Bãi Đỗ Trung Tâm",
            address: "123 Lê Lợi, Quận 1",
            latitude: 10.7769,
            longitude: 106.7009,
            type: "MultiStorey",
            approvalStatus: "Pending",
        });
        apiMocks.confirmOwnerLotEntrance.mockResolvedValue({
            locationConfirmationToken: "test-valid-confirmation-token-v1",
            expiresAtUtc: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
        });
        apiMocks.saveInitialLotSetup.mockResolvedValue([]);
        apiMocks.loadOwnerLots.mockResolvedValue(undefined);
    });

    it("Tình huống 11: Chưa đặt ghim hoặc tọa độ (0,0) -> nút xác nhận vị trí bị khóa", async () => {
        render(<NewParkingLotPage />);

        const confirmBtn = screen.getByTestId("btn-confirm-entrance");
        // Khi chưa nhập địa chỉ và chưa ghim -> disabled
        expect(confirmBtn).toBeDisabled();

        // Nhập địa chỉ nhưng chưa ghim
        fireEvent.change(screen.getByTestId("picker-address"), {
            target: { value: "123 Lê Lợi, Quận 1" },
        });
        expect(confirmBtn).toBeDisabled();

        // Chọn tọa độ (0, 0)
        fireEvent.click(screen.getByTestId("simulate-zero-coords"));
        expect(confirmBtn).toBeDisabled();
    });

    it("Tình huống 5: Owner xác nhận ghim thành công -> lưu token và đánh dấu đã xác nhận", async () => {
        render(<NewParkingLotPage />);

        fireEvent.change(screen.getByTestId("picker-address"), {
            target: { value: "  123 Lê Lợi, Quận 1  " },
        });
        fireEvent.click(screen.getByTestId("simulate-place-pin"));

        const confirmBtn = screen.getByTestId("btn-confirm-entrance");
        expect(confirmBtn).toBeEnabled();

        fireEvent.click(confirmBtn);

        await waitFor(() => {
            expect(apiMocks.confirmOwnerLotEntrance).toHaveBeenCalledWith({
                address: "123 Lê Lợi, Quận 1", // Đã trim
                latitude: 10.7769,
                longitude: 106.7009,
                confirmedOnMap: true,
            });
            expect(
                screen.getByTestId("confirmed-token-badge"),
            ).toBeInTheDocument();
        });
    });

    it("Tình huống 6: Owner kéo ghim sau khi xác nhận -> xóa token cũ và yêu cầu xác nhận lại", async () => {
        render(<NewParkingLotPage />);

        fireEvent.change(screen.getByTestId("picker-address"), {
            target: { value: "123 Lê Lợi, Quận 1" },
        });
        fireEvent.click(screen.getByTestId("simulate-place-pin"));

        // Xác nhận vị trí lần đầu
        fireEvent.click(screen.getByTestId("btn-confirm-entrance"));
        await waitFor(() => {
            expect(
                screen.getByTestId("confirmed-token-badge"),
            ).toBeInTheDocument();
        });

        // Kéo ghim sang vị trí khác
        fireEvent.click(screen.getByTestId("simulate-move-pin"));

        // Token cũ bị xóa và badge xác nhận biến mất
        expect(screen.queryByTestId("confirmed-token-badge")).toBeNull();
    });

    it("Tình huống 7: Owner sửa địa chỉ sau khi xác nhận -> xóa token cũ và yêu cầu xác nhận lại", async () => {
        render(<NewParkingLotPage />);

        fireEvent.change(screen.getByTestId("picker-address"), {
            target: { value: "123 Lê Lợi, Quận 1" },
        });
        fireEvent.click(screen.getByTestId("simulate-place-pin"));

        // Xác nhận vị trí
        fireEvent.click(screen.getByTestId("btn-confirm-entrance"));
        await waitFor(() => {
            expect(
                screen.getByTestId("confirmed-token-badge"),
            ).toBeInTheDocument();
        });

        // Sửa địa chỉ
        fireEvent.change(screen.getByTestId("picker-address"), {
            target: { value: "456 Nguyễn Huệ, Quận 1" },
        });

        // Token cũ bị xóa và badge biến mất
        expect(screen.queryByTestId("confirmed-token-badge")).toBeNull();
    });

    it("Tình huống 8: Token hết hạn -> yêu cầu xác nhận lại trước khi gửi đăng ký", async () => {
        // Token trả về đã hết hạn
        apiMocks.confirmOwnerLotEntrance.mockResolvedValueOnce({
            locationConfirmationToken: "expired-token",
            expiresAtUtc: new Date(Date.now() - 60000).toISOString(),
        });

        render(<NewParkingLotPage />);

        const nameInput = screen
            .getByText("Tên bãi")
            .closest("label")!
            .querySelector("input")!;
        fireEvent.change(nameInput, { target: { value: "Bãi Đỗ Trung Tâm" } });

        fireEvent.change(screen.getByTestId("picker-address"), {
            target: { value: "123 Lê Lợi, Quận 1" },
        });
        fireEvent.click(screen.getByTestId("simulate-place-pin"));
        fireEvent.click(screen.getByTestId("btn-confirm-entrance"));

        await waitFor(() => {
            expect(apiMocks.confirmOwnerLotEntrance).toHaveBeenCalledTimes(1);
        });

        // Bấm tiếp tục sang bước 1
        fireEvent.click(screen.getByRole("button", { name: /Tiếp tục/i }));

        // Báo lỗi token hết hạn
        expect(
            await screen.findByText(/Xác nhận vị trí cổng vào đã hết hạn/i),
        ).toBeInTheDocument();
        expect(apiMocks.registerParkingLotApi).not.toHaveBeenCalled();
    });

    it("Tình huống 12: Backend trả 400 token không khớp khi đăng ký -> giữ dữ liệu, xóa token và yêu cầu xác nhận lại", async () => {
        apiMocks.registerParkingLotApi.mockRejectedValueOnce(
            new ApiError(400, {
                title: "Validation Problem",
                status: 400,
                errors: {
                    locationConfirmationToken: [
                        "A valid, unexpired confirmation is required.",
                    ],
                },
            } as unknown as Record<string, unknown>),
        );

        render(<NewParkingLotPage />);

        const nameInput = screen
            .getByText("Tên bãi")
            .closest("label")!
            .querySelector("input")!;
        fireEvent.change(nameInput, { target: { value: "Bãi Đỗ Trung Tâm" } });

        fireEvent.change(screen.getByTestId("picker-address"), {
            target: { value: "123 Lê Lợi, Quận 1" },
        });
        fireEvent.click(screen.getByTestId("simulate-place-pin"));
        fireEvent.click(screen.getByTestId("btn-confirm-entrance"));

        await waitFor(() => {
            expect(
                screen.getByTestId("confirmed-token-badge"),
            ).toBeInTheDocument();
        });

        // Bước 0 -> Bước 1 -> Bước 2
        fireEvent.click(screen.getByRole("button", { name: /Tiếp tục/i }));
        fireEvent.click(screen.getByRole("button", { name: /Tiếp tục/i }));

        // Gửi duyệt
        fireEvent.click(screen.getByRole("button", { name: /Gửi duyệt/i }));
        fireEvent.click(screen.getByRole("button", { name: /Xác nhận gửi/i }));

        await waitFor(() => {
            expect(apiMocks.registerParkingLotApi).toHaveBeenCalledTimes(1);
            expect(
                screen.getByText(/Xác nhận vị trí không khớp hoặc đã hết hạn/i),
            ).toBeInTheDocument();
            // Dữ liệu form vẫn được giữ
            expect(nameInput).toHaveValue("Bãi Đỗ Trung Tâm");
            // Token bị xóa
            expect(screen.queryByTestId("confirmed-token-badge")).toBeNull();
        });
    });

    it("Tình huống 14: Double-submit prevention -> chống gửi nhiều lần khi đang xử lý", async () => {
        let resolveRegister!: (val: unknown) => void;
        apiMocks.registerParkingLotApi.mockImplementation(
            () =>
                new Promise((resolve) => {
                    resolveRegister = resolve;
                }),
        );

        render(<NewParkingLotPage />);

        const nameInput = screen
            .getByText("Tên bãi")
            .closest("label")!
            .querySelector("input")!;
        fireEvent.change(nameInput, { target: { value: "Bãi Đỗ Trung Tâm" } });

        fireEvent.change(screen.getByTestId("picker-address"), {
            target: { value: "123 Lê Lợi, Quận 1" },
        });
        fireEvent.click(screen.getByTestId("simulate-place-pin"));
        fireEvent.click(screen.getByTestId("btn-confirm-entrance"));

        await waitFor(() => {
            expect(
                screen.getByTestId("confirmed-token-badge"),
            ).toBeInTheDocument();
        });

        fireEvent.click(screen.getByRole("button", { name: /Tiếp tục/i }));
        fireEvent.click(screen.getByRole("button", { name: /Tiếp tục/i }));

        fireEvent.click(screen.getByRole("button", { name: /Gửi duyệt/i }));
        const sendBtn = screen.getByRole("button", { name: /Xác nhận gửi/i });

        // Nhấn nhiều lần liên tiếp
        fireEvent.click(sendBtn);
        fireEvent.click(sendBtn);
        fireEvent.click(sendBtn);

        expect(apiMocks.registerParkingLotApi).toHaveBeenCalledTimes(1);

        resolveRegister({
            id: "lot-uuid-1",
            name: "Bãi Đỗ Trung Tâm",
            address: "123 Lê Lợi, Quận 1",
            latitude: 10.7769,
            longitude: 106.7009,
            type: "MultiStorey",
            approvalStatus: "Pending",
        });

        await waitFor(() => {
            expect(apiMocks.loadOwnerLots).toHaveBeenCalledTimes(1);
        });
    });
});
