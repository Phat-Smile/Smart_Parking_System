import type { Metadata } from "next";
import { Manrope } from "next/font/google";
import { ParkWiseOtpPage } from "@/features/auth/ParkWiseOtpPage";
import "../parkwise.css";
import "../login/login.css";
import "./verify-otp.css";

const manrope = Manrope({ subsets: ["latin"], display: "swap" });

export const metadata: Metadata = {
    title: "Verify Email | ParkWise",
    description: "Enter your email verification code for ParkWise.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export const dynamic = "force-dynamic";

export default async function VerifyOtpRoute({
    searchParams,
}: {
    searchParams: Promise<{ email?: string | string[]; role?: string | string[] }>;
}) {
    const params = await searchParams;
    const rawEmail = Array.isArray(params?.email) ? params.email[0] : params?.email;
    const rawRole = Array.isArray(params?.role) ? params.role[0] : params?.role;
    const email = typeof rawEmail === "string" ? rawEmail.trim() : "";
    const role = typeof rawRole === "string" ? rawRole.trim() : "";
    return (
        <ParkWiseOtpPage
            fontClassName={manrope.className}
            initialEmail={email}
            role={role}
        />
    );
}
