import { useArrivalForecast } from "../arrivalForecast";
import { ProbabilityRing, probabilityTone } from "./ProbabilityRing";

type Copy = (english: string, vietnamese: string) => string;

type LotArrivalForecastProps = {
    lotId: string;
    latitude: number;
    longitude: number;
    vehicleType: "Car" | "Motorbike";
    /** Booking date ("yyyy-mm-dd") and the hour the customer picked in the booking card. */
    date: string;
    hour: number;
    copy: Copy;
};

const formatHour = (hour: number) => `${String(hour).padStart(2, "0")}:00`;

/** Probability of a free space at the booking hour and around it, from the AI service (simulated data). */
export function LotArrivalForecast({
    lotId,
    latitude,
    longitude,
    vehicleType,
    date,
    hour,
    copy,
}: LotArrivalForecastProps) {
    const { slots, loading } = useArrivalForecast({
        latitude,
        longitude,
        // The lot sits at the centre, a small radius keeps the candidate list short.
        radiusKm: 2,
        vehicleType,
        date,
        anchorHour: hour,
    });
    const points = slots.map((slot) => ({
        hour: slot.hour,
        percent: slot.percentByLot?.[lotId] ?? null,
    }));
    const selected = points.find((point) => point.hour === hour);
    const hasAny = points.some((point) => point.percent !== null);

    return (
        <section
            className="lot-forecast"
            aria-label={copy("Free space forecast", "Dự báo còn chỗ")}
        >
            <div className="lot-forecast-head">
                <h2>
                    {copy("Forecast for your arrival", "Dự báo khi bạn tới")}
                </h2>
                <span className="lot-forecast-tag">
                    {copy("AI, simulated data", "AI, dữ liệu mô phỏng")}
                </span>
            </div>
            {loading && !hasAny ? (
                <p role="status">
                    {copy("Loading forecast", "Đang tải dự báo")}
                </p>
            ) : !hasAny ? (
                <p>
                    {copy(
                        "No forecast for this lot and vehicle type at the chosen time. The lot may be closed then or have no spaces for this vehicle.",
                        "Chưa có dự báo cho bãi và loại xe này vào giờ đã chọn. Có thể bãi đóng cửa lúc đó hoặc không có chỗ cho loại xe này.",
                    )}
                </p>
            ) : (
                <>
                    <div className="lot-forecast-main">
                        {selected?.percent !== null &&
                        selected?.percent !== undefined ? (
                            <>
                                <ProbabilityRing
                                    percent={selected.percent}
                                    size={64}
                                    label={`${selected.percent}% ${copy("chance of a free space", "khả năng còn chỗ")}`}
                                />
                                <div>
                                    <strong
                                        className={`lot-forecast-value lot-forecast-${probabilityTone(selected.percent)}`}
                                    >
                                        {selected.percent}%
                                    </strong>
                                    <span>
                                        {copy(
                                            "chance of a free space at",
                                            "khả năng còn chỗ lúc",
                                        )}{" "}
                                        {formatHour(hour)}
                                    </span>
                                </div>
                            </>
                        ) : (
                            <span>
                                {copy(
                                    "No figure for the chosen hour.",
                                    "Chưa có số liệu cho giờ đã chọn.",
                                )}
                            </span>
                        )}
                    </div>
                    <div className="lot-forecast-bars" aria-hidden="true">
                        {points.map((point) => (
                            <div
                                key={point.hour}
                                className={
                                    point.hour === hour ? "active" : undefined
                                }
                            >
                                <i
                                    style={{
                                        height: `${point.percent ?? 0}%`,
                                    }}
                                />
                                <small>{point.hour}h</small>
                            </div>
                        ))}
                    </div>
                    <p className="lot-forecast-note">
                        {copy(
                            "Bars show the chance of a free space per hour. Book ahead if you arrive when it is low.",
                            "Cột là khả năng còn chỗ theo giờ. Nên đặt trước nếu bạn tới lúc khả năng này thấp.",
                        )}
                    </p>
                </>
            )}
        </section>
    );
}
