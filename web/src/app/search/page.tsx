import type { Metadata } from "next";
import { CustomerFindParkingPage } from "@/features/customer/CustomerFindParkingPage";
import { CustomerAccessGuard } from "@/features/customer/CustomerAccessGuard";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import "@fortawesome/fontawesome-free/css/all.min.css";
import "../parkwise.css";
import "@/features/customer/customer-shell.css";
import "./search.css";

export const metadata: Metadata = {
    title: "Find Parking | ParkWise",
    description: "Explore parking locations with ParkWise.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default async function FindParkingRoute({
    searchParams,
}: {
    searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
    const params = await searchParams;
    const initialSearch = {
        destination:
            typeof params.destination === "string" ? params.destination : "",
        date: typeof params.date === "string" ? params.date : "",
        time: typeof params.time === "string" ? params.time : "",
        vehicle:
            params.vehicle === "car" || params.vehicle === "motorbike"
                ? params.vehicle
                : "any",
    };
    return (
        <CustomerAccessGuard>
            <CustomerFindParkingPage
                fontClassName={parkwiseFont.className}
                initialSearch={initialSearch}
            />
        </CustomerAccessGuard>
    );
}
