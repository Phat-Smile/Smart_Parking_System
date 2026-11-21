"use client";

import { useState } from "react";
import {
    Sparkles,
    Compass,
    AlertCircle,
    Info,
    MapPin,
    Calendar,
    Navigation,
    Clock,
    DollarSign,
    CheckCircle2,
    AlertTriangle,
    RotateCcw,
} from "lucide-react";
import { AiScoreExplanation } from "./AiScoreExplanation";
import type { RecommendedParkingLot } from "@/features/customer/parkingLotApi";
import "./ai-recommendation.css";

export type RecommendationState = "empty" | "loading" | "error" | "success";

export interface AiRecommendationPanelProps {
    state?: RecommendationState;
    hasDestination?: boolean;
    isLoading?: boolean;
    isError?: boolean;
    errorMessage?: string;
    fallbackUsed?: boolean;
    fallbackReason?: string | null;
    recommendations?: RecommendedParkingLot[];
    selectedLotId?: string | null;
    onSelectOnMap?: (lot: RecommendedParkingLot) => void;
    onBook?: (lot: RecommendedParkingLot) => void;
    onRetry?: () => void;
    className?: string;
}

function formatVnd(value: number): string {
    return `${new Intl.NumberFormat("vi-VN").format(value)} đ`;
}

function formatDriveTime(
    driveTimeMinutes: number | null,
    distanceKm: number,
): string {
    if (driveTimeMinutes !== null && !isNaN(driveTimeMinutes)) {
        return `~${Math.round(driveTimeMinutes)} phút lái xe (${distanceKm} km)`;
    }
    return `${distanceKm} km`;
}

