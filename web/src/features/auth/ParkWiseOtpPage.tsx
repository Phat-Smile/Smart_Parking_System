"use client";

import { useState } from "react";
import type { FormEvent } from "react";
import Link from "next/link";
import { ParkWiseAuthAside, ParkWiseAuthLayout } from "./ParkWiseAuthLayout";
import { SegmentedOtpInput } from "./SegmentedOtpInput";

// The verification UI uses the six-digit code from the registration flow.
const OTP_LENGTH = 6;

export function ParkWiseOtpPage({ fontClassName }: { fontClassName: string }) {
    const [digits, setDigits] = useState<string[]>(() =>
        Array(OTP_LENGTH).fill(""),
    );
    const [error, setError] = useState("");
    const complete = digits.every(Boolean);

    function onDigitsChange(next: string[]) {
        setDigits(next);
        setError("");
    }

    function onSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        if (!complete) return;
        // Sign Up is not yet connected to an active verification request.
        setError(
            "No active verification request was found. Complete registration to receive a code.",
        );
    }

    return (
        <ParkWiseAuthLayout
            fontClassName={fontClassName}
            className="parkwise-otp-page"
        >
            <ParkWiseAuthAside
                description="Enter your verification code to finish creating your account."
                afterWelcome={
                    <div className="parkwise-aside-box parkwise-otp-back-box">
                        <p>Need to use a different email?</p>
                        <Link
                            href="/sign-up"
                            className="parkwise-otp-back-link"
                        >
                            Back to Sign Up
                        </Link>
                    </div>
                }
            />

            <div className="parkwise-login-form-panel parkwise-otp-form-panel">
                <form noValidate onSubmit={onSubmit}>
                    <h2>Verify Your Email</h2>
                    <div className="parkwise-otp-session-note" role="status">
                        <p>No active verification request was found.</p>
                        <p>Complete sign up to receive a code by email.</p>
                    </div>
                    <label
                        className="parkwise-otp-label"
                        id="parkwise-otp-label"
                    >
                        Verification code
                    </label>
                    <SegmentedOtpInput
                        value={digits}
                        onChange={onDigitsChange}
                        error={error}
                    />
                    {error && (
                        <p
                            id="parkwise-otp-error"
                            className="parkwise-login-error"
                            role="alert"
                            aria-live="polite"
                        >
                            {error}
                        </p>
                    )}
                    <button
                        className="parkwise-login-submit"
                        type="submit"
                        disabled={!complete}
                    >
                        Verify Code
                    </button>
                    <p className="parkwise-otp-help">
                        Need a new code?{" "}
                        <Link href="/sign-up">Return to sign up</Link>
                    </p>
                </form>
            </div>
        </ParkWiseAuthLayout>
    );
}
