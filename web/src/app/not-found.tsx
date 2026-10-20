import type { Metadata } from "next";
import { ParkWiseErrorPage } from "@/features/parkwise/ParkWiseErrorPage";
import "./parkwise.css";
import "./error-pages.css";

export const metadata: Metadata = {
    title: "Page not found | ParkWise",
    description: "The ParkWise page you requested could not be found.",
};

export default function NotFound() {
    return <ParkWiseErrorPage kind="not-found" />;
}
