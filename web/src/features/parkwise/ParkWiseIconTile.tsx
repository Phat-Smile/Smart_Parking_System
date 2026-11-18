import type { ReactNode } from "react";
import "./parkwise-controls.css";

export type ParkWiseTone =
    "wallet" | "booking" | "parking" | "vehicles" | "account" | "notifications";

/** Shared service colors: the same feature has the same tone on every page. */
export function ParkWiseIconTile({
    tone,
    children,
    className = "",
}: {
    tone: ParkWiseTone;
    children: ReactNode;
    className?: string;
}) {
    return (
        <span
            className={`parkwise-icon-tile parkwise-tone ${className}`.trim()}
            data-tone={tone}
        >
            {children}
        </span>
    );
}
