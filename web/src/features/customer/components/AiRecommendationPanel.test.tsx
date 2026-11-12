import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { RecommendedParkingLot } from "../parkingLotApi";
import { AiRecommendationPanel } from "./AiRecommendationPanel";
import { probabilityTone } from "./ProbabilityRing";

const copy = (english: string) => english;

const rec = (
    overrides: Partial<RecommendedParkingLot>,
): RecommendedParkingLot => ({
    lotId: "lot-1",
    name: "Bãi đỗ Ngoài trời B",
    address: "02 Đường Mẫu",
    latitude: 10.77,
    longitude: 106.7,
    rank: 1,
    driveTimeMinutes: 3,
    probabilityPercent: 82,
    price: 15000,
    warningHighFullRisk: false,
    reasons: ["Gần nhất", "Giá thấp"],
    distanceKm: 0.24,
    ...overrides,
});

describe("probabilityTone", () => {
    it("follows the BR-08 thresholds", () => {
        expect(probabilityTone(82)).toBe("good");
        expect(probabilityTone(60)).toBe("good");
        expect(probabilityTone(59)).toBe("warn");
        expect(probabilityTone(30)).toBe("warn");
        expect(probabilityTone(29)).toBe("risk");
    });
});

describe("AiRecommendationPanel", () => {
    it("shows the top pick with reasons and key numbers", () => {
        render(
            <AiRecommendationPanel
                recommendations={[rec({})]}
                activeId=""
                onSelect={() => {}}
                copy={copy}
            />,
        );
        expect(screen.getByText("Bãi đỗ Ngoài trời B")).toBeInTheDocument();
        expect(screen.getByText("Gần nhất")).toBeInTheDocument();
        expect(screen.getByText("82%")).toBeInTheDocument();
        expect(screen.getByText("3 min drive")).toBeInTheDocument();
        expect(screen.getByText("Top pick")).toBeInTheDocument();
        expect(screen.queryByText("Other options")).not.toBeInTheDocument();
    });

    it("lists the other options with a probability ring and selects on click", () => {
        const onSelect = vi.fn();
        render(
            <AiRecommendationPanel
                recommendations={[
                    rec({}),
                    rec({
                        lotId: "lot-2",
                        name: "Bãi đỗ Tầng hầm C",
                        rank: 2,
                        probabilityPercent: 31,
                        driveTimeMinutes: null,
                        distanceKm: 0.41,
                    }),
                ]}
                activeId="lot-2"
                onSelect={onSelect}
                copy={copy}
            />,
        );
        expect(screen.getByText("Other options")).toBeInTheDocument();
        expect(
            screen.getByRole("img", { name: "31% chance of a free space" }),
        ).toBeInTheDocument();
        expect(screen.getByText("0.41 km")).toBeInTheDocument();

        const row = screen.getByRole("button", { name: /Bãi đỗ Tầng hầm C/ });
        expect(row).toHaveClass("active");
        fireEvent.click(row);
        expect(onSelect).toHaveBeenCalledWith("lot-2");
    });

    it("warns when the top pick may be full", () => {
        render(
            <AiRecommendationPanel
                recommendations={[rec({ warningHighFullRisk: true })]}
                activeId=""
                onSelect={() => {}}
                copy={copy}
            />,
        );
        expect(screen.getByText("May be full")).toBeInTheDocument();
    });

    it("renders nothing without recommendations", () => {
        const { container } = render(
            <AiRecommendationPanel
                recommendations={[]}
                activeId=""
                onSelect={() => {}}
                copy={copy}
            />,
        );
        expect(container).toBeEmptyDOMElement();
    });
});
