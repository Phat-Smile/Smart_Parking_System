import type { Metadata } from "next";
import { Manrope } from "next/font/google";
import { CompleteProfilePage } from "@/features/customer/CompleteProfilePage";
import "../../parkwise.css";
import "../../login/login.css";
import "./complete-profile.css";

const manrope = Manrope({ subsets: ["latin", "vietnamese"], display: "swap" });

export const metadata: Metadata = {
    title: "Complete Your Profile | ParkWise",
    description: "Add the required information to complete your ParkWise profile.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default function CompleteProfileRoute() {
    return <CompleteProfilePage fontClassName={manrope.className} />;
}
