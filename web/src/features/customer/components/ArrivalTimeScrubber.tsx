type Copy = (english: string, vietnamese: string) => string;

export type ScrubberSlot = {
    hour: number;
    /** Probability of the selected lot (or the average when none is selected) at this hour. */
    percent: number | null;
};

type ArrivalTimeScrubberProps = {
    slots: ScrubberSlot[];
    selectedHour: number;
    onChange: (hour: number) => void;
    loading: boolean;
    copy: Copy;
};

const formatHour = (hour: number) => `${String(hour).padStart(2, "0")}:00`;

function sparkline(slots: ScrubberSlot[]) {
    const width = 100;
    const height = 30;
    const step = slots.length > 1 ? width / (slots.length - 1) : width;
    const points = slots.map((slot, index) => ({
        x: index * step,
        y: height - ((slot.percent ?? 0) / 100) * height,
    }));
    const line = points.map((p) => `${p.x},${p.y}`).join(" ");
    return { line, area: `0,${height} ${line} ${width},${height}` };
}

export function ArrivalTimeScrubber({
    slots,
    selectedHour,
    onChange,
    loading,
    copy,
}: ArrivalTimeScrubberProps) {
    if (slots.length === 0) return null;
    const first = slots[0].hour;
    const last = slots[slots.length - 1].hour;
    const selected = slots.find((slot) => slot.hour === selectedHour);
    const { line, area } = sparkline(slots);
    const selectedIndex = Math.max(
        0,
        slots.findIndex((slot) => slot.hour === selectedHour),
    );
    const markerLeft =
        slots.length > 1 ? (selectedIndex / (slots.length - 1)) * 100 : 0;
    const valueText =
        selected?.percent === null || selected === undefined
            ? formatHour(selectedHour)
            : `${formatHour(selectedHour)}, ${selected.percent}% ${copy("chance of a free space", "khả năng còn chỗ")}`;

    return (
        <section
            className="arrival-scrubber"
            aria-label={copy("Arrival time forecast", "Dự báo theo giờ tới")}
        >
            <div className="arrival-scrubber-head">
                <strong>
                    {copy("Arrival time", "Giờ bạn tới")}:{" "}
                    {formatHour(selectedHour)}
                </strong>
                <span className="arrival-scrubber-note">
                    {loading
                        ? copy("Loading forecast", "Đang tải dự báo")
                        : copy("AI, simulated data", "AI, dữ liệu mô phỏng")}
                </span>
            </div>
            <div className="arrival-scrubber-chart">
                <svg
                    viewBox="0 0 100 30"
                    preserveAspectRatio="none"
                    aria-hidden="true"
                >
                    <polygon className="arrival-scrubber-area" points={area} />
                    <polyline className="arrival-scrubber-line" points={line} />
                </svg>
                <span
                    className="arrival-scrubber-marker"
                    style={{ left: `${markerLeft}%` }}
                />
            </div>
            <input
                className="arrival-scrubber-range"
                type="range"
                min={first}
                max={last}
                step={1}
                value={selectedHour}
                aria-label={copy("Arrival hour", "Giờ tới")}
                aria-valuetext={valueText}
                onChange={(event) => onChange(Number(event.target.value))}
            />
            <div className="arrival-scrubber-ticks" aria-hidden="true">
                {slots.map((slot) => (
                    <span
                        key={slot.hour}
                        className={slot.hour === selectedHour ? "active" : ""}
                    >
                        {slot.hour}h
                    </span>
                ))}
            </div>
        </section>
    );
}
