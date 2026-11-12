export type ProbabilityTone = "good" | "warn" | "risk";

/** Thresholds follow BR-08: below 30% is a high full-risk, 60% and above is comfortable. */
export function probabilityTone(percent: number): ProbabilityTone {
    if (percent >= 60) return "good";
    if (percent >= 30) return "warn";
    return "risk";
}

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
                strokeWidth={stroke}
            />
            <circle
                className="probability-ring-value"
                cx={size / 2}
                cy={size / 2}
                r={radius}
                fill="none"
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
            >
                {value}
            </text>
        </svg>
    );
}
