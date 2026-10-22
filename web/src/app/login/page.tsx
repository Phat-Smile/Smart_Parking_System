import { Suspense } from "react";
import type { Metadata } from "next";
import { Manrope } from "next/font/google";
import { ParkWiseLoginPage } from "@/features/auth/ParkWiseLoginPage";
import "../parkwise.css";
import "./login.css";

const manrope = Manrope({ subsets: ["latin"], display: "swap" });

export const metadata: Metadata = {
    title: "Login | ParkWise",
    description: "Sign in to your ParkWise account.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default function LoginRoute() {
    return (
        <Suspense fallback={<div className="parkwise-login-loading">Loading...</div>}>
            <ParkWiseLoginPage fontClassName={manrope.className} />
        </Suspense>
    );
}
