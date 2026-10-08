import type { Metadata } from "next";
import { Manrope } from "next/font/google";
import { ParkWiseSignUpPage } from "@/features/auth/ParkWiseSignUpPage";
import "../parkwise.css";
import "../login/login.css";
import "./sign-up.css";

const manrope = Manrope({ subsets: ["latin"], display: "swap" });

export const metadata: Metadata = {
    title: "Sign Up | ParkWise",
    description: "Create your ParkWise account.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default function SignUpRoute() {
    return <ParkWiseSignUpPage fontClassName={manrope.className} />;
}
