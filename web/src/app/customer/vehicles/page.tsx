import type { Metadata } from "next";
import { MyVehiclesPage } from "@/features/customer/MyVehiclesPage";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import { CustomerAccessGuard } from "@/features/customer/CustomerAccessGuard";
import "../../parkwise.css";
import "@/features/customer/customer-shell.css";
import "./vehicles-v2.css";

export const metadata: Metadata = {
    title: "My Vehicles | ParkWise",
    description: "Manage the vehicles you use with ParkWise.",
};

export default function VehiclesRoute() {
    return (
        <CustomerAccessGuard>
            <MyVehiclesPage fontClassName={parkwiseFont.className} />
        </CustomerAccessGuard>
    );
}
