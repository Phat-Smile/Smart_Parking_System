import type { Metadata } from "next";
import { ParkWiseLegalPage } from "@/features/parkwise/ParkWiseLegalPage";
import { termsAndConditionsMarkdown } from "@/features/parkwise/legal/termsAndConditionsMarkdown";
import "../parkwise.css";

export const metadata: Metadata = {
    title: "Terms & Conditions | ParkWise",
    description: "ParkWise Terms & Conditions page",
};

export default async function TermsAndConditionsPage({
    searchParams,
}: {
    searchParams: Promise<{ from?: string | string[] }>;
}) {
    const fromProfile = (await searchParams).from === "complete-profile";
    return (
        <ParkWiseLegalPage
            title="Terms & Conditions"
            markdown={termsAndConditionsMarkdown}
            fromProfile={fromProfile}
        />
    );
}
