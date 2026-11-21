"use client";

import { AiRecommendationPanel } from "../components/AiRecommendationPanel";
import { type RecommendedParkingLot } from "../parkingLotApi";

export type CustomerAiRecommendationsProps = {
    recommendations: RecommendedParkingLot[] | null;
    activeId: string;
    onSelect: (lotId: string) => void;
    copy: (en: string, vi: string) => string;
};

export function CustomerAiRecommendations({
    recommendations,
    activeId,
    onSelect,
    copy,
}: CustomerAiRecommendationsProps) {
    if (!recommendations || recommendations.length === 0) return null;

    return (
        <AiRecommendationPanel
            recommendations={recommendations}
            activeId={activeId}
            onSelect={onSelect}
            copy={copy}
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
