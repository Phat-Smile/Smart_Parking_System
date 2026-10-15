import type { Metadata } from "next";
import { Manrope } from "next/font/google";
import { ParkWiseForgotPasswordPage } from "@/features/auth/ParkWiseForgotPasswordPage";
import "../parkwise.css";
import "../login/login.css";
import "./forgot-password.css";

const manrope = Manrope({ subsets: ["latin"], display: "swap" });

export const metadata: Metadata = {
    title: "Forgot Password | ParkWise",
    description: "Request a ParkWise password reset link.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default function ForgotPasswordRoute() {
    return <ParkWiseForgotPasswordPage fontClassName={manrope.className} />;
}
