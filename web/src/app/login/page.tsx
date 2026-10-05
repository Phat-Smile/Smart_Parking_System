import type { Metadata } from "next";
import { LoginPage } from "@/features/auth/LoginPage";

export const metadata: Metadata = {
    title: "Log In | Smart Parking System",
    description: "Sign in to your Smart Parking System account.",
};

export default function LoginRoute() {
    return <LoginPage />;
}
