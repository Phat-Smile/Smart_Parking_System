"use client";

import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import type { FormEvent } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ParkWiseAuthLayout } from "./ParkWiseAuthLayout";
import { SegmentedOtpInput } from "./SegmentedOtpInput";
import {
    clearPendingVerification,
    readPendingVerification,
    readPendingVerificationCooldown,
    readPendingVerificationExpiration,
    rememberPendingVerificationCooldown,
    rememberPendingVerificationExpiration,
} from "./pendingVerification";
import { resendParkWiseOtp, verifyParkWiseEmail } from "./emailVerificationApi";
import { ApiError } from "@/shared/errors/api-error";

// The verification UI uses the six-digit code from the registration flow.
const OTP_LENGTH = 6;
const subscribePendingVerification = () => () => {};

function formatRemaining(seconds: number) {
    return `${String(Math.floor(seconds / 60)).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`;
}

export function ParkWiseOtpPage({
    fontClassName,
    initialEmail = "",
}: {
    fontClassName: string;
    initialEmail?: string;
}) {
    const router = useRouter();
    const lastSubmittedCode = useRef("");
    const storedEmail = useSyncExternalStore(
        subscribePendingVerification,
        readPendingVerification,
        () => null,
    );
    const email =
        storedEmail ||
        (/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(initialEmail) ? initialEmail : "");
    const [digits, setDigits] = useState<string[]>(() =>
        Array(OTP_LENGTH).fill(""),
    );
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const [verifying, setVerifying] = useState(false);
    const [resending, setResending] = useState(false);
    const [cooldown, setCooldown] = useState(0);
    const [expirationSeconds, setExpirationSeconds] = useState<number | null>(
        null,
    );
    const [verified, setVerified] = useState(false);
    const [redirectSeconds, setRedirectSeconds] = useState(5);
    const [locked, setLocked] = useState(false);
    const [resetKey, setResetKey] = useState(0);
    const [shaking, setShaking] = useState(false);
    const complete = digits.every(Boolean);
    const expired = expirationSeconds === 0;

    useEffect(() => {
        const updateTimers = () => {
            setCooldown(readPendingVerificationCooldown());
            setExpirationSeconds(readPendingVerificationExpiration());
        };
        const firstTick = window.setTimeout(updateTimers, 0);
        const interval = window.setInterval(updateTimers, 1000);
        return () => {
            window.clearTimeout(firstTick);
            window.clearInterval(interval);
        };
    }, []);

    useEffect(() => {
        if (!verified) return;
        if (redirectSeconds === 0) {
            router.replace("/login");
            return;
        }
        const timer = window.setTimeout(
            () => setRedirectSeconds((seconds) => seconds - 1),
            1000,
        );
        return () => window.clearTimeout(timer);
    }, [verified, redirectSeconds, router]);

    async function verify(code: string) {
        setError("");
        setNotice("");
        setVerifying(true);
        try {
            const result = await verifyParkWiseEmail(email!, code);
            if (!result.isVerified)
                throw new Error("Verification was not confirmed.");
            clearPendingVerification();
            setVerified(true);
        } catch (failure) {
            const code =
                failure instanceof ApiError
                    ? failure.problem?.errorCode
                    : undefined;
            setError(
                code === "identity.invalid_otp_code"
                    ? "Incorrect code. Check the latest email and try again."
                    : code === "identity.otp_max_attempts_exceeded"
                      ? "This code was locked after 3 incorrect attempts. Request a new code below to continue."
                      : code === "identity.otp_expired"
                        ? "This code has expired or was replaced. Request a new code to continue."
                        : "We couldn't verify this code. Please try again.",
            );
            if (code === "identity.otp_max_attempts_exceeded") {
                setLocked(true);
                setDigits(Array(OTP_LENGTH).fill(""));
                setResetKey((value) => value + 1);
            } else if (code === "identity.otp_expired") {
                rememberPendingVerificationExpiration(0);
                setExpirationSeconds(0);
            } else if (code === "identity.invalid_otp_code") {
                lastSubmittedCode.current = "";
                setDigits(Array(OTP_LENGTH).fill(""));
                setResetKey((value) => value + 1);
                setShaking(true);
                window.setTimeout(() => setShaking(false), 400);
            }
        } finally {
            setVerifying(false);
        }
    }

    function onDigitsChange(next: string[]) {
        setDigits(next);
        setError("");
        const code = next.join("");
        if (
            email &&
            !locked &&
            !expired &&
            !verifying &&
            !verified &&
            /^[0-9]{6}$/.test(code) &&
            code !== lastSubmittedCode.current
        ) {
            lastSubmittedCode.current = code;
            void verify(code);
        }
    }

    async function onSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const code = digits.join("");
        if (
            !/^[0-9]{6}$/.test(code) ||
            !email ||
            verifying ||
            locked ||
            expired ||
            code === lastSubmittedCode.current
        )
            return;
        lastSubmittedCode.current = code;
        await verify(code);
    }

    async function onResend() {
        if (!email || cooldown || resending) return;
        setError("");
        setNotice("");
        setResending(true);
        try {
            const result = await resendParkWiseOtp(email);
            if (!result.otpSent) {
                setError("This email is not waiting for verification.");
                return;
            }
            setLocked(false);
            lastSubmittedCode.current = "";
            setDigits(Array(OTP_LENGTH).fill(""));
            setResetKey((value) => value + 1);
            const seconds = Math.max(0, result.cooldownSeconds);
            setCooldown(seconds);
            rememberPendingVerificationCooldown(seconds);
            const expiration = Math.max(0, result.expiresInSeconds);
            setExpirationSeconds(expiration);
            rememberPendingVerificationExpiration(expiration);
            setNotice("A new code has been sent to your email.");
        } catch (failure) {
            const code =
                failure instanceof ApiError
                    ? failure.problem?.errorCode
                    : undefined;
            if (failure instanceof ApiError && failure.retryAfterSeconds) {
                setCooldown(failure.retryAfterSeconds);
                rememberPendingVerificationCooldown(failure.retryAfterSeconds);
            }
            setError(
                code === "identity.resend_rate_limit_exceeded"
                    ? "Too many codes requested. Please try again later."
                    : code === "identity.resend_cooldown_active"
                      ? "Please wait before requesting another code."
                      : code === "identity.account_not_pending"
                        ? "This account is not waiting for verification."
                        : "We couldn't request a new code. Please try again.",
            );
        } finally {
            setResending(false);
        }
    }

    return (
        <ParkWiseAuthLayout
            fontClassName={fontClassName}
            className="parkwise-otp-page"
        >
            <aside className="parkwise-login-aside parkwise-otp-aside">
                <div className="parkwise-welcome">
                    <h1>Almost there!</h1>
                    <p className="parkwise-login-description">
                        Verify your email to start your smarter parking journey.
                    </p>
                </div>
                <Image
                    src="/images/parkwise/login-parking-illustration.png"
                    alt="Car beside a parking availability map"
                    width={1536}
                    height={1024}
                    sizes="(max-width: 767px) 90vw, 480px"
                    className="parkwise-login-illustration"
                />
            </aside>

            <div className="parkwise-login-form-panel parkwise-otp-form-panel">
                {verified ? (
                    <div className="parkwise-otp-verified" role="status">
                        <h2>Email verified</h2>
                        <p>Your email is verified. You can now log in.</p>
                        <p role="timer">
                            Redirecting to Login in {redirectSeconds} seconds...
                        </p>
                        <Link href="/login" className="parkwise-login-submit">
                            Go to Login
                        </Link>
                    </div>
                ) : !email ? (
                    <div className="parkwise-otp-empty" role="status">
                        <svg
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="1.8"
                            aria-hidden="true"
                        >
                            <rect x="3" y="5" width="18" height="14" rx="2" />
                            <path d="m3 7 9 6 9-6M16 16l5 5m0-5-5 5" />
                        </svg>
                        <h2>No verification request found</h2>
                        <p>
                            Please complete sign up to receive your verification
                            code.
                        </p>
                        <Link href="/sign-up" className="parkwise-login-submit">
                            Back to Sign Up
                        </Link>
                    </div>
                ) : (
                    <form noValidate onSubmit={onSubmit}>
                        <h2>Verify Your Email</h2>
                        <p className="parkwise-otp-instruction">
                            Enter the 6-digit code sent to{" "}
                            <strong>{email}</strong>.
                        </p>
                        <p
                            className={`parkwise-otp-expiry${expired ? " is-expired" : ""}`}
                            role="timer"
                            aria-live="off"
                        >
                            {expirationSeconds === null ? (
                                "Codes are valid for 5 minutes. Request a new code if yours has expired."
                            ) : expired ? (
                                "Code expired. Request a new code to continue."
                            ) : (
                                <>
                                    Code expires in{" "}
                                    <strong>
                                        {formatRemaining(expirationSeconds)}
                                    </strong>
                                </>
                            )}
                        </p>
                        <label
                            className="parkwise-visually-hidden"
                            id="parkwise-otp-label"
                        >
                            6-digit verification code
                        </label>
                        <SegmentedOtpInput
                            value={digits}
                            onChange={onDigitsChange}
                            error={error}
                            disabled={verifying || locked || expired}
                            resetKey={resetKey}
                            shaking={shaking}
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
                            disabled={
                                !complete || verifying || locked || expired
                            }
                        >
                            {verifying ? "Verifying..." : "Verify Code"}
                        </button>
                        <p className="parkwise-otp-help">
                            Need a new code?{" "}
                            <button
                                type="button"
                                onClick={onResend}
                                disabled={resending || cooldown > 0}
                            >
                                {resending
                                    ? "Sending..."
                                    : cooldown > 0
                                      ? `Resend in ${cooldown}s`
                                      : "Resend code"}
                            </button>
                        </p>
                        {notice && (
                            <p className="parkwise-login-notice" role="status">
                                {notice}
                            </p>
                        )}
                    </form>
                )}
            </div>
        </ParkWiseAuthLayout>
    );
}
