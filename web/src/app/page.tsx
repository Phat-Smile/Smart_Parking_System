import type { Metadata } from "next";
import { ParkWiseLanding } from "@/features/parkwise/ParkWiseLanding";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import "./parkwise.css";
import "./parkwise-motion.css";

export const metadata: Metadata = {
    title: "ParkWise | Find Convenient and Affordable Parking",
    description:
        "Explore ParkWise and learn how registered drivers can plan parking.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default function Home() {
    return <ParkWiseLanding fontClassName={parkwiseFont.className} />;
}
