"use client";

import { useState } from "react";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { AuthGlyph } from "./AuthGlyph";

const schema = z.object({
    email: z
        .string()
        .trim()
        .min(1, "Email is required")
        .email("Enter a valid email address"),
    password: z.string().min(1, "Password is required"),
    rememberMe: z.boolean(),
});

type Values = z.infer<typeof schema>;

function GoogleMark() {
    return (
        <svg viewBox="0 0 24 24" className="h-4 w-4" aria-hidden="true">
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
        formState: { errors },
    } = useForm<Values>({
        resolver: zodResolver(schema),
        defaultValues: { email: "", password: "", rememberMe: true },
    });

    return (
        <div className="relative z-10 flex w-full flex-col justify-center rounded-2xl border border-[var(--login-border)] bg-[var(--login-panel)] p-[26px] shadow-xl backdrop-blur-2xl lg:col-span-5 lg:h-full">
            <div>
                <Link
                    href="/"
                    className="mb-3 inline-flex h-8 items-center gap-1.5 rounded-full border border-[var(--login-back-border)] bg-[var(--login-back-bg)] px-3 text-[13px] font-medium text-[var(--login-back-text)] hover:bg-[var(--login-back-hover)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--login-accent)]"
                >
                    <span aria-hidden="true" className="text-sm leading-none">
                        ←
                    </span>
                    Back to Home
                </Link>
                <h1 className="font-heading text-[36px] leading-[1.08] font-bold tracking-tight text-[var(--login-text)] lg:text-[38px]">
                    Sign in to your{" "}
                    <span className="text-[var(--login-heading-accent)]">
                        account
                    </span>
                </h1>
            </div>

            <form
                method="post"
                noValidate
                onSubmit={handleSubmit(() =>
                    setNotice(
                        "Sign-in is not available yet. The Identity login service has not been connected.",
                    ),
                )}
                className="my-8 flex flex-col gap-4"
            >
                <div className="flex flex-col gap-1.5">
                    <label
                        htmlFor="login-email"
                        className="text-xs leading-none font-semibold tracking-wide text-[var(--login-label)] uppercase"
                    >
                        Email Address
                    </label>
                    <div className="relative flex items-center">
                        <AuthGlyph
                            name="mail"
                            className="pointer-events-none absolute left-3.5 h-[18px] w-[18px] text-[var(--login-muted)]"
                        />
                        <input
                            id="login-email"
                            type="email"
                            autoComplete="username"
                            placeholder="you@company.com"
                            aria-invalid={Boolean(errors.email)}
                            aria-describedby={
                                errors.email ? "login-email-error" : undefined
                            }
                            className="h-12 w-full rounded-xl border border-transparent bg-[var(--login-input)] pr-3 pl-10 text-sm text-[var(--login-text)] placeholder:text-[var(--login-placeholder)] focus:border-[var(--login-accent)] focus:outline-none focus:ring-2 focus:ring-[var(--login-accent)]/20"
                            {...register("email")}
                        />
                    </div>
                    {errors.email && (
                        <p
                            id="login-email-error"
                            role="alert"
                            className="text-xs text-red-400"
                        >
                            {errors.email.message}
                        </p>
                    )}
                </div>

                <div className="flex flex-col gap-1.5">
                    <label
                        htmlFor="login-password"
                        className="text-xs leading-none font-semibold tracking-wide text-[var(--login-label)] uppercase"
                    >
                        Password
                    </label>
                    <div className="relative flex items-center">
                        <AuthGlyph
                            name="lock"
                            className="pointer-events-none absolute left-3.5 h-[18px] w-[18px] text-[var(--login-muted)]"
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
                            className="h-12 w-full rounded-xl border border-transparent bg-[var(--login-input)] pr-10 pl-10 text-sm text-[var(--login-text)] placeholder:text-[var(--login-placeholder)] focus:border-[var(--login-accent)] focus:outline-none focus:ring-2 focus:ring-[var(--login-accent)]/20"
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
                            className="absolute right-3 rounded p-1 text-[var(--login-muted)] hover:text-[var(--login-text)] focus-visible:outline-2 focus-visible:outline-[var(--login-accent)]"
                        >
                            <AuthGlyph
                                name={passwordVisible ? "eye" : "eyeOff"}
                                className="h-[18px] w-[18px]"
                            />
                        </button>
                    </div>
                    {errors.password && (
                        <p
                            id="login-password-error"
                            role="alert"
                            className="text-xs text-red-400"
                        >
                            {errors.password.message}
                        </p>
                    )}
                </div>

                <div className="flex items-center justify-between gap-2 py-0.5">
                    <label className="flex cursor-pointer items-center gap-2 text-[13px] text-[var(--login-text)]">
                        <input
                            type="checkbox"
                            className="h-3.5 w-3.5 accent-[var(--login-accent)]"
                            {...register("rememberMe")}
                        />
                        Remember me
                    </label>
                    <button
                        type="button"
                        onClick={() =>
                            setNotice("Password reset is not available yet.")
                        }
                        className="text-[13px] font-medium text-[var(--login-back-text)] hover:underline focus-visible:outline-2 focus-visible:outline-[var(--login-accent)]"
                    >
                        Forgot password?
                    </button>
                </div>

                <button
                    type="submit"
                    className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-[#937AFF] to-[#4D36D0] font-heading text-sm font-semibold text-white shadow-md transition-opacity hover:opacity-95 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--login-accent)]"
                >
                    Log In{" "}
                    <AuthGlyph name="arrow" className="h-[18px] w-[18px]" />
                </button>

                <div className="flex items-center py-0.5" aria-hidden="true">
                    <span className="h-px flex-1 bg-[var(--login-divider)]" />
                    <span className="mx-3 text-[11px] font-medium tracking-wider text-[var(--login-label)] uppercase">
                        or continue with
                    </span>
                    <span className="h-px flex-1 bg-[var(--login-divider)]" />
                </div>

                <button
                    type="button"
                    onClick={() =>
                        setNotice("Google sign-in is not available yet.")
                    }
                    className="flex h-12 w-full items-center justify-center gap-2.5 rounded-xl border border-[var(--login-border)] bg-[var(--login-control)] font-heading text-[13px] font-medium text-[var(--login-text)] hover:bg-[var(--login-control-hover)] focus-visible:outline-2 focus-visible:outline-[var(--login-accent)]"
                >
                    <GoogleMark /> Continue with Google
                </button>
            </form>

            <div className="border-t border-[var(--login-border)] pt-4 text-center text-[13px] text-[var(--login-muted)]">
                Don&apos;t have an account?{" "}
                <button
                    type="button"
                    onClick={() =>
                        setNotice(
                            "Account registration UI is not available yet.",
                        )
                    }
                    className="ml-1 inline-flex items-center gap-0.5 font-semibold text-[var(--login-heading-accent)] hover:underline focus-visible:outline-2 focus-visible:outline-[var(--login-accent)]"
                >
                    Sign up <AuthGlyph name="arrow" className="h-3.5 w-3.5" />
                </button>
                {notice && (
                    <p
                        role="status"
                        aria-live="polite"
                        className="mt-1 text-xs text-[var(--login-muted)]"
                    >
                        {notice}
                    </p>
                )}
            </div>
        </div>
    );
}
