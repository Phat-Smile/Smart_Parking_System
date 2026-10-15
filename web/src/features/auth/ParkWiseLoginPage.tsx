"use client";

import { useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ParkWiseAuthLayout } from "./ParkWiseAuthLayout";
import { loginParkWiseAccount } from "./loginParkWiseAccount";
import { GoogleSignInButton } from "./GoogleSignInButton";
import {
    describeGoogleSignInError,
    googleLoginParkWiseAccount,
} from "./googleSignIn";
import { rememberPendingVerification } from "./pendingVerification";
import { ApiError } from "@/shared/errors/api-error";

const loginSchema = z.object({
    email: z
        .string()
        .trim()
        .min(1, "Email is required")
        .email("Enter a valid email address")
        .max(254, "Email is too long"),
    password: z
        .string()
        .min(1, "Password is required")
        .refine(
            (password) => new TextEncoder().encode(password).length <= 72,
            "Password is too long",
        ),
});

type LoginValues = z.infer<typeof loginSchema>;

export function ParkWiseLoginPage({
    fontClassName,
}: {
    fontClassName: string;
}) {
    const router = useRouter();
    const [notice, setNotice] = useState("");
    const [submitError, setSubmitError] = useState("");
    const [authenticatedEmail, setAuthenticatedEmail] = useState("");
    const [lockSeconds, setLockSeconds] = useState(0);
    const [googlePending, setGooglePending] = useState(false);
    const [googleUnavailable, setGoogleUnavailable] = useState(false);

    useEffect(() => {
        if (lockSeconds <= 0) return;
        const timer = window.setTimeout(
            () => setLockSeconds((value) => value - 1),
            1000,
        );
        return () => window.clearTimeout(timer);
    }, [lockSeconds]);
    const {
        register,
        handleSubmit,
        resetField,
        formState: { errors, isSubmitting },
    } = useForm<LoginValues>({
        resolver: zodResolver(loginSchema),
        defaultValues: { email: "", password: "" },
    });

    async function onSubmit(values: LoginValues) {
        setNotice("");
        setSubmitError("");
        setAuthenticatedEmail("");
        try {
            const account = await loginParkWiseAccount(
                values.email.trim(),
                values.password,
            );
            resetField("password");
            setAuthenticatedEmail(account.email);
        } catch (error) {
            if (error instanceof ApiError) {
                const code = error.problem?.errorCode;
                const message =
                    code === "identity.password_not_set"
                        ? 'This account was created with Google and has no password yet. Use "Continue with Google", or set one with "Forgot password?".'
                        : code === "identity.invalid_credentials" ||
                            error.status === 401
                          ? "Email or password is incorrect."
                          : code === "identity.email_not_verified"
                            ? "Your email is not verified yet. Enter the code sent to your email."
                            : code === "identity.account_temporarily_locked" ||
                                error.status === 423
                              ? "Too many attempts. Please try again later."
                              : error.status === 400
                                ? "Check your email and password and try again."
                                : "We couldn't log you in. Please try again later.";
                if (code === "identity.email_not_verified") {
                    rememberPendingVerification(values.email.trim());
                    router.push(
                        `/verify-otp?email=${encodeURIComponent(values.email.trim())}`,
                    );
                    return;
                }
                if (
                    code === "identity.account_temporarily_locked" ||
                    error.status === 423
                ) {
                    setLockSeconds(error.retryAfterSeconds ?? 900);
                }
                setSubmitError(
                    error.correlationId
                        ? `${message} Support ID: ${error.correlationId}`
                        : message,
                );
            } else {
                setSubmitError(
                    "We couldn't connect. Check your connection and try again.",
                );
            }
        }
    }

    async function onGoogleCredential(idToken: string) {
        setGooglePending(true);
        setNotice("");
        setSubmitError("");
        setAuthenticatedEmail("");
        try {
            const account = await googleLoginParkWiseAccount(idToken);
            setAuthenticatedEmail(account.email);
        } catch (error) {
            if (
                error instanceof ApiError &&
                (error.problem?.errorCode ===
                    "identity.google_login_unavailable" ||
                    error.status === 503)
            ) {
                setGoogleUnavailable(true);
            }
            setSubmitError(describeGoogleSignInError(error));
        } finally {
            setGooglePending(false);
        }
    }

    function onGoogleUnavailable() {
        setGoogleUnavailable(true);
        setSubmitError("");
        setNotice("Google sign-in is not available yet.");
    }

    return (
        <ParkWiseAuthLayout
            fontClassName={fontClassName}
            className="parkwise-login-redesign"
        >
            <aside className="parkwise-login-aside">
                <div className="parkwise-welcome">
                    <h1>Welcome Back!</h1>
                    <p className="parkwise-login-lead">
                        Find smarter parking with ParkWise.
                    </p>
                    <p className="parkwise-login-description">
                        Search nearby parking and enjoy a smarter parking
                        experience.
                    </p>
                </div>
                <Image
                    src="/images/parkwise/login-parking-illustration.png"
                    alt="Car beside a parking availability map"
                    width={1536}
                    height={1024}
                    sizes="(max-width: 767px) 90vw, 480px"
                    className="parkwise-login-illustration"
                />
            </aside>

            <div className="parkwise-login-form-panel">
                <form noValidate onSubmit={handleSubmit(onSubmit)}>
                    <h2>Log in to your account</h2>

                    <div className="parkwise-login-field">
                        <label htmlFor="parkwise-login-email">Email</label>
                        <input
                            id="parkwise-login-email"
                            type="email"
                            autoComplete="username"
                            placeholder="Enter your email"
                            aria-invalid={Boolean(errors.email)}
                            aria-describedby={
                                errors.email
                                    ? "parkwise-login-email-error"
                                    : undefined
                            }
                            {...register("email")}
                            disabled={lockSeconds > 0}
                        />
                        {errors.email && (
                            <p id="parkwise-login-email-error" role="alert">
                                {errors.email.message}
                            </p>
                        )}
                    </div>

                    <div className="parkwise-login-field">
                        <label htmlFor="parkwise-login-password">
                            Password
                        </label>
                        <input
                            id="parkwise-login-password"
                            type="password"
                            autoComplete="current-password"
                            placeholder="Enter your password"
                            aria-invalid={Boolean(errors.password)}
                            aria-describedby={
                                errors.password
                                    ? "parkwise-login-password-error"
                                    : undefined
                            }
                            {...register("password")}
                            disabled={lockSeconds > 0}
                        />
                        {errors.password && (
                            <p id="parkwise-login-password-error" role="alert">
                                {errors.password.message}
                            </p>
                        )}
                    </div>

                    <Link
                        href="/forgot-password"
                        className="parkwise-login-forgot"
                    >
                        Forgot password?
                    </Link>
                    <button
                        className="parkwise-login-submit"
                        type="submit"
                        disabled={isSubmitting || lockSeconds > 0}
                    >
                        {isSubmitting ? "Verifying..." : "Log in"}
                    </button>
                    {submitError && (
                        <p className="parkwise-login-error" role="alert">
                            {submitError}
                        </p>
                    )}
                    {lockSeconds > 0 && (
                        <p className="parkwise-login-error" role="status">
                            Account locked. Try again in{" "}
                            {Math.floor(lockSeconds / 60)}:
                            {String(lockSeconds % 60).padStart(2, "0")}, or
                            reset your password.
                        </p>
                    )}
                    {authenticatedEmail && (
                        <div className="parkwise-login-notice" role="status">
                            <p>Signed in as {authenticatedEmail}.</p>
                            <Link href="/settings/security">
                                Account security
                            </Link>
                        </div>
                    )}

                    <div className="parkwise-login-divider" aria-hidden="true">
                        <span>or</span>
                    </div>
                    {!googleUnavailable && (
                        <GoogleSignInButton
                            text="continue_with"
                            onCredential={onGoogleCredential}
                            onUnavailable={onGoogleUnavailable}
                        />
                    )}
                    {googlePending && (
                        <p className="parkwise-login-notice" role="status">
                            Verifying your Google account...
                        </p>
                    )}
                    {notice && (
                        <p
                            className="parkwise-login-notice"
                            role="status"
                            aria-live="polite"
                        >
                            {notice}
                        </p>
                    )}

                    <p className="parkwise-login-signup">
                        Don&apos;t have an account?{" "}
                        <Link href="/sign-up">Sign up</Link>
                    </p>
                </form>
            </div>
        </ParkWiseAuthLayout>
    );
}
