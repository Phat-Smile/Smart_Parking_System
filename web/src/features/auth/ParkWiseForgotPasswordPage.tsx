"use client";

import { useState } from "react";
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
        .email("Enter a valid email address"),
});

type ForgotPasswordValues = z.infer<typeof forgotPasswordSchema>;

function resetRequestErrorMessage(error: unknown): string {
    if (
        error instanceof Error &&
        error.message === "Password reset is not configured yet."
    ) {
        return "Password reset is not available yet. Please try again later.";
    }
    if (error instanceof ApiError && error.status === 429) {
        return "Too many requests. Please try again later.";
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
            await requestPasswordReset(email);
            setSubmittedEmail(email);
        } catch (error) {
            setSubmitError(resetRequestErrorMessage(error));
        } finally {
            setRequestInFlight(false);
        }
    }

    async function onResend() {
        if (resendInFlight || !submittedEmail) return;
        setResendInFlight(true);
        setResendError("");
        setResendNotice("");
        try {
            await requestPasswordReset(submittedEmail);
            setResendNotice("If an account exists, a new link is on its way.");
        } catch (error) {
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
                            We&apos;ve sent a password reset link to your email.
                        </p>
                        <p>
                            Please check your inbox and follow the link to
                            create a new password.
                        </p>
                        <Link
                            href="/login"
                            className="parkwise-login-submit parkwise-forgot-login-link"
                        >
                            Back to Login
                        </Link>
                        <p className="parkwise-forgot-resend-row">
                            Didn&apos;t receive the email?{" "}
                            <button
                                type="button"
                                className="parkwise-forgot-resend"
                                onClick={onResend}
                                disabled={resendInFlight}
                            >
                                {resendInFlight ? "Sending..." : "Resend link"}
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
                            We&apos;ll send a link to reset your password.
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
                                : "Send Reset Link"}
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
