"use client";

import { BarChart2, AlertCircle, Clock, TrendingUp } from "lucide-react";
import "./ai-recommendation.css";

export interface HourlyForecastSlot {
    hour: number;
    label?: string;
    probabilityPercent: number | null;
    insufficientData?: boolean;
    isPeak?: boolean;
    isCurrent?: boolean;
}

export interface LotArrivalForecastProps {
    lotName?: string;
    selectedHour?: number;
    forecastData?: HourlyForecastSlot[];
    insufficientData?: boolean;
    isLoading?: boolean;
    isError?: boolean;
    errorMessage?: string;
    onSelectHour?: (hour: number) => void;
    className?: string;
}

function formatHourLabel(hour: number): string {
    return `${String(hour).padStart(2, "0")}:00`;
}

export function LotArrivalForecast({
    lotName,
    selectedHour,
    forecastData = [],
    insufficientData = false,
    isLoading = false,
    isError = false,
    errorMessage = "Không thể tải biểu đồ dự báo còn chỗ. Vui lòng thử lại sau.",
    onSelectHour,
    className = "",
}: LotArrivalForecastProps) {
    if (isError) {
        return (
            <div
                className={`ai-forecast-card ${className}`}
                role="alert"
                aria-label="Lỗi dự báo khả năng còn chỗ"
            >
                <div className="ai-forecast-header">
                    <div className="ai-panel-title-group">
                        <span className="ai-panel-icon" aria-hidden="true">
                            <BarChart2 size={18} />
                        </span>
                        <h3 className="ai-panel-title">
                            Dự báo khả năng còn chỗ theo giờ
                        </h3>
                    </div>
                </div>
                <div className="ai-fallback-alert" style={{ margin: 0 }}>
                    <AlertCircle className="ai-fallback-icon" size={20} />
                    <div className="ai-fallback-content">
                        <p className="ai-fallback-title">Lỗi tải dữ liệu</p>
                        <p className="ai-fallback-desc">{errorMessage}</p>
                    </div>
                </div>
            </div>
        );
    }

    if (isLoading) {
        return (
            <div
                className={`ai-forecast-card ${className}`}
                aria-busy="true"
                role="status"
                aria-label="Đang tải dữ liệu dự báo..."
            >
                <div className="ai-forecast-header">
                    <div className="ai-panel-title-group">
                        <span className="ai-panel-icon" aria-hidden="true">
                            <BarChart2 size={18} />
                        </span>
                        <h3 className="ai-panel-title">
                            Dự báo khả năng còn chỗ theo giờ
                        </h3>
                    </div>
                    <div
                        className="ai-shimmer-block"
                        style={{ width: "80px", height: "24px" }}
                    />
                </div>
                <div
                    className="ai-forecast-chart-container"
                    style={{
                        alignItems: "flex-end",
                        gap: "12px",
                        height: "160px",
                    }}
                >
                    {Array.from({ length: 8 }).map((_, index) => (
                        <div
                            key={index}
                            style={{
                                flex: 1,
                                display: "flex",
                                flexDirection: "column",
                                alignItems: "center",
                                gap: "8px",
                            }}
                        >
                            <div
                                className="ai-shimmer-block"
                                style={{
                                    width: "24px",
                                    height: `${50 + (index % 4) * 25}px`,
                                    borderRadius: "4px 4px 0 0",
                                }}
                            />
                            <div
                                className="ai-shimmer-block"
                                style={{ width: "36px", height: "14px" }}
                            />
                        </div>
                    ))}
                </div>
                <span className="sr-only" style={{ display: "none" }}>
                    Đang tải dữ liệu dự báo...
                </span>
            </div>
        );
    }

    if (!forecastData || forecastData.length === 0 || insufficientData) {
        return (
            <div
                className={`ai-forecast-card ${className}`}
                role="region"
                aria-label="Dự báo khả năng còn chỗ"
            >
                <div className="ai-forecast-header">
                    <div className="ai-panel-title-group">
                        <span className="ai-panel-icon" aria-hidden="true">
                            <BarChart2 size={18} />
                        </span>
                        <h3 className="ai-panel-title">
                            Dự báo khả năng còn chỗ theo giờ
                        </h3>
                    </div>
                    {insufficientData && (
                        <span className="ai-panel-badge">Chưa đủ dữ liệu</span>
                    )}
                </div>
                <div
                    className="ai-empty-box"
                    style={{ padding: "var(--pw-space-lg) var(--pw-space-md)" }}
                >
                    <Clock size={32} color="var(--pw-text-muted)" />
                    <p
                        className="ai-empty-title"
                        style={{ fontSize: "0.95rem" }}
                    >
                        {insufficientData
                            ? "Chưa đủ dữ liệu dự báo cho bãi đỗ xe này."
                            : "Chưa có dữ liệu dự báo cho bãi đỗ xe này."}
                    </p>
                    <p className="ai-empty-desc" style={{ fontSize: "0.8rem" }}>
                        Dữ liệu dự báo theo giờ sẽ được hiển thị khi bãi xe có
                        đủ lịch sử phiên đỗ thực tế.
                    </p>
                </div>
            </div>
        );
    }

    return (
        <div
            className={`ai-forecast-card ${className}`}
            role="region"
            aria-label={`Biểu đồ dự báo khả năng còn chỗ theo giờ${lotName ? ` - ${lotName}` : ""}`}
        >
            <div className="ai-forecast-header">
                <div>
                    <div className="ai-panel-title-group">
                        <span className="ai-panel-icon" aria-hidden="true">
                            <BarChart2 size={18} />
                        </span>
                        <h3 className="ai-panel-title">
                            Dự báo khả năng còn chỗ theo giờ
                        </h3>
                    </div>
                    {lotName && (
                        <p
                            style={{
                                margin: "4px 0 0",
                                fontSize: "0.8rem",
                                color: "var(--pw-text-muted)",
                            }}
                        >
                            Bãi đỗ: <strong>{lotName}</strong>
                        </p>
                    )}
                </div>
                <span className="ai-panel-badge">Mô hình học máy BR-08</span>
            </div>

            {/* Visual Bar Chart */}
            <div
                className="ai-forecast-chart-container"
                role="group"
                aria-label="Biểu đồ tỷ lệ còn chỗ theo các khung giờ"
            >
                {forecastData.map((slot) => {
                    const isInsufficient =
                        slot.insufficientData === true ||
                        slot.probabilityPercent === null;
                    const prob =
                        !isInsufficient && slot.probabilityPercent !== null
                            ? Math.max(
                                  0,
                                  Math.min(
                                      100,
                                      Math.round(slot.probabilityPercent),
                                  ),
                              )
                            : null;
                    const label = slot.label ?? formatHourLabel(slot.hour);
                    const isSelected =
                        selectedHour !== undefined &&
                        selectedHour === slot.hour;
                    const tone =
                        prob === null
                            ? "prob-insufficient"
                            : prob >= 60
                              ? "prob-good"
                              : prob >= 30
                                ? "prob-warn"
                                : "prob-risk";

                    return (
                        <button
                            key={slot.hour}
                            type="button"
                            className={`ai-forecast-col ${isSelected ? "is-active" : ""}`}
                            onClick={() => onSelectHour?.(slot.hour)}
                            aria-label={`Khung giờ ${label}: ${isInsufficient ? "Chưa đủ dữ liệu" : `khả năng còn chỗ ${prob}%`}${slot.isPeak ? " (Giờ cao điểm)" : ""}`}
                            aria-pressed={isSelected}
                        >
                            {/* Peak Hour Indicator */}
                            {slot.isPeak && (
                                <span
                                    title="Giờ cao điểm"
                                    style={{
                                        fontSize: "0.65rem",
                                        fontWeight: 700,
                                        color: "var(--pw-status-warning)",
                                        marginBottom: "2px",
                                    }}
                                >
                                    <TrendingUp size={12} />
                                </span>
                            )}

                            {/* Percentage value or Insufficient Data label */}
                            {isInsufficient ? (
                                <span
                                    className="ai-forecast-insufficient-label"
                                    title="Chưa đủ dữ liệu"
                                    style={{
                                        fontSize: "0.65rem",
                                        fontWeight: 600,
                                        color: "var(--pw-text-muted)",
                                        marginBottom: "4px",
                                        whiteSpace: "nowrap",
                                    }}
                                >
                                    Chưa đủ dữ liệu
                                </span>
                            ) : (
                                <span
                                    style={{
                                        fontSize: "0.7rem",
                                        fontWeight: 700,
                                        color: "var(--pw-text-primary)",
                                        marginBottom: "4px",
                                    }}
                                >
                                    {prob}%
                                </span>
                            )}

                            {/* Bar */}
                            <div className="ai-forecast-bar-wrapper">
                                <div
                                    className={`ai-forecast-bar ${tone}`}
                                    style={{
                                        height: isInsufficient
                                            ? "6px"
                                            : `${prob}%`,
                                    }}
                                />
                            </div>

                            {/* Hour label */}
                            <span className="ai-forecast-hour-label">
                                {label}
                            </span>
                        </button>
                    );
                })}
            </div>

            {/* Legend */}
            <div
                className="ai-forecast-legend"
                aria-label="Chú thích màu sắc biểu đồ"
            >
                <div className="ai-legend-item">
                    <span
                        className="ai-legend-dot"
                        style={{ backgroundColor: "var(--pw-status-success)" }}
                    />
                    <span>Dễ còn chỗ (≥ 60%)</span>
                </div>
                <div className="ai-legend-item">
                    <span
                        className="ai-legend-dot"
                        style={{ backgroundColor: "var(--pw-status-warning)" }}
                    />
                    <span>Chỗ hạn chế (30% - 59%)</span>
                </div>
                <div className="ai-legend-item">
                    <span
                        className="ai-legend-dot"
                        style={{ backgroundColor: "var(--pw-status-error)" }}
                    />
                    <span>Nguy cơ hết chỗ (&lt; 30%)</span>
                </div>
                <div className="ai-legend-item">
                    <TrendingUp size={14} color="var(--pw-status-warning)" />
                    <span>Giờ cao điểm</span>
                </div>
            </div>
        </div>
    );
}
