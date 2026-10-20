"use client";

import { useState, useSyncExternalStore } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ApiError } from "@/shared/errors/api-error";
import { ParkWiseAuthAside, ParkWiseAuthLayout } from "./ParkWiseAuthLayout";
import { getAuthSessionSnapshot, subscribeAuthSession } from "./authSession";
import { changePassword } from "./changePassword";
import { logoutParkWiseAccount } from "./logoutParkWiseAccount";
import { passwordSchema } from "./passwordSchema";

const schema = z
    .object({
        currentPassword: z.string().min(1, "Enter your current password"),
        newPassword: passwordSchema,
        confirmPassword: z.string().min(1, "Confirm your new password"),
    })
    .refine((value) => value.newPassword === value.confirmPassword, {
        path: ["confirmPassword"],
        message: "The passwords do not match",
    });
type Values = z.infer<typeof schema>;

export function SecuritySettingsPage({
    fontClassName,
}: {
    fontClassName: string;
}) {
    const router = useRouter();
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );
    const [notice, setNotice] = useState("");
    const [error, setError] = useState("");
    const [loggingOut, setLoggingOut] = useState(false);
    const {
        register,
        handleSubmit,
        reset,
        formState: { errors, isSubmitting },
    } = useForm<Values>({
        resolver: zodResolver(schema),
        defaultValues: {
            currentPassword: "",
            newPassword: "",
            confirmPassword: "",
        },
    });

    async function onSubmit(values: Values) {
        setNotice("");
        setError("");
        try {
            await changePassword(values.currentPassword, values.newPassword);
            reset();
            setNotice("Password changed successfully.");
        } catch (failure) {
            const code =
                failure instanceof ApiError
                    ? failure.problem?.errorCode
                    : undefined;
            setError(
                code === "identity.invalid_current_password"
                    ? "Current password is incorrect."
                    : code === "identity.password_same_as_old"
                      ? "Choose a password different from your current one."
                      : code === "identity.account_temporarily_locked"
                        ? `Account temporarily locked. Try again in ${failure instanceof ApiError ? (failure.retryAfterSeconds ?? 900) : 900} seconds.`
                        : "We couldn't change your password. Please try again.",
            );
        }
    }

    async function onLogout() {
        setLoggingOut(true);
        try {
            await logoutParkWiseAccount();
        } catch {
            /* Local session is cleared even if the request fails. */
        } finally {
            router.replace("/login");
        }
    }

    return (
        <ParkWiseAuthLayout
            fontClassName={fontClassName}
            className="parkwise-forgot-page"
        >
            <ParkWiseAuthAside description="Manage your account security and active session." />
            <div className="parkwise-login-form-panel parkwise-forgot-form-panel">
                <form noValidate onSubmit={handleSubmit(onSubmit)}>
                    <h2>Account security</h2>
                    {session ? (
                        <>
                            <p className="parkwise-forgot-description">
                                Signed in as {session.account.email}
                            </p>
                            <div className="parkwise-login-field">
                                <label htmlFor="security-current">
                                    Current password
                                </label>
                                <input
                                    id="security-current"
                                    type="password"
                                    autoComplete="current-password"
                                    aria-invalid={Boolean(
                                        errors.currentPassword,
                                    )}
                                    {...register("currentPassword")}
                                />
                                {errors.currentPassword && (
                                    <p role="alert">
                                        {errors.currentPassword.message}
                                    </p>
                                )}
                            </div>
                            <div className="parkwise-login-field">
                                <label htmlFor="security-new">
                                    New password
                                </label>
                                <input
                                    id="security-new"
                                    type="password"
                                    autoComplete="new-password"
                                    aria-invalid={Boolean(errors.newPassword)}
                                    {...register("newPassword")}
                                />
                                {errors.newPassword && (
                                    <p role="alert">
                                        {errors.newPassword.message}
                                    </p>
                                )}
                            </div>
                            <div className="parkwise-login-field">
                                <label htmlFor="security-confirm">
                                    Confirm new password
                                </label>
                                <input
                                    id="security-confirm"
                                    type="password"
                                    autoComplete="new-password"
                                    aria-invalid={Boolean(
                                        errors.confirmPassword,
                                    )}
                                    {...register("confirmPassword")}
                                />
                                {errors.confirmPassword && (
                                    <p role="alert">
                                        {errors.confirmPassword.message}
                                    </p>
                                )}
                            </div>
                            <button
                                className="parkwise-login-submit"
                                type="submit"
                                disabled={isSubmitting}
                            >
                                {isSubmitting ? "Saving..." : "Change password"}
                            </button>
                            {error && (
                                <p
                                    className="parkwise-login-error"
                                    role="alert"
                                >
                                    {error}
                                </p>
                            )}
                            {notice && (
                                <p
                                    className="parkwise-login-notice"
                                    role="status"
                                >
                                    {notice}
                                </p>
                            )}
                        </>
                    ) : (
                        <p className="parkwise-forgot-description">
                            Sign in to manage your account security.
                        </p>
                    )}
                </form>
                {session ? (
                    <button
                        type="button"
                        className="parkwise-forgot-back"
                        disabled={loggingOut}
                        onClick={onLogout}
                    >
                        {loggingOut ? "Signing out..." : "Log out"}
                    </button>
                ) : (
                    <Link href="/login" className="parkwise-forgot-back">
                        Go to Login
                    </Link>
                )}
            </div>
        </ParkWiseAuthLayout>
    );
}
