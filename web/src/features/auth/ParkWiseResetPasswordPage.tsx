"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ApiError } from "@/shared/errors/api-error";
import { ParkWiseAuthLayout } from "./ParkWiseAuthLayout";
import { ParkWiseRecoveryAside } from "./ParkWiseRecoveryAside";
import {
    changePassword,
    ResetEndpointUnavailableError,
} from "./changePassword";
import { passwordSchema } from "./passwordSchema";

const resetSchema = z
    .object({
        newPassword: passwordSchema,
        confirmPassword: z.string().min(1, "Confirm your new password"),
    })
    .refine((values) => values.newPassword === values.confirmPassword, {
        message: "The passwords do not match",
        path: ["confirmPassword"],
    });

type ResetValues = z.infer<typeof resetSchema>;
type View = "form" | "expired" | "success";

function EyeIcon({ visible }: { visible: boolean }) {
    return visible ? (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
        >
            <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z" />
            <circle cx="12" cy="12" r="3" />
        </svg>
    ) : (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
        >
            <path d="M3 3l18 18M10.6 6.1A10.8 10.8 0 0112 6c6.5 0 10 6 10 6a14.9 14.9 0 01-3 3.5M6.1 8.1A15 15 0 002 12s3.5 6 10 6a10.8 10.8 0 004-.8" />
            <path d="M10 10a3 3 0 004 4" />
        </svg>
    );
}

