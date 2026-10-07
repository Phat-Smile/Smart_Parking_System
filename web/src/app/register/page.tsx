import type { Metadata } from "next";
import { RegisterPage } from "@/features/auth/register/RegisterPage";

export const metadata: Metadata = {
    title: "Create Account | Smart Parking System",
    description: "Create a Smart Parking System account.",
};

export default function RegisterRoute() {
    return <RegisterPage />;
}
