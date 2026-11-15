import React from "react";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { OwnerLocationPicker } from "./OwnerLocationPicker";
import { ApiError } from "@/shared/errors/api-error";

const { apiMocks } = vi.hoisted(() => ({
    apiMocks: {
        resolveOwnerLotAddress: vi.fn(),
    },
}));

vi.mock("../api/ownerApi", async (importOriginal) => {
    const actual = await importOriginal<typeof import("../api/ownerApi")>();
    return {
        ...actual,
        resolveOwnerLotAddress: apiMocks.resolveOwnerLotAddress,
    };
});

vi.mock("@/shared/config/env", () => ({
    getPublicMapboxToken: () => "pk.test-mapbox-token",
}));

vi.mock("react-map-gl/mapbox", () => ({
    default: (props: any) => (
        <div data-testid="mock-react-map">{props.children}</div>
    ),
    Marker: (props: any) => (
        <div data-testid="mock-marker">{props.children}</div>
    ),
    NavigationControl: () => <div data-testid="mock-nav-control" />,
}));

describe("OwnerLocationPicker component (WP-OW-1)", () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it("Bấm Tìm gọi resolve một lần; bấm liên tục khi đang gọi không gọi thêm", async () => {
        let resolvePromise: (val: any) => void;
        apiMocks.resolveOwnerLotAddress.mockImplementation(
            () =>
                new Promise((resolve) => {
                    resolvePromise = resolve;
                }),
        );

        const onChange = vi.fn();
        render(
            <OwnerLocationPicker
                address="123 Lê Lợi, Quận 1"
                latitude={null}
                longitude={null}
                confirmedOnMap={false}
                onChange={onChange}
            />,
        );

        const searchBtn = screen.getByTestId("owner-lot-search-btn");
        expect(searchBtn).toBeEnabled();

        // Bấm lần 1
        fireEvent.click(searchBtn);
        // Bấm tiếp khi đang gọi
        fireEvent.click(searchBtn);
        fireEvent.click(searchBtn);

        expect(apiMocks.resolveOwnerLotAddress).toHaveBeenCalledTimes(1);
        expect(apiMocks.resolveOwnerLotAddress).toHaveBeenCalledWith(
            "123 Lê Lợi, Quận 1",
        );

        // Nút bị khóa trong lúc gọi
        expect(searchBtn).toBeDisabled();

        // Hoàn tất promise
        resolvePromise!({
            address: "123 Lê Lợi, Quận 1",
            candidates: [
                {
                    displayName: "123 Lê Lợi, Bến Nghé, Quận 1",
                    latitude: 10.7769,
                    longitude: 106.7009,
                },
            ],
            manualPinAllowed: true,
        });

        await waitFor(() => expect(searchBtn).toBeEnabled());
        // Sau khi resolve thành công, CHỈ dời tâm bản đồ, KHÔNG tự cắm ghim và KHÔNG tự xác nhận (Tình huống 1)
        expect(onChange).not.toHaveBeenCalled();
        const status = screen.getByTestId("owner-location-status");
        expect(status).toHaveTextContent(
            "Đã đưa bản đồ đến khu vực gợi ý gần đúng. Vui lòng chạm bản đồ hoặc kéo ghim để xác định chính xác CỔNG VÀO bãi xe.",
        );
    });

    it("Lỗi 429 và 503 hiện thông báo tương ứng, vẫn đặt ghim tay được", async () => {
        // 1. Thử lỗi 429
        apiMocks.resolveOwnerLotAddress.mockRejectedValueOnce(
            new ApiError(429, {
                title: "Too Many Requests",
                status: 429,
                detail: "Rate limit exceeded",
            }),
        );

        const onChange = vi.fn();
        const { rerender } = render(
            <OwnerLocationPicker
                address="456 Hai Bà Trưng"
                latitude={null}
                longitude={null}
                confirmedOnMap={false}
                onChange={onChange}
            />,
        );

        const searchBtn = screen.getByTestId("owner-lot-search-btn");
        fireEvent.click(searchBtn);

        await waitFor(() => {
            const status = screen.getByTestId("owner-location-status");
            expect(status).toHaveTextContent(
                "Quá giới hạn tìm kiếm địa chỉ. Vui lòng thử lại sau hoặc tự ghim vị trí trên bản đồ.",
            );
        });

        // 2. Thử lỗi 503
        apiMocks.resolveOwnerLotAddress.mockRejectedValueOnce(
            new ApiError(503, {
                title: "Service Unavailable",
                status: 503,
                detail: "Nominatim down",
            }),
        );

        rerender(
            <OwnerLocationPicker
                address="789 Điện Biên Phủ"
                latitude={null}
                longitude={null}
                confirmedOnMap={false}
                onChange={onChange}
            />,
        );

        fireEvent.click(searchBtn);

        await waitFor(() => {
            const status = screen.getByTestId("owner-location-status");
            expect(status).toHaveTextContent(
                "Dịch vụ tìm kiếm địa chỉ tạm thời không khả dụng. Bạn vẫn có thể tự ghim vị trí trên bản đồ.",
            );
        });
    });

    it("Kết quả resolve chỉ dời tâm bản đồ, không tự ý ghim hay ghi đè địa chỉ người dùng đã gõ", async () => {
        apiMocks.resolveOwnerLotAddress.mockResolvedValueOnce({
            address: "Địa chỉ OSM chuẩn",
            candidates: [
                {
                    displayName: "Địa chỉ OSM chuẩn đầy đủ",
                    latitude: 10.78,
                    longitude: 106.69,
                },
            ],
            manualPinAllowed: true,
        });

        const onChange = vi.fn();
        render(
            <OwnerLocationPicker
                address="Địa chỉ chủ bãi tự gõ"
                latitude={null}
                longitude={null}
                confirmedOnMap={false}
                onChange={onChange}
            />,
        );

        fireEvent.click(screen.getByTestId("owner-lot-search-btn"));

        await waitFor(() => {
            // Không tự động ghim vị trí hay tự xác nhận
            expect(onChange).not.toHaveBeenCalled();
            expect(
                screen.getByTestId("owner-location-status"),
            ).toHaveTextContent(
                "Đã đưa bản đồ đến khu vực gợi ý gần đúng. Vui lòng chạm bản đồ hoặc kéo ghim để xác định chính xác CỔNG VÀO bãi xe.",
            );
        });
    });
});