export function ParkWiseResetPasswordPage({
    fontClassName,
    token,
}: {
    fontClassName: string;
    token: string;
}) {
    const router = useRouter();
    const [view, setView] = useState<View>(token ? "form" : "expired");
    const [newPasswordVisible, setNewPasswordVisible] = useState(false);
    const [confirmPasswordVisible, setConfirmPasswordVisible] = useState(false);
    const [submitError, setSubmitError] = useState("");
    const [secondsRemaining, setSecondsRemaining] = useState(5);
    const {
        register,
        handleSubmit,
        formState: { errors, isSubmitting },
    } = useForm<ResetValues>({
        resolver: zodResolver(resetSchema),
        defaultValues: { newPassword: "", confirmPassword: "" },
    });

    useEffect(() => {
        if (view !== "success") return;
        if (secondsRemaining === 0) {
            router.replace("/login");
            return;
        }
        const timeout = window.setTimeout(
            () => setSecondsRemaining((seconds) => seconds - 1),
            1000,
        );
        return () => window.clearTimeout(timeout);
    }, [router, secondsRemaining, view]);

    async function onSubmit({ newPassword }: ResetValues) {
        if (!token) {
            setView("expired");
            return;
        }
        setSubmitError("");
        try {
            await changePassword(token, newPassword);
            setSecondsRemaining(5);
            setView("success");
        } catch (error) {
            if (
                error instanceof ApiError &&
                [401, 410].includes(error.status)
            ) {
                setView("expired");
            } else if (
                error instanceof ResetEndpointUnavailableError ||
                (error instanceof ApiError && error.status === 404)
            ) {
                setSubmitError(
                    "Password reset is not available yet. Please try again later.",
                );
            } else if (error instanceof ApiError && error.status === 422) {
                setSubmitError(
                    "The password was rejected. Check the requirements and try again.",
                );
            } else {
                setSubmitError(
                    "We couldn't change your password. Please try again later.",
                );
            }
        }
    }

    return (
        <ParkWiseAuthLayout
            fontClassName={fontClassName}
            className="parkwise-forgot-page parkwise-reset-page"
        >
            <ParkWiseRecoveryAside />
            <div className="parkwise-login-form-panel parkwise-reset-form-panel">
                {view === "expired" ? (
                    <div className="parkwise-reset-state" role="status">
                        <h2>Reset link expired</h2>
                        <p>
                            This password reset link has expired or is no longer
                            valid.
                        </p>
                        <Link
                            href="/forgot-password"
                            className="parkwise-login-submit parkwise-forgot-login-link"
                        >
                            Request New Link
                        </Link>
                    </div>
                ) : view === "success" ? (
                    <div
                        className="parkwise-reset-state"
                        role="status"
                        aria-live="polite"
                    >
                        <span
                            className="parkwise-reset-check"
                            aria-hidden="true"
                        >
                            ✓
                        </span>
                        <h2>Password changed successfully!</h2>
                        <p>Your password has been updated successfully.</p>
                        <p>
                            Redirecting you to Login in {secondsRemaining}{" "}
                            seconds...
                        </p>
                        <Link
                            href="/login"
                            className="parkwise-login-submit parkwise-forgot-login-link"
                        >
                            Go to Login
                        </Link>
                    </div>
                ) : (
                    <form noValidate onSubmit={handleSubmit(onSubmit)}>
                        <h2>Create a new password</h2>
                        <p className="parkwise-reset-description">
                            Choose a strong password for your ParkWise account.
                        </p>
                        <div className="parkwise-login-field">
                            <label htmlFor="parkwise-reset-new-password">
                                New Password
                            </label>
                            <div className="parkwise-password-control">
                                <input
                                    id="parkwise-reset-new-password"
                                    type={
                                        newPasswordVisible ? "text" : "password"
                                    }
                                    autoComplete="new-password"
                                    placeholder="Enter new password"
                                    aria-invalid={Boolean(errors.newPassword)}
                                    aria-describedby={
                                        errors.newPassword
                                            ? "parkwise-reset-new-password-error"
                                            : undefined
                                    }
                                    {...register("newPassword", {
                                        onChange: () => setSubmitError(""),
                                    })}
                                />
                                <button
                                    type="button"
                                    aria-label={
                                        newPasswordVisible
                                            ? "Hide new password"
                                            : "Show new password"
                                    }
                                    onClick={() =>
                                        setNewPasswordVisible(
                                            (visible) => !visible,
                                        )
                                    }
                                >
                                    <EyeIcon visible={newPasswordVisible} />
                                </button>
                            </div>
                            {errors.newPassword && (
                                <p
                                    id="parkwise-reset-new-password-error"
                                    role="alert"
                                >
                                    {errors.newPassword.message}
                                </p>
                            )}
                        </div>
                        <div className="parkwise-login-field">
                            <label htmlFor="parkwise-reset-confirm-password">
                                Confirm New Password
                            </label>
                            <div className="parkwise-password-control">
                                <input
                                    id="parkwise-reset-confirm-password"
                                    type={
                                        confirmPasswordVisible
                                            ? "text"
                                            : "password"
                                    }
                                    autoComplete="new-password"
                                    placeholder="Confirm new password"
                                    aria-invalid={Boolean(
                                        errors.confirmPassword,
                                    )}
                                    aria-describedby={
                                        errors.confirmPassword
                                            ? "parkwise-reset-confirm-password-error"
                                            : undefined
                                    }
                                    {...register("confirmPassword", {
                                        onChange: () => setSubmitError(""),
                                    })}
                                />
                                <button
                                    type="button"
                                    aria-label={
                                        confirmPasswordVisible
                                            ? "Hide confirm password"
                                            : "Show confirm password"
                                    }
                                    onClick={() =>
                                        setConfirmPasswordVisible(
                                            (visible) => !visible,
                                        )
                                    }
                                >
                                    <EyeIcon visible={confirmPasswordVisible} />
                                </button>
                            </div>
                            {errors.confirmPassword && (
                                <p
                                    id="parkwise-reset-confirm-password-error"
                                    role="alert"
                                >
                                    {errors.confirmPassword.message}
                                </p>
                            )}
                        </div>
                        <button
                            className="parkwise-login-submit"
                            type="submit"
                            disabled={isSubmitting}
                        >
                            {isSubmitting
                                ? "Changing password..."
                                : "Change Password"}
                        </button>
                        {submitError && (
                            <p className="parkwise-login-error" role="alert">
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
