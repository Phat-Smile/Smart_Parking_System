import type { Metadata } from "next";
import { CustomerParkingLotDetailPage } from "@/features/customer/CustomerParkingLotDetailPage";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import "../../parkwise.css";
import "@/features/customer/customer-shell.css";
import "./parking-detail.css";

export const metadata: Metadata = {
    title: "Parking Details | ParkWise",
    description: "View location and opening hours for an approved parking lot.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default async function ParkingLotDetailRoute({
    params,
}: {
    params: Promise<{ id: string }>;
}) {
    const { id } = await params;
    return (
        <CustomerParkingLotDetailPage
            key={id}
            id={id}
            fontClassName={parkwiseFont.className}
        />
    );
}
