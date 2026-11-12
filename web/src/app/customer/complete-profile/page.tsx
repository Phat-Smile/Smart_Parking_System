import type { Metadata } from "next";
import { CompleteProfilePage } from "@/features/customer/CompleteProfilePage";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import { CustomerAccessGuard } from "@/features/customer/CustomerAccessGuard";
import "../../parkwise.css";
import "../../login/login.css";
import "@/features/customer/customer-shell.css";
import "./complete-profile.css";

export const metadata: Metadata = {
    title: "Complete Your Profile | ParkWise",
    description:
        "Add the required information to complete your ParkWise profile.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default function CompleteProfileRoute() {
    return (
        <CustomerAccessGuard>
            <CompleteProfilePage fontClassName={parkwiseFont.className} />
        </CustomerAccessGuard>
    );
}
