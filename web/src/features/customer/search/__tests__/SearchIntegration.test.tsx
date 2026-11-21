import { render, screen, fireEvent } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { CustomerSearchForm } from "../CustomerSearchForm";
import { CustomerAiRecommendations } from "../CustomerAiRecommendations";
import type { RecommendedParkingLot } from "@/features/customer/parkingLotApi";

describe("PR F & G Integration in Customer Search", () => {
    describe("CustomerSearchForm Voice Search Integration", () => {
        it("renders without error and hides voice button when SpeechRecognition is not supported", () => {
            const onDestinationInputChange = vi.fn();
            render(
                <CustomerSearchForm
                    destination=""
                    destinationInputRef={{ current: null }}
                    suggestions={[]}
                    suggestionsOpen={false}
                    setSuggestionsOpen={vi.fn()}
                    suggestionsBusy={false}
                    suggestionsError={false}
                    activeSuggestionIndex={-1}
                    setActiveSuggestionIndex={vi.fn()}
                    onDestinationInputChange={onDestinationInputChange}
                    onDestinationKeyDown={vi.fn()}
                    selectSuggestion={vi.fn()}
                    useCurrentLocation={vi.fn()}
                    locationBusy={false}
                    distanceChoice="any"
                    onDistanceChoiceChange={vi.fn()}
                    distanceNeedsOrigin={false}
                    distanceLabels={{}}
                    searchOrigin={null}
                    filters={{
                        distance: "any",
                        evChargingOnly: false,
                        sort: "recommended",
                    }}
                    setFilters={vi.fn()}
                    vehicleType="Car"
                    setVehicleType={vi.fn()}
                    searchBusy={false}
                    handleSearch={vi.fn()}
                    copy={(en) => en}
                />,
            );

            expect(
                screen.getByRole("combobox", { name: "Destination" }),
            ).toBeInTheDocument();
            // Voice button is hidden by default when unsupported in jsdom
            expect(
                screen.queryByTestId("voice-search-button"),
            ).not.toBeInTheDocument();
        });

        it("renders voice search button when Web Speech API is present and triggers input change on speech", () => {
            class MockSpeechRecognition {
                continuous = false;
                interimResults = false;
                lang = "vi-VN";
                onstart: (() => void) | null = null;
                onresult: ((event: unknown) => void) | null = null;
                onerror: ((event: unknown) => void) | null = null;
                onend: (() => void) | null = null;
                start() {
                    this.onstart?.();
                    this.onresult?.({
                        results: [[{ transcript: "Bitexco Financial Tower" }]],
                    });
                    this.onend?.();
                }
                stop() {
                    this.onend?.();
                }
                abort() {
                    this.onend?.();
                }
            }

            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            (window as any).SpeechRecognition = MockSpeechRecognition;

            const onDestinationInputChange = vi.fn();
            render(
                <CustomerSearchForm
                    destination=""
                    destinationInputRef={{ current: null }}
                    suggestions={[]}
                    suggestionsOpen={false}
                    setSuggestionsOpen={vi.fn()}
                    suggestionsBusy={false}
                    suggestionsError={false}
                    activeSuggestionIndex={-1}
                    setActiveSuggestionIndex={vi.fn()}
                    onDestinationInputChange={onDestinationInputChange}
                    onDestinationKeyDown={vi.fn()}
                    selectSuggestion={vi.fn()}
                    useCurrentLocation={vi.fn()}
                    locationBusy={false}
                    distanceChoice="any"
                    onDistanceChoiceChange={vi.fn()}
                    distanceNeedsOrigin={false}
                    distanceLabels={{}}
                    searchOrigin={null}
                    filters={{
                        distance: "any",
                        evChargingOnly: false,
                        sort: "recommended",
                    }}
                    setFilters={vi.fn()}
                    vehicleType="Car"
                    setVehicleType={vi.fn()}
                    searchBusy={false}
                    handleSearch={vi.fn()}
                    copy={(en) => en}
                />,
            );

            const voiceBtn = screen.getByTestId("voice-search-button");
            expect(voiceBtn).toBeInTheDocument();

            fireEvent.click(voiceBtn);
            expect(onDestinationInputChange).toHaveBeenCalledWith(
                "Bitexco Financial Tower",
            );

            // Cleanup
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            delete (window as any).SpeechRecognition;
        });
    });

    describe("CustomerAiRecommendations 4-State Panel Integration", () => {
        const mockRecommendation: RecommendedParkingLot = {
            lotId: "lot-101",
            name: "Bãi xe Bitexco Tower",
            address: "2 Hải Triều, Q.1, TP.HCM",
            latitude: 10.7718,
            longitude: 106.7044,
            rank: 1,
            driveTimeMinutes: 8,
            probabilityPercent: 88,
            price: 25000,
            warningHighFullRisk: false,
            reasons: [
                "Gần điểm đến nhất (0.3 km)",
                "Khả năng còn chỗ cao (88%)",
            ],
            distanceKm: 0.3,
        };

        it("renders empty state guidance when no destination has been set", () => {
            render(
                <CustomerAiRecommendations
                    hasDestination={false}
                    isLoading={false}
                    copy={(en) => en}
                />,
            );
            expect(screen.getByTestId("ai-state-empty")).toBeInTheDocument();
            expect(
                screen.getByText("Chọn điểm đến để nhận gợi ý tối ưu"),
            ).toBeInTheDocument();
        });

        it("renders loading state skeleton shimmer when search is busy", () => {
            render(
                <CustomerAiRecommendations
                    isLoading={true}
                    hasDestination={true}
                    copy={(en) => en}
                />,
            );
            expect(screen.getByTestId("ai-state-loading")).toBeInTheDocument();
        });

        it("renders error state with retry button when search fails", () => {
            const onRetry = vi.fn();
            render(
                <CustomerAiRecommendations
                    isError={true}
                    errorMessage="Không thể kết nối dịch vụ AI"
                    onRetry={onRetry}
                    copy={(en) => en}
                />,
            );
            expect(screen.getByTestId("ai-state-error")).toBeInTheDocument();
            expect(
                screen.getByText("Không thể kết nối dịch vụ AI"),
            ).toBeInTheDocument();

            const retryBtn = screen.getByRole("button", {
                name: /thử lại gợi ý ai/i,
            });
            fireEvent.click(retryBtn);
            expect(onRetry).toHaveBeenCalledTimes(1);
        });

        it("renders success state with recommended cards and dual action buttons", () => {
            const onSelect = vi.fn();
            render(
                <CustomerAiRecommendations
                    hasDestination={true}
                    recommendations={[mockRecommendation]}
                    activeId="lot-101"
                    onSelect={onSelect}
                    copy={(en) => en}
                />,
            );
            expect(screen.getByTestId("ai-state-success")).toBeInTheDocument();
            expect(
                screen.getByText("Bãi xe Bitexco Tower"),
            ).toBeInTheDocument();
            expect(screen.getByText("25.000 đ")).toBeInTheDocument();
            expect(
                screen.getByText("Hạng #1 · Tối ưu nhất"),
            ).toBeInTheDocument();

            const mapBtn = screen.getByRole("button", {
                name: /xem bãi bãi xe bitexco tower trên bản đồ/i,
            });
            fireEvent.click(mapBtn);
            expect(onSelect).toHaveBeenCalledWith("lot-101");
        });
    });
});
