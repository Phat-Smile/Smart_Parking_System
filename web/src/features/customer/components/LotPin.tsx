import type { CSSProperties } from "react";
import { probabilityTone } from "./ProbabilityRing";

type LotPinProps = {
    name: string;
    /** Free-space probability at the chosen arrival hour, or null when the AI has no figure for this lot. */
    percent: number | null;
    selected: boolean;
    onSelect: () => void;
    /** Absolute position on the illustrated map (percent) when no Mapbox token is set. */
    style?: CSSProperties;
    preview?: boolean;
};

/** Isometric block pin: height-neutral cube coloured by BR-08 tone, with the probability as text. */
export function LotPin({
    name,
    percent,
    selected,
    onSelect,
    style,
    preview = false,
}: LotPinProps) {
    const tone = percent === null ? "unknown" : probabilityTone(percent);
    const label =
        percent === null
            ? `Select parking lot ${name}`
            : `Select parking lot ${name}, ${percent}% chance of a free space`;

    return (
        <button
            type="button"
            className={`lot-pin lot-pin-${tone} ${selected ? "selected" : ""} ${preview ? "lot-pin-preview" : ""}`}
            style={style}
            aria-label={label}
            aria-pressed={selected}
            onClick={onSelect}
        >
            <span className="lot-pin-tag">
                {percent === null ? "?" : `${percent}%`}
            </span>
            <svg
                className="lot-pin-cube"
                viewBox="0 0 40 44"
                width="36"
                height="40"
                aria-hidden="true"
            >
                <polygon
                    className="lot-pin-left"
                    points="2,12 20,22 20,42 2,32"
                />
                <polygon
                    className="lot-pin-right"
                    points="20,22 38,12 38,32 20,42"
                />
                <polygon
                    className="lot-pin-top"
                    points="20,2 38,12 20,22 2,12"
                />
            </svg>
        </button>
    );
}
