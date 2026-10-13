import type { Metadata } from "next";
import { Manrope } from "next/font/google";
import { MyVehiclesPage } from "@/features/customer/MyVehiclesPage";
import "../../parkwise.css";
import "./vehicles-v2.css";

const manrope = Manrope({ subsets: ["latin"], display: "swap" });

export const metadata: Metadata = {
    title: "My Vehicles | ParkWise",
    description: "Manage the vehicles you use with ParkWise.",
};

export default function VehiclesRoute() {
    return <MyVehiclesPage fontClassName={manrope.className} />;
}
