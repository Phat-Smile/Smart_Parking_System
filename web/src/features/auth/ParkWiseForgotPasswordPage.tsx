"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ParkWiseAuthLayout } from "./ParkWiseAuthLayout";
import { ParkWiseRecoveryAside } from "./ParkWiseRecoveryAside";
import { requestPasswordReset } from "./requestPasswordReset";
import { ApiError } from "@/shared/errors/api-error";

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

export function ParkWiseForgotPasswordPage({
    fontClassName,
}: {
    fontClassName: string;
}) {
    const [submitError, setSubmitError] = useState("");
    const [submittedEmail, setSubmittedEmail] = useState("");
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
            setSubmittedEmail(email);
            setCooldown(Math.max(0, result.cooldownSeconds));
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
            setCooldown(Math.max(0, result.cooldownSeconds));
            setResendNotice("If an account exists, a new code is on its way.");
        } catch (error) {
            if (error instanceof ApiError && error.retryAfterSeconds)
                setCooldown(error.retryAfterSeconds);
            setResendError(resetRequestErrorMessage(error));
        } finally {
            setResendInFlight(false);
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
                    <div
                        className="parkwise-forgot-success"
                        role="status"
                        aria-live="polite"
                    >
                        <h2>Check your email</h2>
                        <p>
                            If this account exists, a password reset code has
                            been sent.
                        </p>
                        <p>
                            Check your inbox, then enter the six-digit code to
                            create a new password.
                        </p>
                        <Link
                            href={`/reset-password?email=${encodeURIComponent(submittedEmail)}`}
                            className="parkwise-login-submit parkwise-forgot-login-link"
                        >
                            Enter Reset Code
                        </Link>
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
