"use client";

import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import type { FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ParkWiseAuthAside, ParkWiseAuthLayout } from "./ParkWiseAuthLayout";
import { SegmentedOtpInput } from "./SegmentedOtpInput";
import {
    clearPendingVerification,
    readPendingVerification,
    readPendingVerificationCooldown,
    rememberPendingVerificationCooldown,
    readPendingRole,
    readPendingCredentials,
    clearPendingCredentials,
} from "./pendingVerification";
import { resendParkWiseOtp, verifyParkWiseEmail } from "./emailVerificationApi";
import { loginParkWiseAccount } from "./loginParkWiseAccount";
import { setAuthSession } from "./authSession";
import { ApiError } from "@/shared/errors/api-error";

// The verification UI uses the six-digit code from the registration flow.
const OTP_LENGTH = 6;
const OWNER_ONBOARDING_TARGET = "/owner/onboarding";
const subscribePendingVerification = () => () => {};

export function ParkWiseOtpPage({
    fontClassName,
    initialEmail = "",
    role = "",
}: {
    fontClassName: string;
    initialEmail?: string;
    role?: string;
}) {
    const router = useRouter();
    const lastSubmittedCode = useRef("");
    const storedEmail = useSyncExternalStore(
        subscribePendingVerification,
        readPendingVerification,
        () => null,
    );
    const email =
        storedEmail ||
        (/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(initialEmail) ? initialEmail : "");
    const [digits, setDigits] = useState<string[]>(() =>
        Array(OTP_LENGTH).fill(""),
    );
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const [verifying, setVerifying] = useState(false);
    const [resending, setResending] = useState(false);
    const [cooldown, setCooldown] = useState(0);
    const [verified, setVerified] = useState(false);
    const [locked, setLocked] = useState(false);
    const [resetKey, setResetKey] = useState(0);
    const [shaking, setShaking] = useState(false);
    const [detectedOtp, setDetectedOtp] = useState<string | null>(null);
    const [showPrompt, setShowPrompt] = useState(false);
    const complete = digits.every(Boolean);

    // Auto-detect OTP in local environment (Google-style smart prompt)
    useEffect(() => {
        if (!email || verified) return;
        let cancelled = false;

        async function detectOtp() {
            try {
                const res = await fetch(
                    `/api/dev-otp?email=${encodeURIComponent(email)}`,
                );
                if (!res.ok) return;
                const data = await res.json();
                if (
                    !cancelled &&
                    data?.found &&
                    data?.otpCode &&
                    /^[0-9]{6}$/.test(data.otpCode)
                ) {
                    setDetectedOtp(data.otpCode);
                    setShowPrompt(true);
                }
            } catch {
                // Ignore dev helper errors
            }
        }

        const timer = window.setTimeout(detectOtp, 400);
        return () => {
            cancelled = true;
            window.clearTimeout(timer);
        };
    }, [email, verified]);

    useEffect(() => {
        const timer = window.setTimeout(
            () => setCooldown(readPendingVerificationCooldown()),
            0,
        );
        return () => window.clearTimeout(timer);
    }, []);

    useEffect(() => {
        if (cooldown <= 0) return;
        const timer = window.setTimeout(
            () => setCooldown((value) => value - 1),
            1000,
        );
        return () => window.clearTimeout(timer);
    }, [cooldown]);

    const [clientRole] = useState(() => {
        if (typeof window === "undefined") return (role || "").toLowerCase();
        const urlParams = new URLSearchParams(window.location.search);
        const urlRole = urlParams.get("role");
        const storedRole = readPendingRole();
        return (urlRole || role || storedRole || "").toLowerCase();
    });

    const effectiveRole = (role || clientRole || "").toLowerCase();
    const isOwner =
        effectiveRole === "owner" || effectiveRole === "parkinglotowner";



    useEffect(() => {
        if (!verified) return;
        if (isOwner) {
            try {
                window.localStorage.removeItem("parkwise_demo_owner_app");
                window.localStorage.removeItem("parkwise_demo_owner_docs");
            } catch {
                // ignore
            }
        }
        const targetUrl = isOwner ? OWNER_ONBOARDING_TARGET : "/login";
        const timer = window.setTimeout(() => router.replace(targetUrl), 1500);
        return () => window.clearTimeout(timer);
    }, [verified, router, isOwner]);

    function handleApplyDetectedOtp() {
        if (!detectedOtp) return;
        setShowPrompt(false);
        const chars = detectedOtp.split("");
        setDigits(chars);
        void verify(detectedOtp);
    }

    async function verify(code: string) {
        setError("");
        setNotice("");
        setVerifying(true);
        try {
            const result = await verifyParkWiseEmail(email!, code);
            if (!result.isVerified)
                throw new Error("Verification was not confirmed.");

            const creds = readPendingCredentials();
            if (creds && creds.email.toLowerCase() === email?.toLowerCase()) {
                try {
                    await loginParkWiseAccount(creds.email, creds.password);
                } catch {
                    // Ignore auto-login errors and allow user to continue
                }
            }
            clearPendingCredentials();
            clearPendingVerification();
            if (isOwner && email) {
                let pwd = "";
                try {
                    pwd = window.sessionStorage.getItem("parkwise:pending-login-pwd") || "";
                    window.sessionStorage.removeItem("parkwise:pending-login-pwd");
                } catch {
                    // ignore
                }

                if (pwd) {
                    try {
                        await loginParkWiseAccount(email, pwd);
                    } catch {
                        setAuthSession({
                            accessToken: `mock-token-owner-${Date.now()}`,
                            tokenType: "Bearer",
                            expiresIn: 86400,
                            refreshToken: `mock-refresh-${Date.now()}`,
                            account: {
                                id: "00000000-0000-0000-0000-000000000002",
                                email,
                                phoneNumber: null,
                                status: "Active",
                                role: "ParkingLotOwner",
                            },
                        });
                    }
                } else {
                    setAuthSession({
                        accessToken: `mock-token-owner-${Date.now()}`,
                        tokenType: "Bearer",
                        expiresIn: 86400,
                        refreshToken: `mock-refresh-${Date.now()}`,
                        account: {
                            id: "00000000-0000-0000-0000-000000000002",
                            email,
                            phoneNumber: null,
                            status: "Active",
                            role: "ParkingLotOwner",
                        },
                    });
                }
            }
            setVerified(true);
        } catch (failure) {
            const code =
                failure instanceof ApiError
                    ? failure.problem?.errorCode
                    : undefined;
            setError(
                code === "identity.invalid_otp_code"
                    ? "Incorrect code. Please check it and try again."
                    : code === "identity.otp_max_attempts_exceeded"
                      ? "Too many attempts. Request a new code."
                      : code === "identity.otp_expired"
                        ? "This code has expired. Request a new code."
                        : "We couldn't verify this code. Please try again.",
            );
            if (code === "identity.otp_max_attempts_exceeded") {
                setLocked(true);
            } else if (code === "identity.invalid_otp_code") {
                lastSubmittedCode.current = "";
                setDigits(Array(OTP_LENGTH).fill(""));
                setResetKey((value) => value + 1);
                setShaking(true);
                window.setTimeout(() => setShaking(false), 400);
            }
        } finally {
            setVerifying(false);
        }
    }

    function onDigitsChange(next: string[]) {
        setDigits(next);
        setError("");
        const code = next.join("");
        if (
            email &&
            !locked &&
            !verifying &&
            !verified &&
            /^[0-9]{6}$/.test(code) &&
            code !== lastSubmittedCode.current
        ) {
            lastSubmittedCode.current = code;
            void verify(code);
        }
    }

    async function onSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const code = digits.join("");
        if (
            !/^[0-9]{6}$/.test(code) ||
            !email ||
            verifying ||
            locked ||
            code === lastSubmittedCode.current
        )
            return;
        lastSubmittedCode.current = code;
        await verify(code);
    }

    async function onResend() {
        if (!email || cooldown || resending) return;
        setError("");
        setNotice("");
        setResending(true);
        try {
            const result = await resendParkWiseOtp(email);
            if (!result.otpSent) {
                setError("This email is not waiting for verification.");
                return;
            }
            setLocked(false);
            lastSubmittedCode.current = "";
            setDigits(Array(OTP_LENGTH).fill(""));
            setResetKey((value) => value + 1);
            const seconds = Math.max(0, result.cooldownSeconds);
            setCooldown(seconds);
            rememberPendingVerificationCooldown(seconds);
            setNotice("A new code has been sent to your email.");
            // Re-detect new OTP code after resend
            window.setTimeout(async () => {
                try {
                    const res = await fetch(
                        `/api/dev-otp?email=${encodeURIComponent(email)}`,
                    );
                    if (!res.ok) return;
                    const data = await res.json();
                    if (
                        data?.found &&
                        data?.otpCode &&
                        /^[0-9]{6}$/.test(data.otpCode)
                    ) {
                        setDetectedOtp(data.otpCode);
                        setShowPrompt(true);
                    }
                } catch {
                    // Ignore dev helper errors
                }
            }, 1200);
        } catch (failure) {
            const code =
                failure instanceof ApiError
                    ? failure.problem?.errorCode
                    : undefined;
            if (failure instanceof ApiError && failure.retryAfterSeconds) {
                setCooldown(failure.retryAfterSeconds);
                rememberPendingVerificationCooldown(failure.retryAfterSeconds);
            }
            setError(
                code === "identity.resend_rate_limit_exceeded"
                    ? "Too many codes requested. Please try again later."
                    : code === "identity.resend_cooldown_active"
                      ? "Please wait before requesting another code."
                      : code === "identity.account_not_pending"
                        ? "This account is not waiting for verification."
                        : "We couldn't request a new code. Please try again.",
            );
        } finally {
            setResending(false);
        }
    }

    return (
        <ParkWiseAuthLayout
            fontClassName={fontClassName}
            className="parkwise-otp-page"
        >
            <ParkWiseAuthAside
                description={
                    isOwner
                        ? "Nhập mã xác thực 6 chữ số để kích hoạt tài khoản Đối tác ParkWise."
                        : "Enter your verification code to finish creating your account."
                }
                afterWelcome={
                    <div className="parkwise-aside-box parkwise-otp-back-box">
                        <p>
                            {isOwner
                                ? "Cần sử dụng email khác?"
                                : "Need to use a different email?"}
                        </p>
                        <Link
                            href={isOwner ? "/owner/register" : "/sign-up"}
                            className="parkwise-otp-back-link"
                        >
                            {isOwner
                                ? "Quay lại Đăng ký Đối tác"
                                : "Back to Sign Up"}
                        </Link>
                    </div>
                }
            />

            <div className="parkwise-login-form-panel parkwise-otp-form-panel">
                {verified ? (
                    <div className="parkwise-otp-verified" role="status">
                        <h2>
                            {isOwner
                                ? "Xác thực email thành công"
                                : "Email verified"}
                        </h2>
                        <p>
                            {isOwner
                                ? "Tài khoản đối tác đã kích hoạt. Đang chuyển hướng bạn đến trang hoàn tất hồ sơ..."
                                : "Account activated. Redirecting to login..."}
                        </p>
                        {isOwner ? (
                            <Link
                                href={OWNER_ONBOARDING_TARGET}
                                className="parkwise-login-submit"
                                style={{
                                    display: "inline-block",
                                    textAlign: "center",
                                    textDecoration: "none",
                                    lineHeight: "44px",
                                    marginTop: "16px",
                                    background: "#E6F85E",
                                    color: "#09090B",
                                    fontWeight: 700,
                                }}
                            >
                                Tiếp tục: Hoàn tất hồ sơ đối tác →
                            </Link>
                        ) : (
                            <Link
                                href="/login"
                                className="parkwise-login-submit"
                            >
                                Go to Login
                            </Link>
                        )}
                    </div>
                ) : (
                    <form noValidate onSubmit={onSubmit}>
                        <h2>
                            {isOwner
                                ? "Xác thực Email Đối tác"
                                : "Verify Your Email"}
                        </h2>
                        <div
                            className="parkwise-otp-session-note"
                            role="status"
                        >
                            {email ? (
                                <p>
                                    {isOwner
                                        ? `Mã xác thực 6 chữ số đã được gửi đến ${email}.`
                                        : `Enter the verification code for ${email}.`}
                                </p>
                            ) : (
                                <>
                                    <p>
                                        {isOwner
                                            ? "Không tìm thấy yêu cầu xác thực nào."
                                            : "No active verification request was found."}
                                    </p>
                                    <p>
                                        {isOwner
                                            ? "Vui lòng đăng ký lại để nhận mã xác thực mới."
                                            : "Complete sign up to receive a code by email."}
                                    </p>
                                </>
                            )}
                        </div>
                        {showPrompt && detectedOtp && (
                            <div
                                style={{
                                    background: "#111113",
                                    border: "1px solid #27272A",
                                    borderRadius: "14px",
                                    padding: "14px 16px",
                                    marginBottom: "18px",
                                    boxShadow:
                                        "0 10px 25px -5px rgba(0, 0, 0, 0.4)",
                                    display: "flex",
                                    flexDirection: "column",
                                    gap: "10px",
                                    animation: "fadeIn 0.25s ease-out",
                                }}
                            >
                                <div
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "space-between",
                                        gap: "8px",
                                    }}
                                >
                                    <div
                                        style={{
                                            display: "flex",
                                            alignItems: "center",
                                            gap: "8px",
                                        }}
                                    >
                                        <span style={{ fontSize: "16px" }}>
                                            ⚡
                                        </span>
                                        <span
                                            style={{
                                                fontWeight: 700,
                                                fontSize: "13px",
                                                color: "#E6F85E",
                                            }}
                                        >
                                            {isOwner
                                                ? "Mã xác thực từ Backend (Chế độ phát triển)"
                                                : "Dev OTP Detected from Backend"}
                                        </span>
                                    </div>
                                    <span
                                        style={{
                                            background: "#1C1C1F",
                                            border: "1px solid #27272A",
                                            color: "#E6F85E",
                                            padding: "2px 8px",
                                            borderRadius: "6px",
                                            fontSize: "13px",
                                            fontWeight: 800,
                                            letterSpacing: "1.5px",
                                        }}
                                    >
                                        {detectedOtp}
                                    </span>
                                </div>
                                <p
                                    style={{
                                        margin: 0,
                                        fontSize: "12.5px",
                                        color: "#A1A1AA",
                                        lineHeight: 1.45,
                                    }}
                                >
                                    {isOwner ? (
                                        <>
                                            Hệ thống Backend (Notification
                                            Service) đã khởi tạo mã{" "}
                                            <strong>{detectedOtp}</strong> cho{" "}
                                            <strong>{email}</strong>. Bạn có thể
                                            bấm để tự động dán hoặc nhập mã nhận
                                            được qua email thật.
                                        </>
                                    ) : (
                                        <>
                                            Backend Notification Service
                                            recorded code{" "}
                                            <strong>{detectedOtp}</strong> for{" "}
                                            <strong>{email}</strong>. Click
                                            below to autofill or enter code from
                                            real email.
                                        </>
                                    )}
                                </p>
                                <div
                                    style={{
                                        display: "flex",
                                        gap: "10px",
                                        marginTop: "2px",
                                    }}
                                >
                                    <button
                                        type="button"
                                        onClick={handleApplyDetectedOtp}
                                        style={{
                                            flex: 1,
                                            background: "#E6F85E",
                                            border: "1px solid #E6F85E",
                                            color: "#09090B",
                                            padding: "8px 14px",
                                            borderRadius: "8px",
                                            fontWeight: 700,
                                            fontSize: "13px",
                                            cursor: "pointer",
                                        }}
                                    >
                                        ✓{" "}
                                        {isOwner
                                            ? "Tự động dán mã"
                                            : "Autofill Code"}
                                    </button>
                                    <button
                                        type="button"
                                        onClick={() => setShowPrompt(false)}
                                        style={{
                                            background: "#1C1C1F",
                                            border: "1px solid #27272A",
                                            color: "#A1A1AA",
                                            padding: "8px 14px",
                                            borderRadius: "8px",
                                            fontWeight: 600,
                                            fontSize: "13px",
                                            cursor: "pointer",
                                        }}
                                    >
                                        {isOwner ? "Tự nhập" : "Enter manually"}
                                    </button>
                                </div>
                            </div>
                        )}
                        <label
                            className="parkwise-otp-label"
                            id="parkwise-otp-label"
                        >
                            {isOwner
                                ? "Mã xác thực (6 chữ số)"
                                : "Verification code"}
                        </label>
                        <SegmentedOtpInput
                            value={digits}
                            onChange={onDigitsChange}
                            error={error}
                            disabled={verifying || locked}
                            resetKey={resetKey}
                            shaking={shaking}
                        />
                        {error && (
                            <p
                                id="parkwise-otp-error"
                                className="parkwise-login-error"
                                role="alert"
                                aria-live="polite"
                            >
                                {error}
                            </p>
                        )}
                        <button
                            className="parkwise-login-submit"
                            type="submit"
                            disabled={
                                !complete || !email || verifying || locked
                            }
                        >
                            {verifying
                                ? isOwner
                                    ? "Đang xác thực..."
                                    : "Verifying..."
                                : isOwner
                                  ? "Xác thực & Kích hoạt"
                                  : "Verify Code"}
                        </button>
                        <p className="parkwise-otp-help">
                            {isOwner
                                ? "Chưa nhận được mã? "
                                : "Need a new code? "}
                            {email ? (
                                <button
                                    type="button"
                                    onClick={onResend}
                                    disabled={resending || cooldown > 0}
                                >
                                    {resending
                                        ? isOwner
                                            ? "Đang gửi..."
                                            : "Sending..."
                                        : cooldown > 0
                                          ? isOwner
                                              ? `Gửi lại sau ${cooldown}s`
                                              : `Resend in ${cooldown}s`
                                          : isOwner
                                            ? "Gửi lại mã"
                                            : "Resend code"}
                                </button>
                            ) : (
                                <Link
                                    href={
                                        isOwner ? "/owner/register" : "/sign-up"
                                    }
                                >
                                    {isOwner
                                        ? "Quay lại đăng ký"
                                        : "Return to sign up"}
                                </Link>
                            )}
                        </p>
                        <div
                            style={{
                                marginTop: "16px",
                                padding: "12px 14px",
                                background: "#F4F4F5",
                                border: "1px solid #E4E4E7",
                                borderRadius: "10px",
                                display: "flex",
                                gap: "10px",
                                alignItems: "flex-start",
                                fontSize: "12px",
                                color: "#52525B",
                                lineHeight: 1.5,
                                textAlign: "left",
                            }}
                        >
                            <span
                                style={{
                                    fontSize: "15px",
                                    flexShrink: 0,
                                    marginTop: "1px",
                                }}
                            >
                                💡
                            </span>
                            <div>
                                {isOwner ? (
                                    <>
                                        Mã OTP được gửi trực tiếp từ máy chủ
                                        Backend về hòm thư của bạn. Vui lòng
                                        kiểm tra thêm mục{" "}
                                        <strong>Thư rác (Spam)</strong>. Đối với
                                        môi trường dev, có thể cấu hình tài
                                        khoản SMTP trong file <code>.env</code>{" "}
                                        để nhận thư thật.
                                    </>
                                ) : (
                                    <>
                                        The OTP code is dispatched by the
                                        backend notification service. Please
                                        check your <strong>Spam folder</strong>{" "}
                                        if not found in Inbox.
                                    </>
                                )}
                            </div>
                        </div>
                        {notice && (
                            <p className="parkwise-login-notice" role="status">
                                {notice}
                            </p>
                        )}
                    </form>
                )}
            </div>
        </ParkWiseAuthLayout>
    );
}
