import type { Metadata } from "next";
import { ParkWiseErrorPage } from "@/features/parkwise/ParkWiseErrorPage";
import "../parkwise.css";
import "../error-pages.css";

export const metadata: Metadata = {
    title: "Access restricted | ParkWise",
    description: "You don't have access to this ParkWise page.",
};

export default function ForbiddenPage() {
    return <ParkWiseErrorPage kind="forbidden" />;
}
