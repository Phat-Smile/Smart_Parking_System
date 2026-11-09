"use client";

import React, { useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { passwordSchema } from "@/features/auth/passwordSchema";
import { registerParkWiseAccount } from "@/features/auth/registerParkWiseAccount";
import { rememberRegisteredProfileName } from "@/features/auth/authSession";
import {
    rememberPendingVerification,
    rememberPendingRole,
} from "@/features/auth/pendingVerification";
import { ApiError } from "@/shared/errors/api-error";
import { ParkWiseNavbar } from "@/features/parkwise/ParkWiseNavbar";
import { ParkWiseFooter } from "@/features/parkwise/ParkWiseFooter";
import { useLanguage } from "@/shared/i18n/LanguageContext";

const ownerSignUpSchema = z
    .object({
        fullName: z
            .string()
            .trim()
            .min(2, "Họ và tên phải có ít nhất 2 ký tự")
            .max(100, "Họ và tên không được vượt quá 100 ký tự"),
        email: z
            .string()
            .trim()
            .min(1, "Email không được để trống")
            .email("Địa chỉ email không hợp lệ")
            .max(254, "Email quá dài"),
        phone: z
            .string()
            .trim()
            .min(1, "Số điện thoại không được để trống")
            .refine(
                (phone) => /^\s*(0|\+84)[35789][0-9]{8}\s*$/.test(phone),
                "Số điện thoại di động Việt Nam không hợp lệ (10 số, đầu 03, 05, 07, 08, 09)",
            ),
        password: passwordSchema,
        confirmPassword: z.string().min(1, "Vui lòng xác nhận mật khẩu"),
        termsAccepted: z.boolean().refine((val) => val === true, {
            message:
                "Bạn cần đồng ý với Điều khoản dịch vụ và Chính sách đối tác",
        }),
    })
    .refine((values) => values.password === values.confirmPassword, {
        message: "Mật khẩu xác nhận không khớp",
        path: ["confirmPassword"],
    });

type OwnerSignUpValues = z.infer<typeof ownerSignUpSchema>;

function EyeIcon({ visible }: { visible: boolean }) {
    return visible ? (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            style={{ width: 18, height: 18 }}
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
            style={{ width: 18, height: 18 }}
            aria-hidden="true"
        >
            <path d="M3 3l18 18M10.6 6.1A10.8 10.8 0 0112 6c6.5 0 10 6 10 6a14.9 14.9 0 01-3 3.5M6.1 8.1A15 15 0 002 12s3.5 6 10 6a10.8 10.8 0 004-.8" />
            <path d="M10 10a3 3 0 004 4" />
        </svg>
    );
}

export function OwnerRegisterPage({
    fontClassName,
}: {
    fontClassName?: string;
}) {
    const router = useRouter();
    const langContext = useLanguage();
    const [currentLang, setCurrentLang] = useState<"vi" | "en">(
        langContext?.language || "vi",
    );

    function handleSetLanguage(lang: "vi" | "en") {
        setCurrentLang(lang);
        try {
            langContext?.setLanguage?.(lang);
        } catch {
            // ignore
        }
    }

    const isVi = currentLang === "vi";

    const [passwordVisible, setPasswordVisible] = useState(false);
    const [confirmPasswordVisible, setConfirmPasswordVisible] = useState(false);
    const [submitError, setSubmitError] = useState("");
    const [emailConflict, setEmailConflict] = useState(false);

    const {
        register,
        handleSubmit,
        control,
        setError,
        getValues,
        formState: { errors, isSubmitting },
    } = useForm<OwnerSignUpValues>({
        resolver: zodResolver(ownerSignUpSchema),
        defaultValues: {
            fullName: "",
            email: "",
            phone: "",
            password: "",
            confirmPassword: "",
            termsAccepted: false,
        },
    });

    const passwordValue = useWatch({ control, name: "password" }) || "";

    // Tính độ mạnh mật khẩu và nhóm ký tự (như trang Sign Up chuẩn ParkWise)
    const passwordMeetsRequirements =
        passwordSchema.safeParse(passwordValue).success;
    const passwordCharacterGroups = [
        /[A-Z]/.test(passwordValue),
        /[a-z]/.test(passwordValue),
        /[0-9]/.test(passwordValue),
        /[^A-Za-z0-9]/.test(passwordValue),
    ].filter(Boolean).length;
    const passwordStrength = !passwordValue
        ? 0
        : passwordMeetsRequirements
          ? passwordValue.length >= 12
              ? 4
              : 3
          : passwordValue.length >= 8 && passwordCharacterGroups >= 3
            ? 2
            : 1;

    const passwordStrengthLabelsVi = [
        "Chưa nhập",
        "Yếu",
        "Trung bình",
        "Khá",
        "Mạnh",
    ];
    const passwordStrengthLabelsEn = [
        "Not entered",
        "Weak",
        "Fair",
        "Good",
        "Strong",
    ];
    const passwordStrengthLabel = isVi
        ? passwordStrengthLabelsVi[passwordStrength]
        : passwordStrengthLabelsEn[passwordStrength];

    async function onSubmit(values: OwnerSignUpValues) {
        setSubmitError("");
        setEmailConflict(false);
        try {
            const account = await registerParkWiseAccount({
                fullName: values.fullName.trim(),
                email: values.email.trim(),
                password: values.password,
                phoneNumber: values.phone.trim(),
                accountType: "ParkingLotOwner",
            });

            rememberRegisteredProfileName(account.email, account.fullName);
            rememberPendingRole("owner");
            try {
                window.sessionStorage.setItem("parkwise:pending-login-pwd", values.password);
            } catch {
                // ignore
            }
            if (!rememberPendingVerification(account.email, 60)) {
                setSubmitError(
                    isVi
                        ? "Đăng ký thành công nhưng trình duyệt không mở được xác thực. Vui lòng bật Session Storage và thử lại."
                        : "Registration succeeded but browser could not open verification. Please enable session storage and try again.",
                );
                return;
            }

            // Chuyển sang xác thực email OTP
            router.push(
                `/verify-otp?email=${encodeURIComponent(account.email)}&role=owner`,
            );
        } catch (error) {
            const problem =
                error instanceof ApiError ? error.problem : undefined;
            const code = problem?.errorCode;
            const status = error instanceof ApiError ? error.status : undefined;

            if (code === "identity.email_already_registered") {
                setEmailConflict(true);
                setError("email", {
                    message: isVi
                        ? "Email này đã được sử dụng."
                        : "This email address is already in use.",
                });
                setSubmitError(
                    isVi
                        ? "Email này đã được đăng ký. Vui lòng đăng nhập hoặc sử dụng email khác."
                        : "This email is already registered. Please log in or use another email.",
                );
            } else if (code === "identity.email_pending_verification") {
                setEmailConflict(true);
                setSubmitError(
                    isVi
                        ? "Email này đã được đăng ký và đang chờ xác thực OTP. Vui lòng hoàn tất xác thực hoặc đăng nhập."
                        : "This email is awaiting OTP verification. Please verify or log in.",
                );
            } else if (code === "identity.phone_already_registered") {
                setError("phone", {
                    message: isVi
                        ? "Số điện thoại này đã được đăng ký cho tài khoản khác."
                        : "This phone number is already registered.",
                });
                setSubmitError(
                    isVi
                        ? "Số điện thoại này đã được sử dụng bởi một tài khoản khác. Vui lòng kiểm tra lại."
                        : "This phone number is already in use by another account.",
                );
            } else if (status === 400) {
                setSubmitError(
                    problem?.detail ||
                        (isVi
                            ? "Thông tin đăng ký không hợp lệ. Vui lòng kiểm tra lại các trường thông tin."
                            : "Invalid registration details. Please check the fields and try again."),
                );
            } else if (status === 409) {
                const detail = (problem?.detail || "").toLowerCase();
                if (detail.includes("phone") || detail.includes("điện thoại")) {
                    setError("phone", {
                        message: isVi
                            ? "Số điện thoại này đã được sử dụng."
                            : "This phone number is already registered.",
                    });
                    setSubmitError(
                        isVi
                            ? "Số điện thoại này đã được đăng ký cho một tài khoản khác."
                            : "This phone number is already registered for another account.",
                    );
                } else if (detail.includes("email")) {
                    setError("email", {
                        message: isVi
                            ? "Email này đã được sử dụng."
                            : "This email is already registered.",
                    });
                    setEmailConflict(true);
                    setSubmitError(
                        isVi
                            ? "Email này đã được đăng ký trên hệ thống."
                            : "This email address is already registered.",
                    );
                } else {
                    setSubmitError(
                        isVi
                            ? "Thông tin Email hoặc Số điện thoại đã tồn tại trên hệ thống. Vui lòng kiểm tra lại."
                            : "Email or phone already exists in system.",
                    );
                }
            } else if (status === 500) {
                setSubmitError(
                    isVi
                        ? "Không thể hoàn tất đăng ký: Số điện thoại hoặc Email này có thể đã được đăng ký trong hệ thống. Vui lòng thử số điện thoại khác hoặc kiểm tra lại."
                        : "Unable to complete registration: Phone number or Email may already exist. Please try another.",
                );
            } else {
                setSubmitError(
                    error instanceof ApiError &&
                        error.message &&
                        error.message !== "An unexpected error occurred."
                        ? error.message
                        : isVi
                          ? "Không thể tạo tài khoản đối tác lúc này. Vui lòng kiểm tra lại thông tin và thử lại sau."
                          : "Cannot create partner account right now. Please try again later.",
                );
            }
        }
    }

    return (
        <div
            className={`parkwise-page ${fontClassName || ""}`}
            style={{
                minHeight: "100vh",
                display: "flex",
                flexDirection: "column",
                background: "#f8fafc",
            }}
        >
            <ParkWiseNavbar variant="home" />

            <main
                className="parkwise-owner-register-main"
                style={{
                    flex: 1,
                    width: "100%",
                    maxWidth: "100%",
                    paddingTop: "calc(var(--parkwise-nav-height, 66px) + 32px)",
                    paddingBottom: "48px",
                    display: "flex",
                    flexDirection: "column",
                    alignItems: "center",
                    justifyContent: "flex-start",
                    margin: "0 auto",
                }}
            >
                <div
                    className="owner-reg-wrapper"
                    style={{
                        width: "100%",
                        maxWidth: "100%",
                        padding: "0 24px",
                        margin: "0 auto",
                        display: "flex",
                        justifyContent: "center",
                        alignItems: "center",
                    }}
                >
                    <div
                        style={{
                            display: "grid",
                            gridTemplateColumns:
                                "minmax(320px, 1.15fr) minmax(360px, 1.5fr)",
                            width: "100%",
                            maxWidth: 1040,
                            margin: "0 auto",
                            borderRadius: 16,
                            overflow: "hidden",
                            background: "#ffffff",
                            boxShadow:
                                "0 4px 24px -2px rgba(0, 0, 0, 0.08), 0 2px 8px -1px rgba(0, 0, 0, 0.04)",
                            border: "1px solid #e4e4e7",
                        }}
                        className="owner-reg-container"
                    >
                        {/* ── Cột trái: Lợi ích Chủ bãi & Thương hiệu (Đen Carbon #111113 & Vàng Chanh Neon #E6F85E) ── */}
                        <aside
                            style={{
                                background: "#111113",
                                color: "#ffffff",
                                padding: "40px 36px",
                                display: "flex",
                                flexDirection: "column",
                                justifyContent: "flex-start",
                                gap: 24,
                                position: "relative",
                                overflow: "hidden",
                            }}
                        >
                            {/* Họa tiết đường đồng mức (topographic contour lines) mờ 0.05 */}
                            <div
                                style={{
                                    position: "absolute",
                                    inset: 0,
                                    backgroundImage:
                                        "url(/images/parkwise/patern.svg)",
                                    backgroundSize: "cover",
                                    backgroundPosition: "center",
                                    opacity: 0.05,
                                    pointerEvents: "none",
                                }}
                            />

                            <div style={{ position: "relative", zIndex: 1 }}>
                                <div
                                    style={{
                                        display: "inline-flex",
                                        alignItems: "center",
                                        gap: 8,
                                        padding: "6px 14px",
                                        borderRadius: 99,
                                        background: "#1c1c1f",
                                        border: "1px solid #27272a",
                                        color: "#e6f85e",
                                        fontSize: 12,
                                        fontWeight: 700,
                                        letterSpacing: "0.05em",
                                        marginBottom: 20,
                                    }}
                                >
                                    <span
                                        style={{
                                            width: 8,
                                            height: 8,
                                            borderRadius: "50%",
                                            background: "#e6f85e",
                                        }}
                                    />
                                    PARKWISE PARTNER NETWORK
                                </div>

                                <h1
                                    style={{
                                        fontSize: 28,
                                        fontWeight: 800,
                                        lineHeight: 1.25,
                                        margin: "0 0 14px",
                                        color: "#ffffff",
                                    }}
                                >
                                    {isVi ? (
                                        <>
                                            Hợp tác Vận hành <br />
                                            <span style={{ color: "#e6f85e" }}>
                                                Bãi đỗ xe Thông minh
                                            </span>
                                        </>
                                    ) : (
                                        <>
                                            Partner to Operate <br />
                                            <span style={{ color: "#e6f85e" }}>
                                                Smart Parking Network
                                            </span>
                                        </>
                                    )}
                                </h1>

                                <p
                                    style={{
                                        fontSize: 14.5,
                                        lineHeight: 1.6,
                                        color: "#a1a1aa",
                                        margin: "0 0 32px",
                                    }}
                                >
                                    {isVi
                                        ? "Gia nhập mạng lưới bãi đỗ xe số 1 Việt Nam. Số hóa quy trình quản lý, lấp đầy công suất và tăng trưởng doanh thu vượt trội."
                                        : "Join Vietnam's leading smart parking network. Digitize operations, maximize lot occupancy, and accelerate revenue growth."}
                                </p>

                                <div style={{ display: "grid", gap: 18 }}>
                                    <div
                                        style={{
                                            display: "flex",
                                            gap: 14,
                                            alignItems: "flex-start",
                                        }}
                                    >
                                        <div
                                            style={{
                                                width: 38,
                                                height: 38,
                                                borderRadius: 10,
                                                background: "#1c1c1f",
                                                border: "1px solid #27272a",
                                                display: "flex",
                                                alignItems: "center",
                                                justifyContent: "center",
                                                flexShrink: 0,
                                                color: "#e6f85e",
                                            }}
                                        >
                                            <svg
                                                width="20"
                                                height="20"
                                                viewBox="0 0 24 24"
                                                fill="none"
                                                stroke="currentColor"
                                                strokeWidth="2"
                                            >
                                                <path d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5" />
                                            </svg>
                                        </div>
                                        <div>
                                            <h2
                                                style={{
                                                    fontSize: 14.5,
                                                    fontWeight: 700,
                                                    margin: "0 0 3px",
                                                    color: "#f4f4f5",
                                                }}
                                            >
                                                {isVi
                                                    ? "Sơ đồ 2D & 3D tương tác"
                                                    : "Interactive 2D & 3D Maps"}
                                            </h2>
                                            <p
                                                style={{
                                                    fontSize: 13,
                                                    color: "#a1a1aa",
                                                    margin: 0,
                                                    lineHeight: 1.45,
                                                }}
                                            >
                                                {isVi
                                                    ? "Xem và quản lý từng vị trí ô đỗ, trạng thái xe vào/ra trực quan theo thời gian thực."
                                                    : "View and manage every slot, visual vehicle in/out activity in real time."}
                                            </p>
                                        </div>
                                    </div>

                                    <div
                                        style={{
                                            display: "flex",
                                            gap: 14,
                                            alignItems: "flex-start",
                                        }}
                                    >
                                        <div
                                            style={{
                                                width: 38,
                                                height: 38,
                                                borderRadius: 10,
                                                background: "#1c1c1f",
                                                border: "1px solid #27272a",
                                                display: "flex",
                                                alignItems: "center",
                                                justifyContent: "center",
                                                flexShrink: 0,
                                                color: "#e6f85e",
                                            }}
                                        >
                                            <svg
                                                width="20"
                                                height="20"
                                                viewBox="0 0 24 24"
                                                fill="none"
                                                stroke="currentColor"
                                                strokeWidth="2"
                                            >
                                                <path d="M12 1v22M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
                                            </svg>
                                        </div>
                                        <div>
                                            <h2
                                                style={{
                                                    fontSize: 14.5,
                                                    fontWeight: 700,
                                                    margin: "0 0 3px",
                                                    color: "#f4f4f5",
                                                }}
                                            >
                                                {isVi
                                                    ? "Tự động hóa doanh thu & VNPay"
                                                    : "Automated Payments & VNPay"}
                                            </h2>
                                            <p
                                                style={{
                                                    fontSize: 13,
                                                    color: "#a1a1aa",
                                                    margin: 0,
                                                    lineHeight: 1.45,
                                                }}
                                            >
                                                {isVi
                                                    ? "Thu phí tự động qua mã QR và ví điện tử, không lo thất thoát hay sai lệch sổ quỹ."
                                                    : "Seamless automated fee collection via QR codes and e-wallets."}
                                            </p>
                                        </div>
                                    </div>

                                    <div
                                        style={{
                                            display: "flex",
                                            gap: 14,
                                            alignItems: "flex-start",
                                        }}
                                    >
                                        <div
                                            style={{
                                                width: 38,
                                                height: 38,
                                                borderRadius: 10,
                                                background: "#1c1c1f",
                                                border: "1px solid #27272a",
                                                display: "flex",
                                                alignItems: "center",
                                                justifyContent: "center",
                                                flexShrink: 0,
                                                color: "#e6f85e",
                                            }}
                                        >
                                            <svg
                                                width="20"
                                                height="20"
                                                viewBox="0 0 24 24"
                                                fill="none"
                                                stroke="currentColor"
                                                strokeWidth="2"
                                            >
                                                <circle
                                                    cx="12"
                                                    cy="12"
                                                    r="10"
                                                />
                                                <path d="m9 12 2 2 4-4" />
                                            </svg>
                                        </div>
                                        <div>
                                            <h2
                                                style={{
                                                    fontSize: 14.5,
                                                    fontWeight: 700,
                                                    margin: "0 0 3px",
                                                    color: "#f4f4f5",
                                                }}
                                            >
                                                {isVi
                                                    ? "Tiếp cận hàng triệu tài xế"
                                                    : "Reach Millions of Drivers"}
                                            </h2>
                                            <p
                                                style={{
                                                    fontSize: 13,
                                                    color: "#a1a1aa",
                                                    margin: 0,
                                                    lineHeight: 1.45,
                                                }}
                                            >
                                                {isVi
                                                    ? "Bãi xe được xuất hiện nổi bật trên ứng dụng tìm kiếm của khách hàng có nhu cầu gửi xe."
                                                    : "Feature your parking lot directly to drivers actively seeking spots nearby."}
                                            </p>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </aside>

                        {/* ── Cột phải: Form Đăng ký (Brand Identity Đồng bộ) ── */}
                        <div
                            style={{
                                padding: "36px 40px",
                                display: "flex",
                                flexDirection: "column",
                                justifyContent: "center",
                            }}
                        >
                            <div style={{ marginBottom: 20 }}>
                                <div
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "space-between",
                                        marginBottom: 8,
                                        gap: 12,
                                    }}
                                >
                                    <h2
                                        style={{
                                            fontSize: 23,
                                            fontWeight: 800,
                                            color: "#09090b",
                                            margin: 0,
                                        }}
                                    >
                                        {isVi
                                            ? "Đăng ký tài khoản Chủ bãi"
                                            : "Register Lot Owner Account"}
                                    </h2>

                                    {/* Nút chuyển đổi Tiếng Việt / Tiếng Anh */}
                                    <div
                                        style={{
                                            display: "inline-flex",
                                            alignItems: "center",
                                            gap: 3,
                                            background: "#f4f4f5",
                                            padding: "3px 4px",
                                            borderRadius: 8,
                                            border: "1px solid #e4e4e7",
                                            flexShrink: 0,
                                        }}
                                    >
                                        <button
                                            type="button"
                                            onClick={() =>
                                                handleSetLanguage("vi")
                                            }
                                            style={{
                                                background: isVi
                                                    ? "#09090b"
                                                    : "transparent",
                                                color: isVi
                                                    ? "#ffffff"
                                                    : "#71717a",
                                                fontWeight: isVi ? 700 : 500,
                                                border: "none",
                                                borderRadius: 6,
                                                padding: "3px 8px",
                                                fontSize: 11.5,
                                                cursor: "pointer",
                                                transition: "all 0.15s ease",
                                            }}
                                            title="Tiếng Việt"
                                        >
                                            🇻🇳 VI
                                        </button>
                                        <button
                                            type="button"
                                            onClick={() =>
                                                handleSetLanguage("en")
                                            }
                                            style={{
                                                background: !isVi
                                                    ? "#09090b"
                                                    : "transparent",
                                                color: !isVi
                                                    ? "#ffffff"
                                                    : "#71717a",
                                                fontWeight: !isVi ? 700 : 500,
                                                border: "none",
                                                borderRadius: 6,
                                                padding: "3px 8px",
                                                fontSize: 11.5,
                                                cursor: "pointer",
                                                transition: "all 0.15s ease",
                                            }}
                                            title="English"
                                        >
                                            🇬🇧 EN
                                        </button>
                                    </div>
                                </div>

                                {/* Tiến trình 2 bước tuần tự trực quan (thay cho dòng Bước 1/2...) */}
                                <div
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 10,
                                        fontSize: 13,
                                        color: "#71717a",
                                        padding: "4px 0",
                                    }}
                                >
                                    <span
                                        style={{
                                            fontWeight: 700,
                                            color: "#09090b",
                                            display: "inline-flex",
                                            alignItems: "center",
                                            gap: 6,
                                        }}
                                    >
                                        <span
                                            style={{
                                                display: "inline-flex",
                                                alignItems: "center",
                                                justifyContent: "center",
                                                width: 20,
                                                height: 20,
                                                borderRadius: "50%",
                                                background: "#09090b",
                                                color: "#e6f85e",
                                                fontSize: 11,
                                                fontWeight: 800,
                                            }}
                                        >
                                            1
                                        </span>
                                        {isVi
                                            ? "Khởi tạo tài khoản"
                                            : "Create Account"}
                                    </span>
                                    <span
                                        style={{
                                            color: "#d4d4d8",
                                            userSelect: "none",
                                        }}
                                    >
                                        →
                                    </span>
                                    <span
                                        style={{
                                            color: "#a1a1aa",
                                            display: "inline-flex",
                                            alignItems: "center",
                                            gap: 6,
                                        }}
                                    >
                                        <span
                                            style={{
                                                display: "inline-flex",
                                                alignItems: "center",
                                                justifyContent: "center",
                                                width: 20,
                                                height: 20,
                                                borderRadius: "50%",
                                                background: "#f4f4f5",
                                                color: "#71717a",
                                                fontSize: 11,
                                                fontWeight: 700,
                                                border: "1px solid #e4e4e7",
                                            }}
                                        >
                                            2
                                        </span>
                                        {isVi
                                            ? "Xác thực Email"
                                            : "Verify Email"}
                                    </span>
                                </div>
                            </div>

                            {/* Banner chuyển sang đăng ký khách nếu nhầm */}
                            <div
                                style={{
                                    background: "#f4f4f5",
                                    border: "1px solid #e4e4e7",
                                    borderRadius: 10,
                                    padding: "9px 13px",
                                    marginBottom: 18,
                                    fontSize: 12.5,
                                    color: "#18181b",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "space-between",
                                }}
                            >
                                <span>
                                    {isVi
                                        ? "Bạn là tài xế tìm chỗ đỗ xe?"
                                        : "Are you a driver looking for parking?"}
                                </span>
                                <Link
                                    href="/sign-up"
                                    style={{
                                        color: "#09090b",
                                        fontWeight: 700,
                                        textDecoration: "underline",
                                    }}
                                >
                                    {isVi
                                        ? "Đăng ký tài khoản khách →"
                                        : "Register as guest →"}
                                </Link>
                            </div>

                            {/* Thông báo lỗi tổng quát */}
                            {submitError && (
                                <div
                                    style={{
                                        background: "#fef2f2",
                                        border: "1px solid #fecaca",
                                        borderRadius: 10,
                                        padding: "12px 14px",
                                        color: "#b91c1c",
                                        fontSize: 13,
                                        marginBottom: 16,
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 10,
                                    }}
                                >
                                    <svg
                                        width="18"
                                        height="18"
                                        viewBox="0 0 24 24"
                                        fill="none"
                                        stroke="currentColor"
                                        strokeWidth="2"
                                    >
                                        <circle cx="12" cy="12" r="10" />
                                        <line x1="12" y1="8" x2="12" y2="12" />
                                        <line
                                            x1="12"
                                            y1="16"
                                            x2="12.01"
                                            y2="16"
                                        />
                                    </svg>
                                    <div style={{ flex: 1 }}>
                                        <div>{submitError}</div>
                                        {emailConflict && (
                                            <div
                                                style={{
                                                    marginTop: 8,
                                                    paddingTop: 8,
                                                    borderTop:
                                                        "1px dashed #fca5a5",
                                                    fontSize: 12.5,
                                                }}
                                            >
                                                <p
                                                    style={{
                                                        margin: "0 0 6px 0",
                                                        color: "#991b1b",
                                                        fontWeight: 500,
                                                    }}
                                                >
                                                    {isVi
                                                        ? "Bạn đã có tài khoản Chủ bãi. Hãy đăng nhập để tiếp tục hoàn thiện hồ sơ pháp lý:"
                                                        : "You already have a Partner account. Please log in to complete your legal profile:"}
                                                </p>
                                                <Link
                                                    href={`/login?redirect=${encodeURIComponent("/owner/onboarding")}&role=owner&email=${encodeURIComponent(getValues("email") || "")}`}
                                                    style={{
                                                        display: "inline-flex",
                                                        alignItems: "center",
                                                        gap: 6,
                                                        marginTop: 4,
                                                        padding: "6px 14px",
                                                        borderRadius: 6,
                                                        background: "#b91c1c",
                                                        color: "#fff",
                                                        fontWeight: 600,
                                                        textDecoration: "none",
                                                        fontSize: 12,
                                                    }}
                                                >
                                                    👉{" "}
                                                    {isVi
                                                        ? "Đăng nhập để tiếp tục hoàn thiện hồ sơ →"
                                                        : "Log in to continue legal profile →"}
                                                </Link>
                                            </div>
                                        )}
                                    </div>
                                </div>
                            )}

                            <form
                                onSubmit={handleSubmit(onSubmit)}
                                noValidate
                                style={{ display: "grid", gap: 13 }}
                            >
                                {/* Họ và tên */}
                                <div>
                                    <label
                                        htmlFor="owner-fullName"
                                        style={{
                                            display: "block",
                                            fontSize: 13,
                                            fontWeight: 700,
                                            color: "#18181b",
                                            marginBottom: 5,
                                        }}
                                    >
                                        {isVi
                                            ? "Họ và tên người đại diện"
                                            : "Representative Full Name"}{" "}
                                        <span style={{ color: "#ef4444" }}>
                                            *
                                        </span>
                                    </label>
                                    <input
                                        id="owner-fullName"
                                        type="text"
                                        autoComplete="name"
                                        placeholder={
                                            isVi
                                                ? "Ví dụ: Nguyễn Văn An"
                                                : "e.g. John Doe"
                                        }
                                        {...register("fullName")}
                                        className={`owner-reg-input ${errors.fullName ? "has-error" : ""}`}
                                    />
                                    {errors.fullName && (
                                        <p
                                            style={{
                                                color: "#ef4444",
                                                fontSize: 12,
                                                margin: "4px 0 0",
                                            }}
                                        >
                                            {errors.fullName.message}
                                        </p>
                                    )}
                                </div>

                                {/* Hàng 2 cột: Email & SĐT */}
                                <div
                                    style={{
                                        display: "grid",
                                        gridTemplateColumns: "1fr 1fr",
                                        gap: 12,
                                    }}
                                    className="owner-reg-grid2"
                                >
                                    <div>
                                        <label
                                            htmlFor="owner-email"
                                            style={{
                                                display: "block",
                                                fontSize: 13,
                                                fontWeight: 700,
                                                color: "#18181b",
                                                marginBottom: 5,
                                            }}
                                        >
                                            {isVi
                                                ? "Email liên hệ"
                                                : "Contact Email"}{" "}
                                            <span style={{ color: "#ef4444" }}>
                                                *
                                            </span>
                                        </label>
                                        <input
                                            id="owner-email"
                                            type="email"
                                            autoComplete="email"
                                            placeholder="chu.bai@example.com"
                                            {...register("email")}
                                            className={`owner-reg-input ${errors.email ? "has-error" : ""}`}
                                        />
                                        {errors.email && (
                                            <p
                                                style={{
                                                    color: "#ef4444",
                                                    fontSize: 12,
                                                    margin: "4px 0 0",
                                                }}
                                            >
                                                {errors.email.message}
                                            </p>
                                        )}
                                    </div>

                                    <div>
                                        <label
                                            htmlFor="owner-phone"
                                            style={{
                                                display: "block",
                                                fontSize: 13,
                                                fontWeight: 700,
                                                color: "#18181b",
                                                marginBottom: 5,
                                            }}
                                        >
                                            {isVi
                                                ? "Số điện thoại di động"
                                                : "Mobile Phone"}{" "}
                                            <span style={{ color: "#ef4444" }}>
                                                *
                                            </span>
                                        </label>
                                        <input
                                            id="owner-phone"
                                            type="tel"
                                            autoComplete="tel"
                                            placeholder="0912345678"
                                            {...register("phone")}
                                            className={`owner-reg-input ${errors.phone ? "has-error" : ""}`}
                                        />
                                        {errors.phone && (
                                            <p
                                                style={{
                                                    color: "#ef4444",
                                                    fontSize: 12,
                                                    margin: "4px 0 0",
                                                }}
                                            >
                                                {errors.phone.message}
                                            </p>
                                        )}
                                    </div>
                                </div>

                                {/* Mật khẩu */}
                                <div>
                                    <label
                                        htmlFor="owner-password"
                                        style={{
                                            display: "block",
                                            fontSize: 13,
                                            fontWeight: 700,
                                            color: "#18181b",
                                            marginBottom: 5,
                                        }}
                                    >
                                        {isVi
                                            ? "Mật khẩu bảo mật"
                                            : "Password"}{" "}
                                        <span style={{ color: "#ef4444" }}>
                                            *
                                        </span>
                                    </label>
                                    <div style={{ position: "relative" }}>
                                        <input
                                            id="owner-password"
                                            type={
                                                passwordVisible
                                                    ? "text"
                                                    : "password"
                                            }
                                            autoComplete="new-password"
                                            placeholder={
                                                isVi
                                                    ? "Nhập mật khẩu an toàn"
                                                    : "Enter secure password"
                                            }
                                            {...register("password")}
                                            className={`owner-reg-input ${errors.password ? "has-error" : ""}`}
                                            style={{ paddingRight: 38 }}
                                        />
                                        <button
                                            type="button"
                                            onClick={() =>
                                                setPasswordVisible((v) => !v)
                                            }
                                            style={{
                                                position: "absolute",
                                                right: 10,
                                                top: "50%",
                                                transform: "translateY(-50%)",
                                                background: "none",
                                                border: "none",
                                                cursor: "pointer",
                                                color: "#71717a",
                                                padding: 4,
                                            }}
                                            aria-label={
                                                passwordVisible
                                                    ? isVi
                                                        ? "Ẩn mật khẩu"
                                                        : "Hide password"
                                                    : isVi
                                                      ? "Hiện mật khẩu"
                                                      : "Show password"
                                            }
                                        >
                                            <EyeIcon
                                                visible={passwordVisible}
                                            />
                                        </button>
                                    </div>
                                    {errors.password && (
                                        <p
                                            style={{
                                                color: "#ef4444",
                                                fontSize: 12,
                                                margin: "4px 0 0",
                                            }}
                                        >
                                            {errors.password.message}
                                        </p>
                                    )}

                                    {/* Thanh đo độ mạnh mật khẩu và gợi ý (giống hệt Hình 3) */}
                                    <div
                                        id="owner-password-strength"
                                        role="meter"
                                        aria-label={
                                            isVi
                                                ? "Độ mạnh mật khẩu"
                                                : "Password strength"
                                        }
                                        aria-valuemin={0}
                                        aria-valuemax={4}
                                        aria-valuenow={passwordStrength}
                                        aria-valuetext={passwordStrengthLabel}
                                        style={{ marginTop: 8 }}
                                    >
                                        <div
                                            style={{
                                                display: "grid",
                                                gridTemplateColumns:
                                                    "repeat(4, minmax(0, 1fr))",
                                                gap: 5,
                                                marginBottom: 5,
                                            }}
                                            aria-hidden="true"
                                        >
                                            {[1, 2, 3, 4].map((seg) => {
                                                const colors = [
                                                    "#e5e7eb",
                                                    "#c2413a",
                                                    "#c77719",
                                                    "#78a52a",
                                                    "#207d46",
                                                ];
                                                const fillColor =
                                                    colors[passwordStrength] ||
                                                    "#e5e7eb";
                                                return (
                                                    <span
                                                        key={seg}
                                                        style={{
                                                            height: 4,
                                                            borderRadius: 99,
                                                            background:
                                                                seg <=
                                                                passwordStrength
                                                                    ? fillColor
                                                                    : "#e5e7eb",
                                                            transition:
                                                                "background-color 0.2s ease",
                                                        }}
                                                    />
                                                );
                                            })}
                                        </div>
                                        <div
                                            style={{
                                                fontSize: 12,
                                                color: "#52525b",
                                                fontWeight: 600,
                                            }}
                                        >
                                            {passwordValue
                                                ? `${isVi ? "Độ mạnh mật khẩu" : "Password strength"}: ${passwordStrengthLabel}`
                                                : isVi
                                                  ? "Độ mạnh mật khẩu"
                                                  : "Password strength"}
                                        </div>
                                    </div>
                                    <p
                                        style={{
                                            fontSize: 12,
                                            color: passwordMeetsRequirements
                                                ? "#207d46"
                                                : "#71717a",
                                            margin: "4px 0 0",
                                            lineHeight: 1.4,
                                        }}
                                    >
                                        {passwordMeetsRequirements
                                            ? isVi
                                                ? "Mật khẩu đáp ứng đủ yêu cầu bảo mật."
                                                : "Password meets the requirements."
                                            : isVi
                                              ? "Sử dụng từ 8 ký tự trở lên kết hợp chữ hoa, chữ thường, số và ký tự đặc biệt."
                                              : "Use 8+ characters with uppercase, lowercase, a number, and a symbol."}
                                    </p>
                                </div>

                                {/* Xác nhận mật khẩu */}
                                <div>
                                    <label
                                        htmlFor="owner-confirmPassword"
                                        style={{
                                            display: "block",
                                            fontSize: 13,
                                            fontWeight: 700,
                                            color: "#18181b",
                                            marginBottom: 5,
                                        }}
                                    >
                                        {isVi
                                            ? "Xác nhận mật khẩu"
                                            : "Confirm Password"}{" "}
                                        <span style={{ color: "#ef4444" }}>
                                            *
                                        </span>
                                    </label>
                                    <div style={{ position: "relative" }}>
                                        <input
                                            id="owner-confirmPassword"
                                            type={
                                                confirmPasswordVisible
                                                    ? "text"
                                                    : "password"
                                            }
                                            autoComplete="new-password"
                                            placeholder={
                                                isVi
                                                    ? "Nhập lại mật khẩu"
                                                    : "Re-enter password"
                                            }
                                            {...register("confirmPassword")}
                                            className={`owner-reg-input ${errors.confirmPassword ? "has-error" : ""}`}
                                            style={{ paddingRight: 38 }}
                                        />
                                        <button
                                            type="button"
                                            onClick={() =>
                                                setConfirmPasswordVisible(
                                                    (v) => !v,
                                                )
                                            }
                                            style={{
                                                position: "absolute",
                                                right: 10,
                                                top: "50%",
                                                transform: "translateY(-50%)",
                                                background: "none",
                                                border: "none",
                                                cursor: "pointer",
                                                color: "#71717a",
                                                padding: 4,
                                            }}
                                            aria-label={
                                                confirmPasswordVisible
                                                    ? isVi
                                                        ? "Ẩn mật khẩu"
                                                        : "Hide password"
                                                    : isVi
                                                      ? "Hiện mật khẩu"
                                                      : "Show password"
                                            }
                                        >
                                            <EyeIcon
                                                visible={confirmPasswordVisible}
                                            />
                                        </button>
                                    </div>
                                    {errors.confirmPassword && (
                                        <p
                                            style={{
                                                color: "#ef4444",
                                                fontSize: 12,
                                                margin: "4px 0 0",
                                            }}
                                        >
                                            {errors.confirmPassword.message}
                                        </p>
                                    )}
                                </div>

                                {/* Đồng ý điều khoản */}
                                <div style={{ marginTop: 4 }}>
                                    <label
                                        style={{
                                            display: "flex",
                                            alignItems: "flex-start",
                                            gap: 10,
                                            fontSize: 12.5,
                                            color: "#52525b",
                                            cursor: "pointer",
                                        }}
                                    >
                                        <input
                                            type="checkbox"
                                            {...register("termsAccepted")}
                                            style={{
                                                marginTop: 2,
                                                accentColor: "#09090b",
                                                width: 16,
                                                height: 16,
                                                cursor: "pointer",
                                            }}
                                        />
                                        <span>
                                            {isVi
                                                ? "Tôi đồng ý với "
                                                : "I agree to the "}
                                            <Link
                                                href="/terms-and-conditions"
                                                target="_blank"
                                                style={{
                                                    color: "#09090b",
                                                    fontWeight: 600,
                                                    textDecoration: "underline",
                                                }}
                                            >
                                                {isVi
                                                    ? "Điều khoản đối tác bãi xe"
                                                    : "Partner Terms"}
                                            </Link>{" "}
                                            {isVi ? "và " : "and "}
                                            <Link
                                                href="/privacy-policy"
                                                target="_blank"
                                                style={{
                                                    color: "#09090b",
                                                    fontWeight: 600,
                                                    textDecoration: "underline",
                                                }}
                                            >
                                                {isVi
                                                    ? "Chính sách bảo mật"
                                                    : "Privacy Policy"}
                                            </Link>{" "}
                                            {isVi
                                                ? "của ParkWise."
                                                : "of ParkWise."}
                                        </span>
                                    </label>
                                    {errors.termsAccepted && (
                                        <p
                                            style={{
                                                color: "#ef4444",
                                                fontSize: 12,
                                                margin: "4px 0 0",
                                            }}
                                        >
                                            {errors.termsAccepted.message}
                                        </p>
                                    )}
                                </div>

                                {/* Nút Submit: Chữ "Tiếp tục" (Hình 4) */}
                                <button
                                    type="submit"
                                    disabled={isSubmitting}
                                    style={{
                                        width: "100%",
                                        height: 46,
                                        marginTop: 8,
                                        background: isSubmitting
                                            ? "#e4e4e7"
                                            : "#e6f85e",
                                        color: "#09090b",
                                        border: "none",
                                        borderRadius: 8,
                                        fontSize: 14.5,
                                        fontWeight: 700,
                                        cursor: isSubmitting
                                            ? "not-allowed"
                                            : "pointer",
                                        boxShadow:
                                            "0 2px 10px rgba(230, 248, 94, 0.25)",
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "center",
                                        gap: 8,
                                        transition: "all 0.2s ease",
                                    }}
                                >
                                    {isSubmitting ? (
                                        <>
                                            <span
                                                style={{
                                                    display: "inline-block",
                                                    width: 16,
                                                    height: 16,
                                                    border: "2px solid #09090b",
                                                    borderTopColor:
                                                        "transparent",
                                                    borderRadius: "50%",
                                                    animation:
                                                        "spin 0.8s linear infinite",
                                                }}
                                            />
                                            {isVi
                                                ? "Đang xử lý..."
                                                : "Processing..."}
                                        </>
                                    ) : isVi ? (
                                        "Tiếp tục"
                                    ) : (
                                        "Continue"
                                    )}
                                </button>
                            </form>

                            <div
                                style={{
                                    marginTop: 20,
                                    textAlign: "center",
                                    fontSize: 13,
                                    color: "#71717a",
                                }}
                            >
                                {isVi
                                    ? "Đã có tài khoản Đối tác?"
                                    : "Already have a Partner account?"}{" "}
                                <Link
                                    href="/login"
                                    style={{
                                        color: "#09090b",
                                        fontWeight: 700,
                                        textDecoration: "underline",
                                    }}
                                >
                                    {isVi ? "Đăng nhập ngay" : "Log in now"}
                                </Link>
                            </div>
                        </div>
                    </div>
                </div>
            </main>

            <ParkWiseFooter />

            <style>{`
                .parkwise-navbar.parkwise-navbar-dark {
                    background: #09090b !important;
                }
                .owner-reg-input {
                    width: 100% !important;
                    height: 42px !important;
                    padding: 8px 12px !important;
                    border-radius: 8px !important;
                    border: 1px solid #cbd5e1 !important;
                    font-size: 13.5px !important;
                    outline: none !important;
                    transition: border-color 0.2s ease, box-shadow 0.2s ease !important;
                    color: #09090b !important;
                    background: #ffffff !important;
                    box-sizing: border-box !important;
                }
                .owner-reg-input:focus {
                    border-color: #09090b !important;
                    box-shadow: 0 0 0 2px rgba(9, 9, 11, 0.12) !important;
                }
                .owner-reg-input.has-error {
                    border-color: #ef4444 !important;
                }
                .parkwise-owner-register-main {
                    width: 100% !important;
                    max-width: 100% !important;
                    margin: 0 auto !important;
                    display: flex !important;
                    flex-direction: column !important;
                    align-items: center !important;
                    justify-content: flex-start !important;
                    padding-top: calc(var(--parkwise-nav-height, 66px) + 32px) !important;
                    padding-bottom: 48px !important;
                }
                .owner-reg-wrapper {
                    width: 100% !important;
                    max-width: 100% !important;
                    margin: 0 auto !important;
                    display: flex !important;
                    justify-content: center !important;
                    align-items: center !important;
                }
                .owner-reg-container {
                    width: 100% !important;
                    max-width: 1040px !important;
                    margin: 0 auto !important;
                }
                @keyframes spin {
                    from { transform: rotate(0deg); }
                    to { transform: rotate(360deg); }
                }
                @media (max-width: 1023px) {
                    .parkwise-owner-register-main {
                        padding-top: calc(var(--parkwise-nav-height, 66px) + 28px) !important;
                        padding-bottom: 36px !important;
                    }
                }
                @media (max-width: 900px) {
                    .owner-reg-container {
                        grid-template-columns: 1fr !important;
                    }
                }
                @media (max-width: 767px) {
                    .parkwise-owner-register-main {
                        padding-top: calc(var(--parkwise-nav-height, 64px) + 24px) !important;
                        padding-bottom: 28px !important;
                    }
                    .owner-reg-wrapper {
                        padding: 0 16px !important;
                    }
                }
                @media (max-width: 540px) {
                    .owner-reg-grid2 {
                        grid-template-columns: 1fr !important;
                    }
                }
                @media (max-width: 480px) {
                    .owner-reg-wrapper {
                        padding: 0 12px !important;
                    }
                }
            `}</style>
        </div>
    );
}
