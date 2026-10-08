"use client";

import { useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ParkWiseAuthLayout } from "./ParkWiseAuthLayout";

const loginSchema = z.object({
    email: z
        .string()
        .trim()
        .min(1, "Email is required")
        .email("Enter a valid email address"),
    password: z.string().min(1, "Password is required"),
});

type LoginValues = z.infer<typeof loginSchema>;

export function ParkWiseLoginPage({
    fontClassName,
}: {
    fontClassName: string;
}) {
    const [notice, setNotice] = useState("");
    const [submitError, setSubmitError] = useState("");
    const {
        register,
        handleSubmit,
        formState: { errors, isSubmitting },
    } = useForm<LoginValues>({
        resolver: zodResolver(loginSchema),
        defaultValues: { email: "", password: "" },
    });

    function onSubmit() {
        setNotice("");
        setSubmitError(
            "Log in is not connected in this frontend yet. Please try again later.",
        );
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
