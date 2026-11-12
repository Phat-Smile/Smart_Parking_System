import type { RecommendedParkingLot } from "../parkingLotApi";
import { ProbabilityRing } from "./ProbabilityRing";

type Copy = (english: string, vietnamese: string) => string;

type AiRecommendationPanelProps = {
    recommendations: RecommendedParkingLot[];
    activeId: string;
    onSelect: (lotId: string) => void;
    copy: Copy;
};

const formatVnd = (value: number) =>
    `${new Intl.NumberFormat("vi-VN").format(value)} ₫`;

function driveOrDistance(rec: RecommendedParkingLot, copy: Copy): string {
    if (rec.driveTimeMinutes) {
        return `${Math.round(rec.driveTimeMinutes)} ${copy("min drive", "phút lái xe")}`;
    }
    return `${rec.distanceKm} km`;
}

export function AiRecommendationPanel({
    recommendations,
    activeId,
    onSelect,
    copy,
}: AiRecommendationPanelProps) {
    const [top, ...others] = recommendations;
    if (!top) return null;

    return (
        <section
            className="ai-recommendation-banner"
            aria-label={copy(
                "AI parking recommendations",
                "Gợi ý bãi đỗ từ AI",
            )}
        >
            <div className="ai-recommendation-header">
                <div className="ai-recommendation-title">
                    <span>
                        {copy(
                            "Chosen for your trip",
                            "Trợ lý chọn cho chuyến đi của bạn",
                        )}
                    </span>
                </div>
                <span className="ai-recommendation-badge">
                    {copy(
                        "Simulated data, not real accuracy",
                        "Dữ liệu mô phỏng",
                    )}
                </span>
            </div>

            <article
                className={`ai-top-pick ${activeId === top.lotId ? "active" : ""}`}
            >
                <div className="ai-top-pick-head">
                    <span className="ai-top-pick-badge">
                        {copy("Top pick", "Gợi ý số 1")}
                    </span>
                    {top.warningHighFullRisk && (
                        <span className="ai-top-pick-warning">
                            {copy("May be full", "Có thể đầy")}
                        </span>
                    )}
                </div>
                <h3 className="ai-top-pick-name">{top.name}</h3>
                <p className="ai-top-pick-address">{top.address}</p>
                {top.reasons.length > 0 && (
                    <ul className="ai-top-pick-reasons">
                        {top.reasons.slice(0, 3).map((reason) => (
                            <li key={reason}>{reason}</li>
                        ))}
                    </ul>
                )}
                <dl className="ai-top-pick-stats">
                    <div>
                        <dd>{formatVnd(top.price)}</dd>
                        <dt>{copy("Price", "Giá")}</dt>
                    </div>
                    <div>
                        <dd>{driveOrDistance(top, copy)}</dd>
                        <dt>{copy("To the lot", "Tới bãi")}</dt>
                    </div>
                    <div>
                        <dd>{Math.round(top.probabilityPercent)}%</dd>
                        <dt>
                            {copy("Free space on arrival", "Còn chỗ lúc tới")}
                        </dt>
                    </div>
                </dl>
                <button
                    type="button"
                    className="ai-top-pick-action"
                    onClick={() => onSelect(top.lotId)}
                >
                    {copy("View on map", "Xem trên bản đồ")}
                </button>
            </article>

            {others.length > 0 && (
                <>
                    <p className="ai-alt-heading">
                        {copy("Other options", "Các lựa chọn khác")}
                    </p>
                    <ul className="ai-alt-list">
                        {others.map((rec) => (
                            <li key={rec.lotId}>
                                <button
                                    type="button"
                                    className={`ai-alt-row ${activeId === rec.lotId ? "active" : ""}`}
                                    onClick={() => onSelect(rec.lotId)}
                                >
                                    <ProbabilityRing
                                        percent={rec.probabilityPercent}
                                        label={`${Math.round(rec.probabilityPercent)}% ${copy("chance of a free space", "khả năng còn chỗ")}`}
                                    />
                                    <span className="ai-alt-main">
                                        <strong>{rec.name}</strong>
                                        <small>
                                            {driveOrDistance(rec, copy)}
                                        </small>
                                    </span>
                                    <span className="ai-alt-price">
                                        {formatVnd(rec.price)}
                                    </span>
                                </button>
                            </li>
                        ))}
                    </ul>
                </>
            )}
        </section>
    );
}
