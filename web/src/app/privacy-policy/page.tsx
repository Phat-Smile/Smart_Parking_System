import type { Metadata } from "next";
import { ParkWiseLegalPage } from "@/features/parkwise/ParkWiseLegalPage";
import { privacyPolicyMarkdown } from "@/features/parkwise/legal/privacyPolicyMarkdown";
import "../parkwise.css";

export const metadata: Metadata = {
    title: "Privacy Policy | ParkWise",
    description: "ParkWise Privacy Policy page",
};

export default async function PrivacyPolicyPage({
    searchParams,
}: {
    searchParams: Promise<{ from?: string | string[] }>;
}) {
    const fromProfile = (await searchParams).from === "complete-profile";
    return (
        <ParkWiseLegalPage
            title="Privacy Policy"
            markdown={privacyPolicyMarkdown}
            fromProfile={fromProfile}
        />
    );
}
