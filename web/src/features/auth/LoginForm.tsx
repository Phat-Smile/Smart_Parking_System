"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { LoginIcon } from "./LoginIcon";
import { loginSchema, type LoginValues } from "./loginSchema";

const pendingMessage =
    "Sign-in is not available yet. The Identity login service has not been connected.";

function GoogleMark() {
    return (
        <svg
            viewBox="0 0 24 24"
            className="h-5 w-5 shrink-0"
            aria-hidden="true"
        >
            <path
                d="M23.745 12.27c0-.7-.06-1.4-.19-2.07H12v4.51h6.6c-.29 1.52-1.14 2.8-2.4 3.66v3.05h3.88c2.27-2.09 3.665-5.17 3.665-9.15z"
                fill="#4285F4"
            />
            <path
                d="M12 24c3.24 0 5.95-1.08 7.93-2.91l-3.88-3.05c-1.08.72-2.45 1.16-4.05 1.16-3.12 0-5.77-2.1-6.72-4.93H1.25v3.15C3.26 21.36 7.33 24 12 24z"
                fill="#34A853"
            />
            <path
                d="M5.28 14.27c-.25-.72-.38-1.49-.38-2.27s.13-1.55.38-2.27V6.58H1.25C.45 8.18 0 9.97 0 12s.45 3.82 1.25 5.42l4.03-3.15z"
                fill="#FBBC05"
            />
            <path
                d="M12 4.75c1.77 0 3.35.61 4.6 1.8l3.42-3.42C17.95 1.19 15.24 0 12 0 7.33 0 3.26 2.64 1.25 6.58l4.03 3.15c.95-2.83 3.6-4.98 6.72-4.98z"
                fill="#EA4335"
            />
        </svg>
    );
}

