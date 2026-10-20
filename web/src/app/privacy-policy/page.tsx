import type { Metadata } from "next";
import { ParkWiseLegalPage } from "@/features/parkwise/ParkWiseLegalPage";
import { privacyPolicyMarkdown } from "@/features/parkwise/legal/privacyPolicyMarkdown";
import "../parkwise.css";

export const metadata: Metadata = {
    title: "Privacy Policy | ParkWise",
    description: "ParkWise Privacy Policy page",
};

export default function PrivacyPolicyPage() {
    return (
        <ParkWiseLegalPage
            title="Privacy Policy"
            markdown={privacyPolicyMarkdown}
        />
    );
}
