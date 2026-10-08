import type { Metadata } from "next";
import { Manrope } from "next/font/google";
import { ParkWiseLanding } from "@/features/parkwise/ParkWiseLanding";
import "./parkwise.css";

const manrope = Manrope({ subsets: ["latin"], display: "swap" });

export const metadata: Metadata = {
    title: "ParkWise | Find Convenient and Affordable Parking",
    description:
        "Explore ParkWise and learn how registered drivers can plan parking.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default function Home() {
    return <ParkWiseLanding fontClassName={manrope.className} />;
}