export function AiRecommendationPanel({
    state,
    hasDestination = false,
    isLoading = false,
    isError = false,
    errorMessage,
    fallbackUsed = false,
    fallbackReason,
    recommendations = [],
    selectedLotId,
    onSelectOnMap,
    onBook,
    onRetry,
    className = "",
}: AiRecommendationPanelProps) {
    const [isExplanationOpen, setIsExplanationOpen] = useState(false);

    // Resolve state if not explicitly passed
    let resolvedState: RecommendationState = state ?? "empty";
    if (!state) {
        if (isLoading) {
            resolvedState = "loading";
        } else if (isError) {
            resolvedState = "error";
        } else if (!hasDestination) {
            resolvedState = "empty";
        } else if (recommendations && recommendations.length > 0) {
            resolvedState = "success";
        } else {
            resolvedState = "empty";
        }
    }

    return (
        <section
            className={`ai-panel-container ${className}`}
            aria-label={
                fallbackUsed
                    ? "Danh sách xếp theo khoảng cách"
                    : "Gợi ý bãi đỗ thông minh từ AI"
            }
        >
            {/* Header */}
            <div className="ai-panel-header">
                <div className="ai-panel-title-group">
                    <span className="ai-panel-icon" aria-hidden="true">
                        {fallbackUsed ? (
                            <Navigation size={18} />
                        ) : (
                            <Sparkles size={18} />
                        )}
                    </span>
                    <div>
                        <h2 className="ai-panel-title">
                            {fallbackUsed
                                ? "Danh sách xếp theo khoảng cách"
                                : "Gợi ý bãi đỗ thông minh từ AI"}
                        </h2>
                    </div>
                </div>

                <div
                    style={{
                        display: "flex",
                        alignItems: "center",
                        gap: "var(--pw-space-xs)",
                    }}
                >
                    <span className="ai-panel-badge">
                        {fallbackUsed ? "Theo khoảng cách" : "Mô hình BR-08"}
                    </span>
                    {!fallbackUsed && (
                        <button
                            type="button"
                            className="ai-explain-btn"
                            onClick={() => setIsExplanationOpen(true)}
                            aria-label="Xem giải thích cách AI tính điểm và xếp hạng"
                        >
                            <Info size={16} />
                            <span>Vì sao gợi ý này?</span>
                        </button>
                    )}
                </div>
            </div>

            {fallbackUsed && (
                <div
                    className="ai-fallback-notice"
                    role="status"
                    aria-label="Thông báo chế độ xếp theo khoảng cách"
                >
                    <Info size={16} aria-hidden="true" />
                    <span>
                        Hệ thống đã tự động sắp xếp các bãi đỗ theo khoảng cách
                        vì tiêu chí gợi ý AI chưa thể áp dụng đầy đủ.
                    </span>
                </div>
            )}

            {/* State A: Empty State (No Destination Chosen) */}
            {resolvedState === "empty" && (
                <div
                    className="ai-empty-box"
                    data-testid="ai-state-empty"
                    role="region"
                    aria-label="Chưa chọn điểm đến"
                >
                    <div className="ai-empty-icon" aria-hidden="true">
                        <Compass size={24} />
                    </div>
                    <h3 className="ai-empty-title">
                        Chọn điểm đến để nhận gợi ý tối ưu
                    </h3>
                    <p className="ai-empty-desc">
                        Vui lòng nhập hoặc chọn điểm đến trên bản đồ. Hệ thống
                        AI sẽ tự động phân tích và đề xuất Top 3 bãi đỗ xe tối
                        ưu nhất cho bạn.
                    </p>
                    <div className="ai-empty-features">
                        <span className="ai-empty-feature-item">
                            <Navigation
                                size={14}
                                color="var(--pw-brand-primary)"
                            />
                            Tối ưu thời gian lái xe
                        </span>
                        <span className="ai-empty-feature-item">
                            <DollarSign
                                size={14}
                                color="var(--pw-status-success)"
                            />
                            Tiết kiệm chi phí đỗ
                        </span>
                        <span className="ai-empty-feature-item">
                            <CheckCircle2
                                size={14}
                                color="var(--pw-status-info)"
                            />
                            Dự báo còn chỗ lúc tới
                        </span>
                    </div>
                </div>
            )}

            {/* State B: Loading State (Shimmer Skeleton) */}
            {resolvedState === "loading" && (
                <div
                    className="ai-skeleton-container"
                    data-testid="ai-state-loading"
                    aria-busy="true"
                    role="status"
                    aria-label="Đang tải gợi ý từ AI..."
                >
                    <div
                        style={{
                            display: "flex",
                            justifyContent: "space-between",
                            alignItems: "center",
                        }}
                    >
                        <div
                            className="ai-shimmer-block"
                            style={{ width: "200px", height: "16px" }}
                        />
                        <div
                            className="ai-shimmer-block"
                            style={{ width: "90px", height: "16px" }}
                        />
                    </div>
                    <div className="ai-cards-grid">
                        {[1, 2, 3].map((item) => (
                            <div key={item} className="ai-skeleton-card">
                                <div
                                    style={{
                                        display: "flex",
                                        justifyContent: "space-between",
                                    }}
                                >
                                    <div
                                        className="ai-shimmer-block"
                                        style={{
                                            width: "60px",
                                            height: "22px",
                                            borderRadius: "9999px",
                                        }}
                                    />
                                    <div
                                        className="ai-shimmer-block"
                                        style={{
                                            width: "80px",
                                            height: "22px",
                                        }}
                                    />
                                </div>
                                <div
                                    className="ai-shimmer-block"
                                    style={{ width: "70%", height: "20px" }}
                                />
                                <div
                                    className="ai-shimmer-block"
                                    style={{ width: "90%", height: "14px" }}
                                />
                                <div
                                    className="ai-shimmer-block"
                                    style={{ width: "100%", height: "48px" }}
                                />
                                <div style={{ display: "flex", gap: "6px" }}>
                                    <div
                                        className="ai-shimmer-block"
                                        style={{
                                            width: "70px",
                                            height: "20px",
                                            borderRadius: "4px",
                                        }}
                                    />
                                    <div
                                        className="ai-shimmer-block"
                                        style={{
                                            width: "85px",
                                            height: "20px",
                                            borderRadius: "4px",
                                        }}
                                    />
                                </div>
                                <div
                                    style={{
                                        display: "grid",
                                        gridTemplateColumns: "1fr 1fr",
                                        gap: "8px",
                                        marginTop: "4px",
                                    }}
                                >
                                    <div
                                        className="ai-shimmer-block"
                                        style={{
                                            height: "44px",
                                            borderRadius: "8px",
                                        }}
                                    />
                                    <div
                                        className="ai-shimmer-block"
                                        style={{
                                            height: "44px",
                                            borderRadius: "8px",
                                        }}
                                    />
                                </div>
                            </div>
                        ))}
                    </div>
                    <span className="sr-only" style={{ display: "none" }}>
                        Đang tải gợi ý từ AI...
                    </span>
                </div>
            )}

            {/* State C: Error State (Graceful Fallback Alert) */}
            {resolvedState === "error" && (
                <div
                    className="ai-fallback-alert"
                    data-testid="ai-state-error"
                    role="alert"
                    aria-label="Thông báo gợi ý AI"
                >
                    <AlertCircle className="ai-fallback-icon" size={24} />
                    <div className="ai-fallback-content">
                        <h3 className="ai-fallback-title">
                            Tạm thời không có gợi ý, danh sách vẫn đầy đủ
                        </h3>
                        <p className="ai-fallback-desc">
                            {errorMessage ||
                                "Hệ thống AI đang bận hoặc gặp sự cố kết nối. Bạn vẫn có thể tìm kiếm và chọn bãi đỗ xe bình thường từ danh sách đầy đủ bên dưới."}
                        </p>
                        {onRetry && (
                            <button
                                type="button"
                                className="ai-retry-btn"
                                onClick={onRetry}
                            >
                                <RotateCcw size={16} />
                                Thử lại gợi ý AI
                            </button>
                        )}
                    </div>
                </div>
            )}

            {/* State D: Success State (Ranked Cards or Fallback Cards) */}
            {resolvedState === "success" && (
                <div
                    className="ai-cards-grid"
                    data-testid="ai-state-success"
                    role="region"
                    aria-label={
                        fallbackUsed
                            ? "Danh sách bãi đỗ xe xếp theo khoảng cách"
                            : "Danh sách bãi đỗ xe AI gợi ý"
                    }
                >
                    {recommendations.length === 0 ? (
                        <div
                            className="ai-empty-box"
                            style={{ gridColumn: "1 / -1" }}
                        >
                            <p className="ai-empty-title">
                                Không tìm thấy bãi đỗ xe phù hợp trong phạm vi
                                này.
                            </p>
                            <p className="ai-empty-desc">
                                Vui lòng mở rộng bán kính tìm kiếm hoặc chọn
                                thời gian khác.
                            </p>
                        </div>
                    ) : (
                        recommendations.map((lot) => {
                            const isSelected = selectedLotId === lot.lotId;
                            const isRank1 = lot.rank === 1;
                            const isInsufficient =
                                lot.insufficientData === true ||
                                lot.probabilityPercent === null;
                            const prob =
                                !isInsufficient &&
                                lot.probabilityPercent !== null
                                    ? Math.round(lot.probabilityPercent)
                                    : null;
                            const isHighRisk =
                                !isInsufficient &&
                                (lot.warningHighFullRisk ||
                                    (prob !== null && prob < 30));
                            const probTone =
                                prob === null
                                    ? "prob-insufficient"
                                    : prob >= 60
                                      ? "prob-good"
                                      : prob >= 30
                                        ? "prob-warn"
                                        : "prob-risk";

                            return (
                                <article
                                    key={lot.lotId}
                                    className={`ai-card ${isRank1 ? "ai-card-rank-1" : ""}`}
                                    style={
                                        isSelected
                                            ? {
                                                  outline:
                                                      "2px solid var(--pw-brand-primary)",
                                                  outlineOffset: "2px",
                                              }
                                            : undefined
                                    }
                                >
                                    {/* Top Row: Rank & Risk Warning */}
                                    <div className="ai-card-top-row">
                                        <span
                                            className={`ai-rank-badge ${isRank1 ? "ai-rank-badge-1" : ""}`}
                                        >
                                            {fallbackUsed
                                                ? `Hạng #${lot.rank}${isRank1 ? " · Gần nhất" : ""}`
                                                : `Hạng #${lot.rank}${isRank1 ? " · Tối ưu nhất" : ""}`}
                                        </span>
                                        {isHighRisk && (
                                            <span
                                                className="ai-risk-warning"
                                                title="Xác suất còn chỗ dưới 30%"
                                            >
                                                <AlertTriangle size={12} />
                                                Nguy cơ hết chỗ cao
                                            </span>
                                        )}
                                    </div>

                                    {/* Name & Address */}
                                    <div>
                                        <h3 className="ai-lot-name">
                                            {lot.name}
                                        </h3>
                                        <p className="ai-lot-address">
                                            {lot.address}
                                        </p>
                                    </div>

                                    {/* Metrics Grid: Price, Distance/Time, Probability */}
                                    <div className="ai-metrics-grid">
                                        <div className="ai-metric-item">
                                            <span className="ai-metric-value">
                                                {formatVnd(lot.price)}
                                            </span>
                                            <span className="ai-metric-label">
                                                Chi phí
                                            </span>
                                        </div>
                                        <div className="ai-metric-item">
                                            <span className="ai-metric-value">
                                                {lot.distanceKm} km
                                            </span>
                                            <span className="ai-metric-label">
                                                {lot.driveTimeMinutes !== null
                                                    ? `~${Math.round(lot.driveTimeMinutes)}p lái xe`
                                                    : "Cự ly"}
                                            </span>
                                        </div>
                                        <div className="ai-metric-item">
                                            <span
                                                className={`ai-metric-value ai-metric-${probTone}`}
                                            >
                                                {isInsufficient
                                                    ? "Chưa đủ dữ liệu"
                                                    : `${prob}%`}
                                            </span>
                                            <span className="ai-metric-label">
                                                Còn chỗ lúc tới
                                            </span>
                                        </div>
                                    </div>

                                    {/* Up to 3 Reason Tags */}
                                    {lot.reasons && lot.reasons.length > 0 && (
                                        <ul
                                            className="ai-reasons-list"
                                            aria-label="Lý do gợi ý"
                                        >
                                            {lot.reasons
                                                .slice(0, 3)
                                                .map((reason, index) => (
                                                    <li
                                                        key={index}
                                                        className="ai-reason-tag"
                                                    >
                                                        {reason}
                                                    </li>
                                                ))}
                                        </ul>
                                    )}

                                    {/* Dual Action Buttons: Map & Booking */}
                                    <div className="ai-card-actions">
                                        <button
                                            type="button"
                                            className="ai-btn-map"
                                            onClick={() => onSelectOnMap?.(lot)}
                                            aria-label={`Xem bãi ${lot.name} trên bản đồ`}
                                        >
                                            <MapPin size={16} />
                                            <span>Xem trên bản đồ</span>
                                        </button>
                                        <button
                                            type="button"
                                            className="ai-btn-book"
                                            onClick={() => onBook?.(lot)}
                                            aria-label={`Đặt chỗ tại bãi ${lot.name}`}
                                        >
                                            <Calendar size={16} />
                                            <span>Đặt chỗ</span>
                                        </button>
                                    </div>
                                </article>
                            );
                        })
                    )}
                </div>
            )}

            {/* Transparent Scoring Explanation Dialog */}
            <AiScoreExplanation
                isOpen={isExplanationOpen}
                onClose={() => setIsExplanationOpen(false)}
            />
        </section>
    );
}
