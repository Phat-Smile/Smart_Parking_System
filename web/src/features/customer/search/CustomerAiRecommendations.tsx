"use client";

import { AiRecommendationPanel, type RecommendationState } from "./ai";
import { type RecommendedParkingLot } from "../parkingLotApi";

export type CustomerAiRecommendationsProps = {
    recommendations?: RecommendedParkingLot[] | null;
    activeId?: string;
    onSelect?: (lotId: string) => void;
    copy?: (en: string, vi: string) => string;
    state?: RecommendationState;
    hasDestination?: boolean;
    isLoading?: boolean;
    isError?: boolean;
    errorMessage?: string;
    fallbackUsed?: boolean;
    fallbackReason?: string | null;
    onSelectOnMap?: (lot: RecommendedParkingLot) => void;
    onBook?: (lot: RecommendedParkingLot) => void;
    onRetry?: () => void;
    className?: string;
};

export function CustomerAiRecommendations({
    recommendations,
    activeId = "",
    onSelect,
    copy,
    state,
    hasDestination = false,
    isLoading = false,
    isError = false,
    errorMessage,
    fallbackUsed = false,
    fallbackReason,
    onSelectOnMap,
    onBook,
    onRetry,
    className = "",
}: CustomerAiRecommendationsProps) {
    const handleSelectOnMap = (lot: RecommendedParkingLot) => {
        if (onSelectOnMap) {
            onSelectOnMap(lot);
        } else if (onSelect) {
            onSelect(lot.lotId);
        }
    };

    const handleBook = (lot: RecommendedParkingLot) => {
        if (onBook) {
            onBook(lot);
        } else if (typeof window !== "undefined") {
            window.location.assign(`/parking-lots/${lot.lotId}`);
        }
    };

    return (
        <AiRecommendationPanel
            state={state}
            hasDestination={hasDestination}
            isLoading={isLoading}
            isError={isError}
            errorMessage={errorMessage}
            fallbackUsed={fallbackUsed}
            fallbackReason={fallbackReason}
            recommendations={recommendations ?? []}
            selectedLotId={activeId}
            onSelectOnMap={handleSelectOnMap}
            onBook={handleBook}
            onRetry={onRetry}
            className={className}
        />
    );
}

export function CustomerAiRecommendationsSkeleton() {
    return (
        <div
            className="ai-recommendation-skeleton"
            style={{
                minHeight: "120px",
                borderRadius: "16px",
                background:
                    "var(--color-bg-secondary, rgba(255, 255, 255, 0.04))",
                border: "1px solid var(--color-border-subtle, rgba(255, 255, 255, 0.08))",
                marginBottom: "1rem",
                animation: "pulse 1.5s infinite ease-in-out",
            }}
            aria-hidden="true"
        />
    );
}
