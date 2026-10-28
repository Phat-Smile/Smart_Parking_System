"use client";

import { useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ParkWiseAuthLayout } from "./ParkWiseAuthLayout";
import { GoogleSignInButton } from "./GoogleSignInButton";
import {
    describeGoogleSignInError,
    googleLoginParkWiseAccount,
} from "./googleSignIn";
import { passwordSchema } from "./passwordSchema";
import { registerParkWiseAccount } from "./registerParkWiseAccount";
import { rememberRegisteredProfileName } from "./authSession";
import {
    rememberPendingVerification,
    VERIFICATION_CODE_LIFETIME_SECONDS,
} from "./pendingVerification";
import { ApiError } from "@/shared/errors/api-error";

const signUpSchema = z
    .object({
        firstName: z.string().trim(),
        lastName: z.string().trim(),
        email: z
            .string()
            .trim()
            .min(1, "Email is required")
            .email("Enter a valid email address")
            .max(254, "Email is too long"),
        password: passwordSchema,
        confirmPassword: z.string().min(1, "Confirm your password"),
        phone: z
            .string()
            .trim()
            .refine(
                (phone) =>
                    !phone || /^\s*(0|\+84)[35789][0-9]{8}\s*$/.test(phone),
                "Enter a valid Vietnamese mobile number",
            ),
    })
    .refine(
        (values) => {
            const name = `${values.firstName} ${values.lastName}`.trim();
            return (
                !name ||
                (name.length >= 2 &&
                    name.length <= 100 &&
                    /^[\p{L}]+(?:[ '-][\p{L}]+)*$/u.test(name))
            );
        },
        { message: "Enter a valid name (2–100 letters)", path: ["firstName"] },
    )
    .refine((values) => values.password === values.confirmPassword, {
        message: "The passwords do not match",
        path: ["confirmPassword"],
    });

type SignUpValues = z.infer<typeof signUpSchema>;

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

export function ParkWiseSignUpPage({
    fontClassName,
}: {
    fontClassName: string;
}) {
    const router = useRouter();
    const [passwordVisible, setPasswordVisible] = useState(false);
    const [confirmPasswordVisible, setConfirmPasswordVisible] = useState(false);
    const [submitError, setSubmitError] = useState("");
    const [emailConflict, setEmailConflict] = useState(false);
    const [notice, setNotice] = useState("");
    const [googlePending, setGooglePending] = useState(false);
    const [googleUnavailable, setGoogleUnavailable] = useState(false);
    const {
        register,
        handleSubmit,
        setError,
        control,
        formState: { errors, isSubmitting },
    } = useForm<SignUpValues>({
        resolver: zodResolver(signUpSchema),
        defaultValues: {
            firstName: "",
            lastName: "",
            email: "",
            password: "",
            confirmPassword: "",
            phone: "",
        },
    });
    const password = useWatch({ control, name: "password" }) || "";
    const passwordMeetsRequirements =
        passwordSchema.safeParse(password).success;
    const passwordCharacterGroups = [
        /[A-Z]/.test(password),
        /[a-z]/.test(password),
        /[0-9]/.test(password),
        /[^A-Za-z0-9]/.test(password),
    ].filter(Boolean).length;
    const passwordStrength = !password
        ? 0
        : passwordMeetsRequirements
          ? password.length >= 12
              ? 4
              : 3
          : password.length >= 8 && passwordCharacterGroups >= 3
            ? 2
            : 1;
    const passwordStrengthLabel = [
        "Not entered",
        "Weak",
        "Fair",
        "Good",
        "Strong",
    ][passwordStrength];

    async function onSubmit(values: SignUpValues) {
        setNotice("");
        setSubmitError("");
        setEmailConflict(false);
        try {
            const account = await registerParkWiseAccount({
                fullName:
                    `${values.firstName.trim()} ${values.lastName.trim()}`.trim() ||
                    null,
                email: values.email.trim(),
                password: values.password,
                phoneNumber: values.phone.trim() || null,
            });
            rememberRegisteredProfileName(account.email, account.fullName);
            if (
                !rememberPendingVerification(
                    account.email,
                    60,
                    VERIFICATION_CODE_LIFETIME_SECONDS,
                )
            ) {
                setSubmitError(
                    "Registration was accepted, but this browser could not open email verification. Please enable session storage and try again.",
                );
                return;
            }
            router.push("/verify-otp");
        } catch (error) {
            const code =
                error instanceof ApiError
                    ? error.problem?.errorCode
                    : undefined;
            setEmailConflict(code === "identity.email_already_registered");
            if (code === "identity.phone_already_registered") {
                setError("phone", {
                    message: "This phone number is already registered.",
                });
            }
            const message =
                code === "identity.email_already_registered"
                    ? "This email address is already registered. Log in or reset your password."
                    : code === "identity.phone_already_registered"
                      ? "Check the phone number below."
                      : error instanceof ApiError && error.status === 400
                        ? "Check your details and try again."
                        : "We couldn't create your account. Check your connection and try again.";
            setSubmitError(
                error instanceof ApiError && error.correlationId
                    ? `${message} Support ID: ${error.correlationId}`
                    : message,
            );
        }
    }

    // A Google account is verified by Google, so there is no OTP step.
    async function onGoogleCredential(idToken: string) {
        setGooglePending(true);
        setNotice("");
        setSubmitError("");
        try {
            const account = await googleLoginParkWiseAccount(idToken);
            if (account.role === "Customer") {
                router.replace("/customer/complete-profile");
            } else {
                setNotice(`Signed in with Google as ${account.email}.`);
            }
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
        setNotice("Google sign-up is not available yet.");
    }

    return (
        <ParkWiseAuthLayout
            fontClassName={fontClassName}
            className="parkwise-login-redesign parkwise-signup-page"
        >
            <aside className="parkwise-login-aside">
                <div className="parkwise-welcome">
                    <h1>Join ParkWise!</h1>
                    <p className="parkwise-login-lead">
                        Start your smarter parking journey.
                    </p>
                    <p className="parkwise-login-description">
                        Create your account to find nearby parking, get smart
                        recommendations, and manage your parking with ease.
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

            <div className="parkwise-login-form-panel parkwise-signup-form-panel">
                <form
                    className="parkwise-signup-form"
                    noValidate
                    onSubmit={handleSubmit(onSubmit)}
                >
                    <h2>Create your account</h2>
                    <div style={{
                        background: '#eff6ff',
                        border: '1px solid #bfdbfe',
                        borderRadius: '10px',
                        padding: '10px 14px',
                        fontSize: '13px',
                        color: '#1e40af',
                        marginBottom: '8px',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        gap: '8px',
                    }}>
                        <span>Want to manage a parking lot?</span>
                        <Link href="/owner/register" style={{ fontWeight: 700, textDecoration: 'underline', color: '#1d4ed8' }}>
                            Register as Lot Owner →
                        </Link>
                    </div>

                    <div className="parkwise-signup-two-columns">
                        <div className="parkwise-login-field">
                            <label htmlFor="parkwise-signup-first-name">
                                First Name (optional)
                            </label>
                            <input
                                id="parkwise-signup-first-name"
                                type="text"
                                autoComplete="given-name"
                                placeholder="First name"
                                aria-invalid={Boolean(errors.firstName)}
                                aria-describedby={
                                    errors.firstName
                                        ? "parkwise-signup-first-name-error"
                                        : undefined
                                }
                                {...register("firstName")}
                            />
                            {errors.firstName && (
                                <p
                                    id="parkwise-signup-first-name-error"
                                    role="alert"
                                >
                                    {errors.firstName.message}
                                </p>
                            )}
                        </div>
                        <div className="parkwise-login-field">
                            <label htmlFor="parkwise-signup-last-name">
                                Last Name (optional)
                            </label>
                            <input
                                id="parkwise-signup-last-name"
                                type="text"
                                autoComplete="family-name"
                                placeholder="Last name"
                                aria-invalid={Boolean(errors.lastName)}
                                aria-describedby={
                                    errors.lastName
                                        ? "parkwise-signup-last-name-error"
                                        : undefined
                                }
                                {...register("lastName")}
                            />
                            {errors.lastName && (
                                <p
                                    id="parkwise-signup-last-name-error"
                                    role="alert"
                                >
                                    {errors.lastName.message}
                                </p>
                            )}
                        </div>
                    </div>

                    <div className="parkwise-login-field">
                        <label htmlFor="parkwise-signup-email">
                            Email <span aria-hidden="true">*</span>
                        </label>
                        <input
                            id="parkwise-signup-email"
                            type="email"
                            autoComplete="email"
                            placeholder="Enter your email"
                            aria-invalid={Boolean(errors.email)}
                            aria-describedby={
                                errors.email
                                    ? "parkwise-signup-email-error"
                                    : undefined
                            }
                            {...register("email")}
                        />
                        {errors.email && (
                            <p id="parkwise-signup-email-error" role="alert">
                                {errors.email.message}
                            </p>
                        )}
                    </div>

                    <div className="parkwise-login-field">
                        <label htmlFor="parkwise-signup-phone">
                            Phone Number (optional)
                        </label>
                        <input
                            id="parkwise-signup-phone"
                            type="tel"
                            autoComplete="tel"
                            placeholder="Enter your phone number"
                            aria-invalid={Boolean(errors.phone)}
                            aria-describedby={
                                errors.phone
                                    ? "parkwise-signup-phone-error"
                                    : undefined
                            }
                            {...register("phone")}
                        />
                        {errors.phone && (
                            <p id="parkwise-signup-phone-error" role="alert">
                                {errors.phone.message}
                            </p>
                        )}
                    </div>

                    <div className="parkwise-signup-two-columns">
                        <div className="parkwise-login-field">
                            <label htmlFor="parkwise-signup-password">
                                Password <span aria-hidden="true">*</span>
                            </label>
                            <div className="parkwise-password-control">
                                <input
                                    id="parkwise-signup-password"
                                    type={passwordVisible ? "text" : "password"}
                                    autoComplete="new-password"
                                    placeholder="Password"
                                    aria-invalid={Boolean(errors.password)}
                                    aria-describedby={
                                        errors.password
                                            ? "parkwise-signup-password-error parkwise-signup-password-strength parkwise-signup-password-help"
                                            : "parkwise-signup-password-strength parkwise-signup-password-help"
                                    }
                                    {...register("password")}
                                />
                                <button
                                    type="button"
                                    aria-label={
                                        passwordVisible
                                            ? "Hide password"
                                            : "Show password"
                                    }
                                    onClick={() =>
                                        setPasswordVisible((value) => !value)
                                    }
                                >
                                    <EyeIcon visible={passwordVisible} />
                                </button>
                            </div>
                            {errors.password && (
                                <p
                                    id="parkwise-signup-password-error"
                                    role="alert"
                                >
                                    {errors.password.message}
                                </p>
                            )}
                        </div>
                        <div className="parkwise-login-field">
                            <label htmlFor="parkwise-signup-confirm-password">
                                Confirm Password{" "}
                                <span aria-hidden="true">*</span>
                            </label>
                            <div className="parkwise-password-control">
                                <input
                                    id="parkwise-signup-confirm-password"
                                    type={
                                        confirmPasswordVisible
                                            ? "text"
                                            : "password"
                                    }
                                    autoComplete="new-password"
                                    placeholder="Confirm password"
                                    aria-invalid={Boolean(
                                        errors.confirmPassword,
                                    )}
                                    aria-describedby={
                                        errors.confirmPassword
                                            ? "parkwise-signup-confirm-password-error"
                                            : undefined
                                    }
                                    {...register("confirmPassword")}
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
                                            (value) => !value,
                                        )
                                    }
                                >
                                    <EyeIcon visible={confirmPasswordVisible} />
                                </button>
                            </div>
                            {errors.confirmPassword && (
                                <p
                                    id="parkwise-signup-confirm-password-error"
                                    role="alert"
                                >
                                    {errors.confirmPassword.message}
                                </p>
                            )}
                        </div>
                    </div>

                    <div
                        id="parkwise-signup-password-strength"
                        className="parkwise-signup-password-strength"
                        data-strength={passwordStrength}
                        role="meter"
                        aria-label="Password strength"
                        aria-valuemin={0}
                        aria-valuemax={4}
                        aria-valuenow={passwordStrength}
                        aria-valuetext={passwordStrengthLabel}
                    >
                        <div
                            className="parkwise-signup-password-strength-track"
                            aria-hidden="true"
                        >
                            {[1, 2, 3, 4].map((segment) => (
                                <span
                                    key={segment}
                                    className={
                                        segment <= passwordStrength
                                            ? "filled"
                                            : ""
                                    }
                                />
                            ))}
                        </div>
                        <span
                            className="parkwise-signup-password-strength-label"
                            aria-live="polite"
                        >
                            {password
                                ? `Password strength: ${passwordStrengthLabel}`
                                : "Password strength"}
                        </span>
                    </div>
                    <p
                        id="parkwise-signup-password-help"
                        className="parkwise-signup-password-help"
                        data-met={passwordMeetsRequirements}
                        aria-live="polite"
                    >
                        {passwordMeetsRequirements
                            ? "Password meets the requirements."
                            : "Use 8+ characters with uppercase, lowercase, a number, and a symbol."}
                    </p>

                    <button
                        className="parkwise-login-submit"
                        type="submit"
                        disabled={isSubmitting}
                    >
                        {isSubmitting
                            ? "Creating account..."
                            : "Create Account"}
                    </button>
                    {submitError && (
                        <p className="parkwise-login-error" role="alert">
                            {submitError}
                        </p>
                    )}
                    {emailConflict && (
                        <p className="parkwise-login-forgot">
                            <Link href="/login">Log in now</Link> ·{" "}
                            <Link href="/forgot-password">
                                Forgot password?
                            </Link>
                        </p>
                    )}

                    <div className="parkwise-login-divider" aria-hidden="true">
                        <span>or</span>
                    </div>
                    {!googleUnavailable && (
                        <GoogleSignInButton
                            text="signup_with"
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
                        Already have an account?{" "}
                        <Link href="/login">Log in</Link>
                    </p>
                </form>
            </div>
        </ParkWiseAuthLayout>
    );
}
