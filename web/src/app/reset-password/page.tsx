import type { Metadata } from "next";
import { Manrope } from "next/font/google";
import { ParkWiseResetPasswordPage } from "@/features/auth/ParkWiseResetPasswordPage";
import "../parkwise.css";
import "../login/login.css";
import "../forgot-password/forgot-password.css";
import "./reset-password.css";

const manrope = Manrope({ subsets: ["latin"], display: "swap" });

export const metadata: Metadata = {
    title: "Reset Password | ParkWise",
    description: "Create a new password for your ParkWise account.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default function ResetPasswordRoute() {
    return <ParkWiseResetPasswordPage fontClassName={manrope.className} />;
}
