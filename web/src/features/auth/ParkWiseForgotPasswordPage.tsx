"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ParkWiseAuthLayout } from "./ParkWiseAuthLayout";
import { ParkWiseRecoveryAside } from "./ParkWiseRecoveryAside";
import { requestPasswordReset } from "./requestPasswordReset";
import { ApiError } from "@/shared/errors/api-error";
import { SegmentedOtpInput } from "./SegmentedOtpInput";
import {
    clearPendingPasswordReset,
    rememberPendingPasswordReset,
} from "./pendingPasswordReset";

const forgotPasswordSchema = z.object({
    email: z
        .string()
        .trim()
        .min(1, "Email is required")
        .email("Enter a valid email address")
        .max(254, "Email is too long"),
});

type ForgotPasswordValues = z.infer<typeof forgotPasswordSchema>;

function resetRequestErrorMessage(error: unknown): string {
    if (error instanceof ApiError && error.status === 429) {
        return error.retryAfterSeconds
            ? `Please wait ${error.retryAfterSeconds} seconds before requesting another code.`
            : "Too many requests. Please try again later.";
    }
    if (error instanceof ApiError && error.status === 503) {
        return "Password reset is temporarily unavailable. Please try again later.";
    }
    if (error instanceof ApiError) {
        return "We couldn't send reset instructions. Please try again.";
    }
    return "We couldn't connect. Check your connection and try again.";
}

function resetCodeDeadline(expiresInSeconds: number): number {
    return Date.now() + Math.max(0, expiresInSeconds) * 1000;
}