export function LoginForm() {
    const [passwordVisible, setPasswordVisible] = useState(false);
    const [notice, setNotice] = useState("");
    const {
        register,
        handleSubmit,
        formState: { errors, isSubmitting },
    } = useForm<LoginValues>({
        resolver: zodResolver(loginSchema),
        defaultValues: { email: "", password: "", rememberMe: true },
    });

    const showPending = (message = pendingMessage) => setNotice(message);

    return (
        <div className="relative z-10 flex flex-col justify-between rounded-3xl border border-[var(--auth-panel-border)] bg-[var(--auth-panel)] p-7 shadow-[var(--auth-panel-shadow)] backdrop-blur-2xl sm:p-10 lg:col-span-5">
            <div>
                <div className="mb-5 inline-flex w-fit items-center gap-2 rounded-full border border-[var(--auth-panel-border)] bg-[var(--auth-badge)] px-3 py-1.5 shadow-sm">
                    <LoginIcon
                        name="person"
                        className="h-[17px] w-[17px] text-[var(--auth-badge-text)]"
                    />
                    <span className="font-mono text-xs font-medium text-[var(--auth-badge-text)]">
                        Welcome back
                    </span>
                </div>
                <h1 className="mb-2 font-heading text-[28px] leading-9 font-bold tracking-[-0.02em] text-[var(--auth-text)] sm:text-[40px] sm:leading-[48px]">
                    Sign in to your{" "}
                    <span className="text-[var(--auth-accent)]">account</span>
                </h1>
                <p className="mb-7 text-[15px] leading-6 text-[var(--auth-text-secondary)]">
                    Access your dashboard to find, compare, and reserve parking
                    in seconds.
                </p>
            </div>

            <form
                noValidate
                onSubmit={handleSubmit(() => showPending())}
                className="flex flex-col gap-4"
            >
                <div className="flex flex-col gap-1.5">
                    <label
                        htmlFor="login-email"
                        className="font-mono text-xs font-medium tracking-[0.06em] text-[var(--auth-text-secondary)]"
                    >
                        Email Address
                    </label>
                    <div className="relative flex items-center">
                        <LoginIcon
                            name="mail"
                            className="pointer-events-none absolute left-3.5 h-5 w-5 text-[var(--auth-input-icon)]"
                        />
                        <input
                            id="login-email"
                            type="email"
                            autoComplete="username"
                            placeholder="Enter your email"
                            aria-invalid={Boolean(errors.email)}
                            aria-describedby={
                                errors.email ? "login-email-error" : undefined
                            }
                            className="h-12 w-full rounded-xl border border-[var(--auth-input-border)] bg-[var(--auth-input)] pr-4 pl-11 text-[15px] text-[var(--auth-text)] placeholder:text-[var(--auth-input-placeholder)] focus:border-[var(--auth-accent)] focus:outline-none focus:ring-2 focus:ring-[var(--auth-accent)]/30"
                            {...register("email")}
                        />
                    </div>
                    {errors.email && (
                        <p
                            id="login-email-error"
                            role="alert"
                            className="text-xs text-red-500"
                        >
                            {errors.email.message}
                        </p>
                    )}
                </div>

                <div className="flex flex-col gap-1.5">
                    <label
                        htmlFor="login-password"
                        className="font-mono text-xs font-medium tracking-[0.06em] text-[var(--auth-text-secondary)]"
                    >
                        Password
                    </label>
                    <div className="relative flex items-center">
                        <LoginIcon
                            name="lock"
                            className="pointer-events-none absolute left-3.5 h-5 w-5 text-[var(--auth-input-icon)]"
                        />
                        <input
                            id="login-password"
                            type={passwordVisible ? "text" : "password"}
                            autoComplete="current-password"
                            placeholder="Enter your password"
                            aria-invalid={Boolean(errors.password)}
                            aria-describedby={
                                errors.password
                                    ? "login-password-error"
                                    : undefined
                            }
                            className="h-12 w-full rounded-xl border border-[var(--auth-input-border)] bg-[var(--auth-input)] pr-11 pl-11 text-[15px] text-[var(--auth-text)] placeholder:text-[var(--auth-input-placeholder)] focus:border-[var(--auth-accent)] focus:outline-none focus:ring-2 focus:ring-[var(--auth-accent)]/30"
                            {...register("password")}
                        />
                        <button
                            type="button"
                            aria-label={
                                passwordVisible
                                    ? "Hide password"
                                    : "Show password"
                            }
                            aria-pressed={passwordVisible}
                            onClick={() =>
                                setPasswordVisible((visible) => !visible)
                            }
                            className="absolute right-3.5 rounded p-1 text-[var(--auth-input-icon)] hover:text-[var(--auth-accent)] focus-visible:outline-2 focus-visible:outline-[var(--auth-accent)]"
                        >
                            <LoginIcon
                                name={passwordVisible ? "eye" : "eyeOff"}
                                className="h-5 w-5"
                            />
                        </button>
                    </div>
                    {errors.password && (
                        <p
                            id="login-password-error"
                            role="alert"
                            className="text-xs text-red-500"
                        >
                            {errors.password.message}
                        </p>
                    )}
                </div>

                <div className="mt-1 mb-2 flex items-center justify-between gap-3">
                    <label className="flex cursor-pointer items-center gap-2.5 font-mono text-xs text-[var(--auth-text-secondary)]">
                        <input
                            type="checkbox"
                            className="h-4 w-4 accent-[var(--auth-accent)]"
                            {...register("rememberMe")}
                        />
                        Remember me
                    </label>
                    <a
                        href="#login-pending"
                        onClick={(event) => {
                            event.preventDefault();
                            showPending("Password reset is not available yet.");
                        }}
                        className="font-mono text-xs text-[var(--auth-accent)] hover:underline focus-visible:outline-2 focus-visible:outline-[var(--auth-accent)]"
                    >
                        Forgot password?
                    </a>
                </div>

                <button
                    type="submit"
                    disabled={isSubmitting}
                    style={{
                        background: "var(--auth-accent-gradient)",
                        boxShadow: "var(--auth-accent-shadow)",
                    }}
                    className="flex h-12 w-full items-center justify-center gap-2 rounded-xl font-heading text-xl font-semibold text-white transition-opacity hover:opacity-95 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--auth-accent)] disabled:opacity-60"
                >
                    Log In{" "}
                    <LoginIcon name="arrow" className="h-[18px] w-[18px]" />
                </button>

                <div className="flex items-center py-2" aria-hidden="true">
                    <span className="h-px flex-1 bg-[var(--auth-panel-border)]" />
                    <span className="mx-4 font-mono text-[10px] font-semibold tracking-wider text-[var(--auth-text-muted)] uppercase">
                        or continue with
                    </span>
                    <span className="h-px flex-1 bg-[var(--auth-panel-border)]" />
                </div>

                <button
                    type="button"
                    onClick={() =>
                        showPending("Google sign-in is not available yet.")
                    }
                    className="flex h-12 w-full items-center justify-center gap-3 rounded-xl border border-[var(--auth-panel-border)] bg-[var(--auth-google)] font-heading text-xl font-semibold text-[var(--auth-text)] hover:border-[var(--auth-accent)] focus-visible:outline-2 focus-visible:outline-[var(--auth-accent)]"
                >
                    <GoogleMark /> Continue with Google
                </button>
            </form>

            <div className="mt-6 text-center text-[13px] leading-5 text-[var(--auth-text-muted)]">
                Don&apos;t have an account?{" "}
                <a
                    href="#login-pending"
                    onClick={(event) => {
                        event.preventDefault();
                        showPending(
                            "Account registration UI is not available yet.",
                        );
                    }}
                    className="ml-1 inline-flex items-center gap-1 font-heading text-xl font-semibold text-[var(--auth-accent)] hover:underline focus-visible:outline-2 focus-visible:outline-[var(--auth-accent)]"
                >
                    Sign up <LoginIcon name="arrow" className="h-4 w-4" />
                </a>
                {notice && (
                    <p
                        id="login-pending"
                        role="status"
                        className="mt-2 text-xs text-[var(--auth-text-secondary)]"
                        aria-live="polite"
                    >
                        {notice}
                    </p>
                )}
            </div>
        </div>
    );
}
