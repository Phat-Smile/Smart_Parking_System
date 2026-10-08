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

export default function VerifyOtpRoute() {
    return <ParkWiseOtpPage fontClassName={manrope.className} />;
}
