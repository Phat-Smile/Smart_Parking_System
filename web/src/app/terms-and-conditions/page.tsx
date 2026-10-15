import type { Metadata } from "next";
import { ParkWiseLegalPage } from "@/features/parkwise/ParkWiseLegalPage";
import { termsAndConditionsMarkdown } from "@/features/parkwise/legal/termsAndConditionsMarkdown";
import "../parkwise.css";

export const metadata: Metadata = {
    title: "Terms & Conditions | ParkWise",
    description: "ParkWise Terms & Conditions page",
};

export default function TermsAndConditionsPage() {
    return (
        <ParkWiseLegalPage
            title="Terms & Conditions"
            markdown={termsAndConditionsMarkdown}
        />
    );
}
