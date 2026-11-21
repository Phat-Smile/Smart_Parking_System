import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AiRecommendationPanel } from "../AiRecommendationPanel";
import type { RecommendedParkingLot } from "@/features/customer/parkingLotApi";

const mockRecommendations: RecommendedParkingLot[] = [
    {
        lotId: "lot-1",
        name: "Bãi đỗ xe Nguyễn Huệ",
        address: "68 Nguyễn Huệ, Quận 1",
        latitude: 10.776,
        longitude: 106.701,
        rank: 1,
        driveTimeMinutes: 4,
        probabilityPercent: 88,
        price: 25000,
        warningHighFullRisk: false,
        reasons: [
            "Gần bạn nhất (4 phút)",
            "Tỷ lệ còn chỗ rất cao",
            "Đỗ xe tầng hầm",
        ],
        distanceKm: 0.5,
    },
    {
        lotId: "lot-2",
        name: "Bãi đỗ xe Lê Lợi",
        address: "22 Lê Lợi, Quận 1",
        latitude: 10.774,
        longitude: 106.699,
        rank: 2,
        driveTimeMinutes: 8,
        probabilityPercent: 25,
        price: 20000,
        warningHighFullRisk: true,
        reasons: ["Mức giá tiết kiệm nhất", "Thuận tiện di chuyển"],
        distanceKm: 0.8,
    },
];

