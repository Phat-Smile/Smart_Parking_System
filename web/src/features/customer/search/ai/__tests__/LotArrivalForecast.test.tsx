import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import {
    LotArrivalForecast,
    type HourlyForecastSlot,
} from "../LotArrivalForecast";

const mockForecastSlots: HourlyForecastSlot[] = [
    { hour: 8, label: "08:00", probabilityPercent: 85, isPeak: false },
    { hour: 9, label: "09:00", probabilityPercent: 45, isPeak: true },
    { hour: 10, label: "10:00", probabilityPercent: 25, isPeak: true },
    { hour: 11, label: "11:00", probabilityPercent: 70, isPeak: false },
];

describe("LotArrivalForecast", () => {
    it("renders visual chart with bars, percentages, hour labels, peak indicators, and legend", () => {
        render(
            <LotArrivalForecast
                lotName="Bãi đỗ xe Bến Thành"
                selectedHour={9}
                forecastData={mockForecastSlots}
            />,
        );

        expect(
            screen.getByRole("region", {
                name: /Biểu đồ dự báo khả năng còn chỗ theo giờ - Bãi đỗ xe Bến Thành/i,
            }),
        ).toBeInTheDocument();
        expect(screen.getByText("Bãi đỗ xe Bến Thành")).toBeInTheDocument();

        // Hours rendered
        expect(screen.getByText("08:00")).toBeInTheDocument();
        expect(screen.getByText("09:00")).toBeInTheDocument();
        expect(screen.getByText("10:00")).toBeInTheDocument();
        expect(screen.getByText("11:00")).toBeInTheDocument();

        // Percentages rendered
        expect(screen.getByText("85%")).toBeInTheDocument();
        expect(screen.getByText("45%")).toBeInTheDocument();
        expect(screen.getByText("25%")).toBeInTheDocument();
        expect(screen.getByText("70%")).toBeInTheDocument();

        // Legend items rendered
        expect(screen.getByText(/Dễ còn chỗ \(≥ 60%\)/i)).toBeInTheDocument();
        expect(
            screen.getByText(/Chỗ hạn chế \(30% - 59%\)/i),
        ).toBeInTheDocument();
        expect(
            screen.getByText(/Nguy cơ hết chỗ \(< 30%\)/i),
        ).toBeInTheDocument();
        expect(screen.getByText("Giờ cao điểm")).toBeInTheDocument();
    });

    it("triggers onSelectHour when an hour button is clicked", async () => {
        const user = userEvent.setup();
        const onSelectHour = vi.fn();

        render(
            <LotArrivalForecast
                forecastData={mockForecastSlots}
                onSelectHour={onSelectHour}
            />,
        );

        const hour10Button = screen.getByRole("button", {
            name: /Khung giờ 10:00: khả năng còn chỗ 25%/i,
        });
        await user.click(hour10Button);

        expect(onSelectHour).toHaveBeenCalledWith(10);
    });

    it("renders empty state when forecastData is empty", () => {
        render(<LotArrivalForecast forecastData={[]} />);

        expect(
            screen.getByText("Chưa có dữ liệu dự báo cho bãi đỗ xe này."),
        ).toBeInTheDocument();
    });

    it("renders error state when isError is true", () => {
        render(
            <LotArrivalForecast
                isError={true}
                errorMessage="Lỗi kết nối máy chủ AI dự báo."
            />,
        );

        expect(screen.getByRole("alert")).toBeInTheDocument();
        expect(screen.getByText("Lỗi tải dữ liệu")).toBeInTheDocument();
        expect(
            screen.getByText("Lỗi kết nối máy chủ AI dự báo."),
        ).toBeInTheDocument();
    });

    it("renders loading skeleton state when isLoading is true", () => {
        render(<LotArrivalForecast isLoading={true} />);

        const skeleton = screen.getByRole("status");
        expect(skeleton).toBeInTheDocument();
        expect(skeleton).toHaveAttribute("aria-busy", "true");
    });

    it("renders 'Chưa đủ dữ liệu' label when slot probabilityPercent is null or insufficientData is true without converting to 0%", () => {
        const slotsWithNullProb: HourlyForecastSlot[] = [
            {
                hour: 8,
                label: "08:00",
                probabilityPercent: null,
                isPeak: false,
            },
            {
                hour: 9,
                label: "09:00",
                probabilityPercent: 50,
                insufficientData: true,
                isPeak: false,
            },
            { hour: 10, label: "10:00", probabilityPercent: 80, isPeak: false },
        ];

        render(
            <LotArrivalForecast
                lotName="Bãi đỗ xe Bến Thành"
                forecastData={slotsWithNullProb}
            />,
        );

        const insufficientLabels = screen.getAllByText("Chưa đủ dữ liệu");
        expect(insufficientLabels).toHaveLength(2);
        expect(screen.queryByText("0%")).not.toBeInTheDocument();
        expect(screen.getByText("80%")).toBeInTheDocument();
    });

    it("displays 'Chưa đủ dữ liệu' badge and message when insufficientData prop is true", () => {
        render(
            <LotArrivalForecast
                lotName="Bãi đỗ xe Bến Thành"
                insufficientData={true}
                forecastData={[]}
            />,
        );

        expect(screen.getByText("Chưa đủ dữ liệu")).toBeInTheDocument();
        expect(
            screen.getByText("Chưa đủ dữ liệu dự báo cho bãi đỗ xe này."),
        ).toBeInTheDocument();
    });
});