export function ParkWiseForgotPasswordPage({
    fontClassName,
}: {
    fontClassName: string;
}) {
    const router = useRouter();
    const [submitError, setSubmitError] = useState("");
    const [submittedEmail, setSubmittedEmail] = useState("");
    const [otpDigits, setOtpDigits] = useState<string[]>(() =>
        Array(6).fill(""),
    );
    const [otpError, setOtpError] = useState("");
    const [expiresAt, setExpiresAt] = useState(0);
    const [expiresIn, setExpiresIn] = useState(0);
    const [requestInFlight, setRequestInFlight] = useState(false);
    const [resendError, setResendError] = useState("");
    const [resendNotice, setResendNotice] = useState("");
    const [resendInFlight, setResendInFlight] = useState(false);
    const [cooldown, setCooldown] = useState(0);

    useEffect(() => {
        if (cooldown <= 0) return;
        const timer = window.setTimeout(
            () => setCooldown((value) => value - 1),
            1000,
        );
        return () => window.clearTimeout(timer);
    }, [cooldown]);
    useEffect(() => {
        if (!expiresAt) return;
        const update = () =>
            setExpiresIn(
                Math.max(0, Math.ceil((expiresAt - Date.now()) / 1000)),
            );
        update();
        const timer = window.setInterval(update, 1000);
        return () => window.clearInterval(timer);
    }, [expiresAt]);
    const {
        register,
        handleSubmit,
        formState: { errors, isSubmitting },
    } = useForm<ForgotPasswordValues>({
        resolver: zodResolver(forgotPasswordSchema),
        defaultValues: { email: "" },
    });

    async function onSubmit({ email }: ForgotPasswordValues) {
        if (requestInFlight) return;
        setRequestInFlight(true);
        setSubmitError("");
        try {
            const result = await requestPasswordReset(email);
            clearPendingPasswordReset();
            setSubmittedEmail(email);
            setCooldown(Math.max(0, result.cooldownSeconds));
            setExpiresAt(resetCodeDeadline(result.expiresInSeconds));
        } catch (error) {
            if (error instanceof ApiError && error.retryAfterSeconds)
                setCooldown(error.retryAfterSeconds);
            setSubmitError(resetRequestErrorMessage(error));
        } finally {
            setRequestInFlight(false);
        }
    }

    async function onResend() {
        if (resendInFlight || !submittedEmail || cooldown > 0) return;
        setResendInFlight(true);
        setResendError("");
        setResendNotice("");
        try {
            const result = await requestPasswordReset(submittedEmail);
            clearPendingPasswordReset();
            setOtpDigits(Array(6).fill(""));
            setOtpError("");
            setCooldown(Math.max(0, result.cooldownSeconds));
            setExpiresAt(resetCodeDeadline(result.expiresInSeconds));
            setResendNotice("If an account exists, a new code is on its way.");
        } catch (error) {
            if (error instanceof ApiError && error.retryAfterSeconds)
                setCooldown(error.retryAfterSeconds);
            setResendError(resetRequestErrorMessage(error));
        } finally {
            setResendInFlight(false);
        }
    }

    function onContinue() {
        const otpCode = otpDigits.join("");
        if (!/^[0-9]{6}$/.test(otpCode)) {
            setOtpError("Enter the six-digit code from your email.");
            return;
        }
        if (expiresIn <= 0) {
            setOtpError("This code has expired. Request a new code.");
            return;
        }
        try {
            rememberPendingPasswordReset(submittedEmail, otpCode, expiresAt);
            router.push("/reset-password");
        } catch {
            setOtpError(
                "We couldn't continue. Allow session storage and try again.",
            );
        }
    }

    return (
        <ParkWiseAuthLayout
            fontClassName={fontClassName}
            className="parkwise-forgot-page"
        >
            <ParkWiseRecoveryAside />

            <div className="parkwise-login-form-panel parkwise-forgot-form-panel">
                {submittedEmail ? (
                    <div className="parkwise-forgot-success">
                        <h2>Check your email</h2>
                        <p>
                            If this account exists, a password reset code has
                            been sent.
                        </p>
                        <p>
                            Enter the six-digit code sent to{" "}
                            <strong>{submittedEmail}</strong>.
                        </p>
                        <p role="timer" aria-live="off">
                            {expiresIn > 0
                                ? `Code expires in ${String(Math.floor(expiresIn / 60)).padStart(2, "0")}:${String(expiresIn % 60).padStart(2, "0")}`
                                : "Code expired. Request a new code."}
                        </p>
                        <div className="parkwise-forgot-code-field">
                            <span id="parkwise-forgot-code-label">
                                Six-digit code
                            </span>
                            <SegmentedOtpInput
                                value={otpDigits}
                                onChange={(digits) => {
                                    setOtpDigits(digits);
                                    setOtpError("");
                                }}
                                error={otpError}
                                labelId="parkwise-forgot-code-label"
                                errorId="parkwise-forgot-code-error"
                                disabled={expiresIn <= 0}
                            />
                            {otpError && (
                                <p id="parkwise-forgot-code-error" role="alert">
                                    {otpError}
                                </p>
                            )}
                        </div>
                        <button
                            type="button"
                            onClick={onContinue}
                            className="parkwise-login-submit"
                            disabled={
                                expiresIn <= 0 || !otpDigits.every(Boolean)
                            }
                        >
                            Continue
                        </button>
                        <p className="parkwise-forgot-resend-row">
                            Didn&apos;t receive the email?{" "}
                            <button
                                type="button"
                                className="parkwise-forgot-resend"
                                onClick={onResend}
                                disabled={resendInFlight || cooldown > 0}
                            >
                                {resendInFlight
                                    ? "Sending..."
                                    : cooldown > 0
                                      ? `Resend in ${cooldown}s`
                                      : "Resend code"}
                            </button>
                        </p>
                        {resendNotice && (
                            <p className="parkwise-login-notice" role="status">
                                {resendNotice}
                            </p>
                        )}
                        {resendError && (
                            <p className="parkwise-login-error" role="alert">
                                {resendError}
                            </p>
                        )}
                    </div>
                ) : (
                    <form noValidate onSubmit={handleSubmit(onSubmit)}>
                        <h2>Forgot your password?</h2>
                        <p className="parkwise-forgot-description">
                            Enter the email associated with your account.
                            We&apos;ll send a six-digit code to reset your
                            password.
                        </p>
                        <div className="parkwise-login-field">
                            <label htmlFor="parkwise-forgot-email">Email</label>
                            <input
                                id="parkwise-forgot-email"
                                type="email"
                                autoComplete="email"
                                placeholder="Enter email"
                                aria-invalid={Boolean(errors.email)}
                                aria-describedby={
                                    errors.email
                                        ? "parkwise-forgot-email-error"
                                        : undefined
                                }
                                {...register("email", {
                                    onChange: () => setSubmitError(""),
                                })}
                            />
                            {errors.email && (
                                <p
                                    id="parkwise-forgot-email-error"
                                    role="alert"
                                >
                                    {errors.email.message}
                                </p>
                            )}
                        </div>
                        <button
                            className="parkwise-login-submit"
                            type="submit"
                            disabled={isSubmitting || requestInFlight}
                        >
                            {isSubmitting || requestInFlight
                                ? "Sending..."
                                : "Send Reset Code"}
                        </button>
                        {submitError && (
                            <p
                                className="parkwise-login-error"
                                role="alert"
                                aria-live="polite"
                            >
                                {submitError}
                            </p>
                        )}
                        <Link href="/login" className="parkwise-forgot-back">
                            Back to Login
                        </Link>
                    </form>
                )}
            </div>
        </ParkWiseAuthLayout>
    );
}
