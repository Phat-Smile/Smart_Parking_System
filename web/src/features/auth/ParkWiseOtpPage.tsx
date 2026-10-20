"use client";

import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import type { FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ParkWiseAuthAside, ParkWiseAuthLayout } from "./ParkWiseAuthLayout";
import { SegmentedOtpInput } from "./SegmentedOtpInput";
import {
    clearPendingVerification,
    readPendingVerification,
    readPendingVerificationCooldown,
    rememberPendingVerificationCooldown,
} from "./pendingVerification";
import { resendParkWiseOtp, verifyParkWiseEmail } from "./emailVerificationApi";
import { ApiError } from "@/shared/errors/api-error";

// The verification UI uses the six-digit code from the registration flow.
const OTP_LENGTH = 6;
const subscribePendingVerification = () => () => {};

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
    const [verified, setVerified] = useState(false);
    const [locked, setLocked] = useState(false);
    const [resetKey, setResetKey] = useState(0);
    const [shaking, setShaking] = useState(false);
    const complete = digits.every(Boolean);

    useEffect(() => {
        const timer = window.setTimeout(
            () => setCooldown(readPendingVerificationCooldown()),
            0,
        );
        return () => window.clearTimeout(timer);
    }, []);

    useEffect(() => {
        if (cooldown <= 0) return;
        const timer = window.setTimeout(
            () => setCooldown((value) => value - 1),
            1000,
        );
        return () => window.clearTimeout(timer);
    }, [cooldown]);

    useEffect(() => {
        if (!verified) return;
        const timer = window.setTimeout(() => router.replace("/login"), 1500);
        return () => window.clearTimeout(timer);
    }, [verified, router]);

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
                    ? "Incorrect code. Please check it and try again."
                    : code === "identity.otp_max_attempts_exceeded"
                      ? "Too many attempts. Request a new code."
                      : code === "identity.otp_expired"
                        ? "This code has expired. Request a new code."
                        : "We couldn't verify this code. Please try again.",
            );
            if (code === "identity.otp_max_attempts_exceeded") {
                setLocked(true);
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
                {verified ? (
                    <div className="parkwise-otp-verified" role="status">
                        <h2>Email verified</h2>
                        <p>Account activated. Redirecting to login...</p>
                        <Link href="/login" className="parkwise-login-submit">
                            Go to Login
                        </Link>
                    </div>
                ) : (
                    <form noValidate onSubmit={onSubmit}>
                        <h2>Verify Your Email</h2>
                        <div
                            className="parkwise-otp-session-note"
                            role="status"
                        >
                            {email ? (
                                <p>Enter the verification code for {email}.</p>
                            ) : (
                                <>
                                    <p>
                                        No active verification request was
                                        found.
                                    </p>
                                    <p>
                                        Complete sign up to receive a code by
                                        email.
                                    </p>
                                </>
                            )}
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
                            disabled={verifying || locked}
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
                                !complete || !email || verifying || locked
                            }
                        >
                            {verifying ? "Verifying..." : "Verify Code"}
                        </button>
                        <p className="parkwise-otp-help">
                            Need a new code?{" "}
                            {email ? (
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
                            ) : (
                                <Link href="/sign-up">Return to sign up</Link>
                            )}
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
