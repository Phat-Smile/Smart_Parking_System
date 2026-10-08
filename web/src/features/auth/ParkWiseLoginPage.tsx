"use client";

import { useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ParkWiseAuthLayout } from "./ParkWiseAuthLayout";
import { loginParkWiseAccount } from "./loginParkWiseAccount";
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
        .max(72, "Password is too long"),
});

type LoginValues = z.infer<typeof loginSchema>;

export function ParkWiseLoginPage({
    fontClassName,
}: {
    fontClassName: string;
}) {
    const [notice, setNotice] = useState("");
    const [submitError, setSubmitError] = useState("");
    const [authenticatedEmail, setAuthenticatedEmail] = useState("");
    const [canVerifyEmail, setCanVerifyEmail] = useState(false);
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
        setCanVerifyEmail(false);
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
                    code === "identity.invalid_credentials" ||
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
                    setCanVerifyEmail(
                        rememberPendingVerification(values.email.trim()),
                    );
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

    function onGoogleSignIn() {
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
                        disabled={isSubmitting}
                    >
                        {isSubmitting ? "Verifying..." : "Log in"}
                    </button>
                    {submitError && (
                        <p className="parkwise-login-error" role="alert">
                            {submitError}
                        </p>
                    )}
                    {canVerifyEmail && (
                        <Link
                            href="/verify-otp"
                            className="parkwise-login-forgot"
                        >
                            Verify your email
                        </Link>
                    )}
                    {authenticatedEmail && (
                        <p className="parkwise-login-notice" role="status">
                            Identity accepted {authenticatedEmail}. Persistent
                            sign-in is not available until session management is
                            connected.
                        </p>
                    )}

                    <div className="parkwise-login-divider" aria-hidden="true">
                        <span>or</span>
                    </div>
                    <button
                        className="parkwise-login-google"
                        type="button"
                        onClick={onGoogleSignIn}
                    >
                        <Image
                            src="/images/parkwise/google.png"
                            alt=""
                            width={22}
                            height={22}
                        />
                        <span>Continue with Google</span>
                    </button>
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
