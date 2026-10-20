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
import { resetPassword } from "./changePassword";
import { passwordSchema } from "./passwordSchema";

const resetSchema = z
    .object({
        email: z
            .string()
            .trim()
            .min(1, "Email is required")
            .email("Enter a valid email address")
            .max(254, "Email is too long"),
        otpCode: z
            .string()
            .regex(/^[0-9]{6}$/, "Enter the six-digit code from your email"),
        newPassword: passwordSchema,
        confirmPassword: z.string().min(1, "Confirm your new password"),
    })
    .refine((values) => values.newPassword === values.confirmPassword, {
        message: "The passwords do not match",
        path: ["confirmPassword"],
    });

type ResetValues = z.infer<typeof resetSchema>;
type View = "form" | "success";

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
    email,
}: {
    fontClassName: string;
    email: string;
}) {
    const router = useRouter();
    const [view, setView] = useState<View>("form");
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
        defaultValues: {
            email,
            otpCode: "",
            newPassword: "",
            confirmPassword: "",
        },
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

    async function onSubmit({
        email: accountEmail,
        otpCode,
        newPassword,
    }: ResetValues) {
        setSubmitError("");
        try {
            await resetPassword(accountEmail.trim(), otpCode, newPassword);
            setSecondsRemaining(5);
            setView("success");
        } catch (error) {
            if (error instanceof ApiError) {
                const code = error.problem?.errorCode;
                setSubmitError(
                    code === "identity.invalid_otp_code"
                        ? "Incorrect code. Please check it and try again."
                        : code === "identity.otp_expired" ||
                            code === "identity.otp_max_attempts_exceeded"
                          ? "This code is no longer valid. Request a new code."
                          : code === "identity.password_same_as_old"
                            ? "Choose a password different from your current one."
                            : error.status === 400
                              ? "Check your details and try again."
                              : "We couldn't change your password. Please try again later.",
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
                {view === "success" ? (
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
                            Enter the code from your email and choose a strong
                            password.
                        </p>
                        <div className="parkwise-login-field">
                            <label htmlFor="parkwise-reset-email">Email</label>
                            <input
                                id="parkwise-reset-email"
                                type="email"
                                autoComplete="email"
                                aria-invalid={Boolean(errors.email)}
                                {...register("email")}
                            />
                            {errors.email && (
                                <p role="alert">{errors.email.message}</p>
                            )}
                        </div>
                        <div className="parkwise-login-field">
                            <label htmlFor="parkwise-reset-code">
                                Six-digit code
                            </label>
                            <input
                                id="parkwise-reset-code"
                                type="text"
                                inputMode="numeric"
                                autoComplete="one-time-code"
                                maxLength={6}
                                aria-invalid={Boolean(errors.otpCode)}
                                {...register("otpCode")}
                            />
                            {errors.otpCode && (
                                <p role="alert">{errors.otpCode.message}</p>
                            )}
                        </div>
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
                        <Link
                            href="/forgot-password"
                            className="parkwise-forgot-back"
                        >
                            Request a new code
                        </Link>
                        <Link href="/login" className="parkwise-forgot-back">
                            Back to Login
                        </Link>
                    </form>
                )}
            </div>
        </ParkWiseAuthLayout>
    );
}
