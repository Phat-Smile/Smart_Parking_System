"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm, useWatch } from "react-hook-form";
import { ApiError } from "@/shared/errors/api-error";
import { RegisterIcon } from "./RegisterIcons";
import { registerAccount } from "./registerApi";
import { registerSchema, type RegisterValues } from "./registerSchema";
import { rememberPendingVerification } from "../pendingVerification";
import { rememberRegisteredProfileName } from "../authSession";

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

const inputClass =
    "h-[35px] w-full rounded-lg border border-[var(--reg-input-border)] bg-[var(--reg-input)] pr-3 pl-8 text-[12.5px] text-[var(--reg-text)] placeholder:text-[var(--reg-muted)]/60 focus:border-[var(--reg-accent)] focus:outline-2 focus:outline-[var(--reg-accent)] sm:h-[35px] max-sm:h-11";
const labelClass =
    "mb-0.5 block font-mono text-[10.5px] font-semibold tracking-wider text-[var(--reg-muted)] uppercase";

export function RegisterForm() {
    const router = useRouter();
    const [passwordVisible, setPasswordVisible] = useState(false);
    const [confirmationVisible, setConfirmationVisible] = useState(false);
    const [notice, setNotice] = useState("");
    const [emailConflict, setEmailConflict] = useState(false);
    const {
        register,
        handleSubmit,
        setError,
        control,
        formState: { errors, isSubmitting },
    } = useForm<RegisterValues>({
        resolver: zodResolver(registerSchema),
        defaultValues: {
            fullName: "",
            phoneNumber: "",
            email: "",
            password: "",
            confirmPassword: "",
            agreeTerms: false,
        },
    });
    const password = useWatch({ control, name: "password" }) || "";

    const onSubmit = handleSubmit(async (values) => {
        setNotice("");
        setEmailConflict(false);
        try {
            const account = await registerAccount({
                email: values.email.trim(),
                password: values.password,
                fullName: values.fullName || null,
                phoneNumber: values.phoneNumber || null,
            });
            rememberRegisteredProfileName(account.email, account.fullName);
            if (!rememberPendingVerification(account.email, 60)) {
                setNotice(
                    "Account created. Open email verification in this browser to enter your code.",
                );
                return;
            }
            router.push("/verify-otp");
        } catch (error) {
            if (error instanceof ApiError) {
                const code = error.problem?.errorCode;
                setEmailConflict(code === "identity.email_already_registered");
                if (code === "identity.phone_already_registered") {
                    setError("phoneNumber", {
                        message: "This phone number is already registered.",
                    });
                }
                const message =
                    code === "identity.email_already_registered"
                        ? "This email address is already registered. Log in or reset your password."
                        : code === "identity.phone_already_registered"
                          ? "Check the phone number below."
                          : error.status === 400
                            ? "Check your details and try again."
                            : "Registration could not be completed. Please try again.";
                setNotice(
                    error.correlationId
                        ? `${message} Support ID: ${error.correlationId}`
                        : message,
                );
            } else {
                setNotice(
                    "Registration could not be completed. Please try again.",
                );
            }
        }
    });

    return (
        <section
            className="min-w-0 lg:col-span-5 lg:ml-auto lg:w-full lg:max-w-[450px]"
            aria-label="Account registration form"
        >
            <div className="flex min-h-[475px] w-full flex-col justify-between gap-3 rounded-2xl border border-[var(--reg-border)] bg-[var(--reg-panel)] p-4 shadow-2xl backdrop-blur-md">
                <div>
                    <Link
                        href="/"
                        className="inline-flex h-6 items-center gap-1 rounded-full border border-[var(--reg-border)] bg-[var(--reg-badge)] px-2.5 text-[11px] font-medium text-[var(--reg-muted)] hover:text-[var(--reg-text)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--reg-accent)]"
                    >
                        ← Back to Home
                    </Link>
                    <h2 className="font-heading mt-1 text-[21px] leading-tight font-bold tracking-tight">
                        Create account
                    </h2>
                    <p className="mt-0.5 text-[11.5px] text-[var(--reg-muted)]">
                        Join Smart Parking System and get started today.
                    </p>
                </div>

                <button
                    type="button"
                    onClick={() =>
                        setNotice("Google sign-up is not connected yet.")
                    }
                    className="flex h-9 w-full items-center justify-center gap-2 rounded-xl border border-[var(--reg-input-border)] bg-[var(--reg-input)] text-[12.5px] font-semibold shadow-sm hover:border-[var(--reg-accent)] focus-visible:outline-2 focus-visible:outline-[var(--reg-accent)]"
                >
                    <GoogleMark />
                    Continue with Google
                </button>

                <div
                    className="relative flex items-center justify-center"
                    aria-hidden="true"
                >
                    <div className="w-full border-t border-[var(--reg-border)]" />
                    <span className="absolute bg-[var(--reg-panel)] px-2 font-mono text-[10px] tracking-wider text-[var(--reg-muted)] uppercase">
                        or continue with email
                    </span>
                </div>

                <form
                    method="post"
                    noValidate
                    onSubmit={onSubmit}
                    className="flex flex-col gap-1.5"
                >
                    <div>
                        <label htmlFor="register-name" className={labelClass}>
                            Full Name (optional)
                        </label>
                        <div className="relative flex items-center">
                            <RegisterIcon
                                name="person"
                                className="pointer-events-none absolute left-2.5 h-[17px] w-[17px] text-[var(--reg-muted)]"
                            />
                            <input
                                id="register-name"
                                type="text"
                                autoComplete="name"
                                placeholder="Enter your full name"
                                aria-invalid={Boolean(errors.fullName)}
                                aria-describedby={
                                    errors.fullName
                                        ? "register-name-error"
                                        : "register-name-note"
                                }
                                className={inputClass}
                                {...register("fullName")}
                            />
                        </div>
                        {errors.fullName ? (
                            <p
                                id="register-name-error"
                                role="alert"
                                className="text-[11px] text-red-400"
                            >
                                {errors.fullName.message}
                            </p>
                        ) : (
                            <p
                                id="register-name-note"
                                className="text-[10px] text-[var(--reg-muted)]"
                            >
                                You can add or update your name later.
                            </p>
                        )}
                    </div>
                    <div>
                        <label htmlFor="register-phone" className={labelClass}>
                            Phone Number (optional)
                        </label>
                        <input
                            id="register-phone"
                            type="tel"
                            autoComplete="tel"
                            placeholder="0901234567"
                            aria-invalid={Boolean(errors.phoneNumber)}
                            aria-describedby={
                                errors.phoneNumber
                                    ? "register-phone-error"
                                    : undefined
                            }
                            className={inputClass}
                            {...register("phoneNumber")}
                        />
                        {errors.phoneNumber && (
                            <p
                                id="register-phone-error"
                                role="alert"
                                className="text-[11px] text-red-400"
                            >
                                {errors.phoneNumber.message}
                            </p>
                        )}
                    </div>
                    <div>
                        <label htmlFor="register-email" className={labelClass}>
                            Email Address
                        </label>
                        <div className="relative flex items-center">
                            <RegisterIcon
                                name="mail"
                                className="pointer-events-none absolute left-2.5 h-[17px] w-[17px] text-[var(--reg-muted)]"
                            />
                            <input
                                id="register-email"
                                type="email"
                                autoComplete="email"
                                placeholder="Enter your email"
                                aria-invalid={Boolean(errors.email)}
                                aria-describedby={
                                    errors.email
                                        ? "register-email-error"
                                        : undefined
                                }
                                className={inputClass}
                                {...register("email")}
                            />
                        </div>
                        {errors.email && (
                            <p
                                id="register-email-error"
                                role="alert"
                                className="text-[11px] text-red-400"
                            >
                                {errors.email.message}
                            </p>
                        )}
                    </div>
                    <div>
                        <label
                            htmlFor="register-password"
                            className={labelClass}
                        >
                            Password
                        </label>
                        <div className="relative flex items-center">
                            <RegisterIcon
                                name="lock"
                                className="pointer-events-none absolute left-2.5 h-[17px] w-[17px] text-[var(--reg-muted)]"
                            />
                            <input
                                id="register-password"
                                type={passwordVisible ? "text" : "password"}
                                autoComplete="new-password"
                                placeholder="Create a password"
                                aria-invalid={Boolean(errors.password)}
                                aria-describedby={
                                    errors.password
                                        ? "register-password-error"
                                        : undefined
                                }
                                className={`${inputClass} pr-9`}
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
                                    setPasswordVisible((value) => !value)
                                }
                                className="absolute right-2 rounded p-0.5 text-[var(--reg-muted)] hover:text-[var(--reg-text)] focus-visible:outline-2 focus-visible:outline-[var(--reg-accent)]"
                            >
                                <RegisterIcon
                                    name={passwordVisible ? "eyeOff" : "eye"}
                                    className="h-[17px] w-[17px]"
                                />
                            </button>
                        </div>
                        {errors.password && (
                            <p
                                id="register-password-error"
                                role="alert"
                                className="text-[11px] text-red-400"
                            >
                                {errors.password.message}
                            </p>
                        )}
                        <p
                            className="text-[11px] text-[var(--reg-muted)]"
                            aria-live="polite"
                        >
                            {password.length >= 8 ? "✓" : "○"} 8 characters ·{" "}
                            {/[A-Z]/.test(password) ? "✓" : "○"} uppercase ·{" "}
                            {/[a-z]/.test(password) ? "✓" : "○"} lowercase ·{" "}
                            {/[0-9]/.test(password) ? "✓" : "○"} number ·{" "}
                            {/[^A-Za-z0-9]/.test(password) ? "✓" : "○"} special
                        </p>
                    </div>
                    <div>
                        <label
                            htmlFor="register-confirm"
                            className={labelClass}
                        >
                            Confirm Password
                        </label>
                        <div className="relative flex items-center">
                            <RegisterIcon
                                name="lock"
                                className="pointer-events-none absolute left-2.5 h-[17px] w-[17px] text-[var(--reg-muted)]"
                            />
                            <input
                                id="register-confirm"
                                type={confirmationVisible ? "text" : "password"}
                                autoComplete="new-password"
                                placeholder="Confirm your password"
                                aria-invalid={Boolean(errors.confirmPassword)}
                                aria-describedby={
                                    errors.confirmPassword
                                        ? "register-confirm-error"
                                        : undefined
                                }
                                className={`${inputClass} pr-9`}
                                {...register("confirmPassword")}
                            />
                            <button
                                type="button"
                                aria-label={
                                    confirmationVisible
                                        ? "Hide confirmation password"
                                        : "Show confirmation password"
                                }
                                aria-pressed={confirmationVisible}
                                onClick={() =>
                                    setConfirmationVisible((value) => !value)
                                }
                                className="absolute right-2 rounded p-0.5 text-[var(--reg-muted)] hover:text-[var(--reg-text)] focus-visible:outline-2 focus-visible:outline-[var(--reg-accent)]"
                            >
                                <RegisterIcon
                                    name={
                                        confirmationVisible ? "eyeOff" : "eye"
                                    }
                                    className="h-[17px] w-[17px]"
                                />
                            </button>
                        </div>
                        {errors.confirmPassword && (
                            <p
                                id="register-confirm-error"
                                role="alert"
                                className="text-[11px] text-red-400"
                            >
                                {errors.confirmPassword.message}
                            </p>
                        )}
                    </div>
                    <div>
                        <label className="flex cursor-pointer items-start gap-2 text-[11px] leading-tight text-[var(--reg-muted)]">
                            <input
                                type="checkbox"
                                aria-invalid={Boolean(errors.agreeTerms)}
                                aria-describedby={
                                    errors.agreeTerms
                                        ? "register-terms-error"
                                        : undefined
                                }
                                className="mt-0.5 h-3.5 w-3.5 shrink-0 accent-[var(--reg-accent)]"
                                {...register("agreeTerms")}
                            />
                            <span>
                                I agree to the{" "}
                                <span className="text-[var(--reg-accent)]">
                                    Terms of Service
                                </span>{" "}
                                and{" "}
                                <span className="text-[var(--reg-accent)]">
                                    Privacy Policy
                                </span>
                            </span>
                        </label>
                        {errors.agreeTerms && (
                            <p
                                id="register-terms-error"
                                role="alert"
                                className="text-[11px] text-red-400"
                            >
                                {errors.agreeTerms.message}
                            </p>
                        )}
                    </div>
                    <button
                        type="submit"
                        disabled={isSubmitting}
                        className="mt-1 flex h-[38px] w-full items-center justify-center gap-1.5 rounded-xl bg-gradient-to-r from-[#937aff] to-[#4d36d0] text-[13.5px] font-semibold text-white shadow-[0_4px_16px_rgba(147,122,255,0.35)] hover:brightness-110 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--reg-accent)] disabled:cursor-wait disabled:opacity-60"
                    >
                        {isSubmitting ? "Creating account…" : "Create account"}
                        <RegisterIcon
                            name="arrow"
                            className="h-[17px] w-[17px]"
                        />
                    </button>
                </form>

                <div className="text-center text-[11.5px] text-[var(--reg-muted)]">
                    Already have an account?{" "}
                    <Link
                        href="/login"
                        className="ml-0.5 font-semibold text-[var(--reg-accent)] hover:underline focus-visible:outline-2 focus-visible:outline-[var(--reg-accent)]"
                    >
                        Log In →
                    </Link>
                </div>
                {notice && (
                    <p
                        role="status"
                        className="text-center text-[11px] text-[var(--reg-muted)]"
                    >
                        {notice}
                    </p>
                )}
                {emailConflict && (
                    <p className="text-center text-[11px] text-[var(--reg-muted)]">
                        <Link href="/login">Log in now</Link> ·{" "}
                        <Link href="/forgot-password">Forgot password?</Link>
                    </p>
                )}
            </div>
        </section>
    );
}
