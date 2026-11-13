export type ProbabilityTone = "good" | "warn" | "risk";

/** Thresholds follow BR-08: below 30% is a high full-risk, 60% and above is comfortable. */
export function probabilityTone(percent: number): ProbabilityTone {
    if (percent >= 60) return "good";
    if (percent >= 30) return "warn";
    return "risk";
}

// Set on the SVG itself so the ring also draws on pages that do not load the search stylesheet.
const TONE_STROKE: Record<ProbabilityTone, string> = {
    good: "var(--pw-color-success, #16a34a)",
    warn: "var(--pw-color-warning, #f59e0b)",
    risk: "var(--pw-color-error, #dc2626)",
};

type ProbabilityRingProps = {
    percent: number;
    size?: number;
    /** Text for screen readers, for example "82% chance of a free space". */
    label: string;
};

export function ProbabilityRing({
    percent,
    size = 44,
    label,
}: ProbabilityRingProps) {
    const value = Math.max(0, Math.min(100, Math.round(percent)));
    const stroke = 5;
    const radius = (size - stroke) / 2;
    const circumference = 2 * Math.PI * radius;
    const dash = (circumference * value) / 100;

    return (
        <svg
            className={`probability-ring probability-ring-${probabilityTone(value)}`}
            width={size}
            height={size}
            viewBox={`0 0 ${size} ${size}`}
            role="img"
            aria-label={label}
        >
            <circle
                className="probability-ring-track"
                cx={size / 2}
                cy={size / 2}
                r={radius}
                fill="none"
                stroke="var(--pw-gray-200, #e5e5e5)"
                strokeWidth={stroke}
            />
            <circle
                className="probability-ring-value"
                cx={size / 2}
                cy={size / 2}
                r={radius}
                fill="none"
                stroke={TONE_STROKE[probabilityTone(value)]}
                strokeWidth={stroke}
                strokeLinecap="round"
                strokeDasharray={`${dash} ${circumference}`}
                transform={`rotate(-90 ${size / 2} ${size / 2})`}
            />
            <text
                className="probability-ring-text"
                x="50%"
                y="50%"
                textAnchor="middle"
                dominantBaseline="central"
                fill="var(--pw-color-black, #171717)"
                fontSize={size >= 56 ? 16 : 12}
                fontWeight={800}
            >
                {value}
            </text>
        </svg>
    );
}
