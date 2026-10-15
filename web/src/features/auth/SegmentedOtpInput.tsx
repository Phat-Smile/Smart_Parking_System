"use client";

import { useEffect, useRef } from "react";
import type { ChangeEvent, ClipboardEvent, KeyboardEvent } from "react";

type SegmentedOtpInputProps = {
    value: string[];
    onChange: (value: string[]) => void;
    error?: string;
    disabled?: boolean;
    resetKey?: number;
    shaking?: boolean;
};

export function SegmentedOtpInput({
    value,
    onChange,
    error,
    disabled = false,
    resetKey = 0,
    shaking = false,
}: SegmentedOtpInputProps) {
    const inputs = useRef<Array<HTMLInputElement | null>>([]);
    const length = value.length;

    useEffect(() => {
        if (!disabled) inputs.current[0]?.focus();
    }, [disabled, resetKey]);

    function focus(index: number) {
        inputs.current[Math.max(0, Math.min(index, length - 1))]?.focus();
    }

    function onDigitChange(
        event: ChangeEvent<HTMLInputElement>,
        index: number,
    ) {
        const digit = event.target.value.replace(/[^0-9]/g, "").slice(-1);
        const next = [...value];
        next[index] = digit;
        onChange(next);
        if (digit && index < length - 1) focus(index + 1);
    }

    function onDigitKeyDown(
        event: KeyboardEvent<HTMLInputElement>,
        index: number,
    ) {
        if (event.key === "Backspace") {
            event.preventDefault();
            const next = [...value];
            if (next[index]) {
                next[index] = "";
            } else if (index > 0) {
                next[index - 1] = "";
                focus(index - 1);
            }
            onChange(next);
        } else if (event.key === "ArrowLeft") {
            event.preventDefault();
            focus(index - 1);
        } else if (event.key === "ArrowRight") {
            event.preventDefault();
            focus(index + 1);
        }
    }

    function onDigitPaste(
        event: ClipboardEvent<HTMLInputElement>,
        index: number,
    ) {
        event.preventDefault();
        const code = event.clipboardData.getData("text").replace(/[^0-9]/g, "");
        if (!code) return;

        const start = code.length >= length ? 0 : index;
        const next = [...value];
        for (
            let offset = 0;
            offset < code.length && start + offset < length;
            offset += 1
        ) {
            next[start + offset] = code[offset];
        }
        onChange(next);
        focus(Math.min(start + code.length, length - 1));
    }

    return (
        <div
            className={`parkwise-otp-digits${shaking ? " parkwise-otp-shake" : ""}`}
            role="group"
            aria-labelledby="parkwise-otp-label"
        >
            {value.map((digit, index) => (
                <input
                    key={index}
                    ref={(element) => {
                        inputs.current[index] = element;
                    }}
                    name={`otp-digit-${index + 1}`}
                    type="text"
                    inputMode="numeric"
                    pattern="[0-9]*"
                    autoComplete={index === 0 ? "one-time-code" : "off"}
                    maxLength={1}
                    value={digit}
                    data-filled={Boolean(digit)}
                    disabled={disabled}
                    aria-label={`Digit ${index + 1} of ${length}`}
                    aria-invalid={Boolean(error)}
                    aria-describedby={error ? "parkwise-otp-error" : undefined}
                    onFocus={(event) => event.currentTarget.select()}
                    onChange={(event) => onDigitChange(event, index)}
                    onKeyDown={(event) => onDigitKeyDown(event, index)}
                    onPaste={(event) => onDigitPaste(event, index)}
                />
            ))}
        </div>
    );
}
