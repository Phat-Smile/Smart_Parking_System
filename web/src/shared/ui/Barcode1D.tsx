"use client";

import React from "react";

export function Barcode1D({
    value,
    height = 48,
    className = "",
}: {
    value: string;
    height?: number;
    className?: string;
}) {
    const bars: { width: number; isBlack: boolean }[] = [];

    // Quiet zone & Start guard pattern
    bars.push({ width: 4, isBlack: false });
    bars.push({ width: 3, isBlack: true });
    bars.push({ width: 2, isBlack: false });
    bars.push({ width: 3, isBlack: true });
    bars.push({ width: 2, isBlack: false });

    // Deterministic bar width mapping for code-128 appearance
    const cleanValue =
        value.replace(/[^A-Za-z0-9_-]/g, "").toUpperCase() || "PARKWISE";
    for (let i = 0; i < cleanValue.length; i++) {
        const charCode = cleanValue.charCodeAt(i);
        const b1 = (charCode % 3) + 1;
        const w1 = ((charCode >> 2) % 3) + 1;
        const b2 = ((charCode >> 4) % 3) + 1;
        const w2 = 2;
        bars.push({ width: b1 * 1.4, isBlack: true });
        bars.push({ width: w1 * 1.4, isBlack: false });
        bars.push({ width: b2 * 1.4, isBlack: true });
        bars.push({ width: w2 * 1.4, isBlack: false });
    }

    // Stop guard pattern & Quiet zone
    bars.push({ width: 3, isBlack: true });
    bars.push({ width: 2, isBlack: false });
    bars.push({ width: 4, isBlack: true });
    bars.push({ width: 4, isBlack: false });

    const totalWidth = bars.reduce((acc, b) => acc + b.width, 0);

    let currentX = 0;
    return (
        <div className={`flex flex-col items-center select-none ${className}`}>
            <svg
                width="100%"
                height={height}
                viewBox={`0 0 ${totalWidth} ${height}`}
                preserveAspectRatio="none"
                className="w-full max-w-[260px]"
                aria-label={`Barcode: ${cleanValue}`}
            >
                <rect width={totalWidth} height={height} fill="#ffffff" />
                {bars.map((bar, idx) => {
                    const x = currentX;
                    currentX += bar.width;
                    if (!bar.isBlack) return null;
                    return (
                        <rect
                            key={idx}
                            x={x}
                            y={0}
                            width={bar.width}
                            height={height}
                            fill="#000000"
                        />
                    );
                })}
            </svg>
            <div className="font-mono text-[10px] tracking-[0.2em] text-neutral-800 font-bold mt-1 text-center">
                *{cleanValue}*
            </div>
        </div>
    );
}
