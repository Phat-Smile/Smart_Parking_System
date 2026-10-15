import type { Metadata } from "next";
import { Manrope } from "next/font/google";
import { SecuritySettingsPage } from "@/features/auth/SecuritySettingsPage";
import "../../parkwise.css";
import "../../login/login.css";
import "../../forgot-password/forgot-password.css";

const manrope = Manrope({ subsets: ["latin"], display: "swap" });
export const metadata: Metadata = { title: "Account Security | ParkWise" };

export default function SecurityRoute() {
    return <SecuritySettingsPage fontClassName={manrope.className} />;
}
