import type { Metadata } from "next";
import { GuestPage } from "@/features/guest";

export const metadata: Metadata = {
    title: "Smart Parking System - Find parking before you arrive",
    description:
        "See real-time availability, compare nearby parking, and choose the best spot before you go.",
};

export default function GuestRoute() {
    return <GuestPage />;
}