describe("AiRecommendationPanel", () => {
    describe("State 1: Empty State (No destination selected)", () => {
        it("renders friendly guidance explaining AI will recommend best lots once destination chosen", () => {
            render(<AiRecommendationPanel state="empty" />);

            expect(
                screen.getByRole("heading", {
                    name: /Gợi ý bãi đỗ thông minh từ AI/i,
                }),
            ).toBeInTheDocument();
            expect(
                screen.getByText(/Chọn điểm đến để nhận gợi ý tối ưu/i),
            ).toBeInTheDocument();
            expect(
                screen.getByText(
                    /Vui lòng nhập hoặc chọn điểm đến trên bản đồ/i,
                ),
            ).toBeInTheDocument();
            expect(screen.getByTestId("ai-state-empty")).toBeInTheDocument();
        });

        it("automatically resolves to empty state when hasDestination is false", () => {
            render(<AiRecommendationPanel hasDestination={false} />);
            expect(screen.getByTestId("ai-state-empty")).toBeInTheDocument();
        });
    });

    describe("State 2: Loading State (Shimmer Skeleton)", () => {
        it("renders polished skeleton loading state with aria-busy and status role", () => {
            render(<AiRecommendationPanel state="loading" />);

            const loadingContainer = screen.getByTestId("ai-state-loading");
            expect(loadingContainer).toBeInTheDocument();
            expect(loadingContainer).toHaveAttribute("aria-busy", "true");
            expect(loadingContainer).toHaveAttribute("role", "status");
        });

        it("automatically resolves to loading state when isLoading is true", () => {
            render(
                <AiRecommendationPanel
                    isLoading={true}
                    hasDestination={true}
                />,
            );
            expect(screen.getByTestId("ai-state-loading")).toBeInTheDocument();
        });
    });

    describe("State 3: Error State (Graceful Fallback Alert)", () => {
        it("renders graceful fallback alert with exact required message", () => {
            render(<AiRecommendationPanel state="error" />);

            expect(screen.getByTestId("ai-state-error")).toBeInTheDocument();
            expect(
                screen.getByText(
                    "Tạm thời không có gợi ý, danh sách vẫn đầy đủ",
                ),
            ).toBeInTheDocument();
            expect(
                screen.getByText(/Hệ thống AI đang bận hoặc gặp sự cố/i),
            ).toBeInTheDocument();
        });

        it("calls onRetry callback when retry button is clicked", async () => {
            const user = userEvent.setup();
            const onRetry = vi.fn();

            render(<AiRecommendationPanel isError={true} onRetry={onRetry} />);

            const retryBtn = screen.getByRole("button", {
                name: /Thử lại gợi ý AI/i,
            });
            await user.click(retryBtn);

            expect(onRetry).toHaveBeenCalledTimes(1);
        });
    });

    describe("State 4: Success State (Ranked Recommendation Cards)", () => {
        it("renders list of ranked recommendation cards with badges, metrics, reason tags, and CTA buttons", () => {
            render(
                <AiRecommendationPanel
                    state="success"
                    recommendations={mockRecommendations}
                />,
            );

            expect(screen.getByTestId("ai-state-success")).toBeInTheDocument();

            // Rank #1 card
            expect(
                screen.getByText("Bãi đỗ xe Nguyễn Huệ"),
            ).toBeInTheDocument();
            expect(
                screen.getByText("68 Nguyễn Huệ, Quận 1"),
            ).toBeInTheDocument();
            expect(
                screen.getByText(/Hạng #1 · Tối ưu nhất/i),
            ).toBeInTheDocument();
            expect(screen.getByText("25.000 đ")).toBeInTheDocument();
            expect(screen.getByText("88%")).toBeInTheDocument();
            expect(
                screen.getByText("Gần bạn nhất (4 phút)"),
            ).toBeInTheDocument();
            expect(
                screen.getByText("Tỷ lệ còn chỗ rất cao"),
            ).toBeInTheDocument();

            // Rank #2 card with high risk warning (< 30%)
            expect(screen.getByText("Bãi đỗ xe Lê Lợi")).toBeInTheDocument();
            expect(screen.getByText(/Hạng #2/i)).toBeInTheDocument();
            expect(screen.getByText("Nguy cơ hết chỗ cao")).toBeInTheDocument();
            expect(screen.getByText("25%")).toBeInTheDocument();

            // CTA buttons
            const mapButtons = screen.getAllByRole("button", {
                name: /Xem.*trên bản đồ/i,
            });
            const bookButtons = screen.getAllByRole("button", {
                name: /Đặt chỗ tại/i,
            });
            expect(mapButtons).toHaveLength(2);
            expect(bookButtons).toHaveLength(2);
        });

        it("fires onSelectOnMap and onBook when CTA buttons are clicked", async () => {
            const user = userEvent.setup();
            const onSelectOnMap = vi.fn();
            const onBook = vi.fn();

            render(
                <AiRecommendationPanel
                    state="success"
                    recommendations={mockRecommendations}
                    onSelectOnMap={onSelectOnMap}
                    onBook={onBook}
                />,
            );

            const mapBtn = screen.getByRole("button", {
                name: /Xem bãi Bãi đỗ xe Nguyễn Huệ trên bản đồ/i,
            });
            await user.click(mapBtn);
            expect(onSelectOnMap).toHaveBeenCalledWith(mockRecommendations[0]);

            const bookBtn = screen.getByRole("button", {
                name: /Đặt chỗ tại bãi Bãi đỗ xe Nguyễn Huệ/i,
            });
            await user.click(bookBtn);
            expect(onBook).toHaveBeenCalledWith(mockRecommendations[0]);
        });
    });

    describe("Transparent AI Score Explanation Modal", () => {
        it("opens explanation modal and displays 40% distance, 40% pricing, 20% availability probability", async () => {
            const user = userEvent.setup();
            render(
                <AiRecommendationPanel
                    state="success"
                    recommendations={mockRecommendations}
                />,
            );

            const explainBtn = screen.getByRole("button", {
                name: /Xem giải thích cách AI tính điểm và xếp hạng/i,
            });
            await user.click(explainBtn);

            // Modal dialog should now be visible
            const dialog = screen.getByRole("dialog");
            expect(dialog).toBeInTheDocument();
            const fortyPercentBadges = screen.getAllByText(/40% tổng điểm/i);
            expect(fortyPercentBadges).toHaveLength(2);
            expect(screen.getByText(/20% tổng điểm/i)).toBeInTheDocument();
            expect(
                screen.getByText(/Khoảng cách & thời gian di chuyển/i),
            ).toBeInTheDocument();
            expect(screen.getByText(/Mức giá đỗ xe/i)).toBeInTheDocument();
            expect(
                screen.getByText(/Khả năng còn chỗ lúc tới/i),
            ).toBeInTheDocument();
            expect(
                screen.getByText(/Quy tắc an toàn chống rủi ro hết chỗ/i),
            ).toBeInTheDocument();

            // Close dialog
            const closeBtn = screen.getByRole("button", { name: /Đã hiểu/i });
            await user.click(closeBtn);
            expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
        });
    });

    describe("Fallback Mode (fallbackUsed === true)", () => {
        it("renders heading 'Danh sách xếp theo khoảng cách' and explanation when fallbackUsed is true", () => {
            render(
                <AiRecommendationPanel
                    state="success"
                    fallbackUsed={true}
                    recommendations={mockRecommendations}
                />,
            );

            // Must NOT display as AI recommendation
            expect(
                screen.queryByRole("heading", {
                    name: /Gợi ý bãi đỗ thông minh từ AI/i,
                }),
            ).not.toBeInTheDocument();
            expect(screen.queryByText("Mô hình BR-08")).not.toBeInTheDocument();
            expect(
                screen.queryByRole("button", { name: /Vì sao gợi ý này\?/i }),
            ).not.toBeInTheDocument();

            // Must display heading "Danh sách xếp theo khoảng cách"
            expect(
                screen.getByRole("heading", {
                    name: "Danh sách xếp theo khoảng cách",
                }),
            ).toBeInTheDocument();

            // Must display friendly explanation
            expect(
                screen.getByText(
                    /Hệ thống đã tự động sắp xếp các bãi đỗ theo khoảng cách vì tiêu chí gợi ý AI chưa thể áp dụng đầy đủ/i,
                ),
            ).toBeInTheDocument();

            // Must display badge "Theo khoảng cách"
            expect(screen.getByText("Theo khoảng cách")).toBeInTheDocument();

            // Still renders lots
            expect(
                screen.getByText("Bãi đỗ xe Nguyễn Huệ"),
            ).toBeInTheDocument();
            expect(screen.getByText(/Hạng #1 · Gần nhất/i)).toBeInTheDocument();
        });
    });

    describe("Insufficient Data Handling (probabilityPercent is null or insufficientData === true)", () => {
        it("displays 'Chưa đủ dữ liệu' when probabilityPercent is null without converting to 0%", () => {
            const recommendationsWithNullProb: RecommendedParkingLot[] = [
                {
                    ...mockRecommendations[0],
                    probabilityPercent: null,
                    insufficientData: true,
                },
            ];

            render(
                <AiRecommendationPanel
                    state="success"
                    recommendations={recommendationsWithNullProb}
                />,
            );

            expect(screen.getByText("Chưa đủ dữ liệu")).toBeInTheDocument();
            expect(screen.queryByText("0%")).not.toBeInTheDocument();
            expect(screen.queryByText("Chưa có")).not.toBeInTheDocument();
        });

        it("displays 'Chưa đủ dữ liệu' when insufficientData is true even if probabilityPercent is present", () => {
            const recommendationsWithInsufficientFlag: RecommendedParkingLot[] =
                [
                    {
                        ...mockRecommendations[0],
                        probabilityPercent: 50,
                        insufficientData: true,
                    },
                ];

            render(
                <AiRecommendationPanel
                    state="success"
                    recommendations={recommendationsWithInsufficientFlag}
                />,
            );

            expect(screen.getByText("Chưa đủ dữ liệu")).toBeInTheDocument();
            expect(screen.queryByText("50%")).not.toBeInTheDocument();
        });
    });
});
