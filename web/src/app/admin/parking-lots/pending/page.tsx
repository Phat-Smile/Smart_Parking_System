import type { Metadata } from "next";
import { ParkingLotsListPage } from "@/features/admin/ParkingLotsListPage";

export const metadata: Metadata = {
    title: "Pending parking lots | ParkWise Admin",
};

export default function Page() {
    return <ParkingLotsListPage mode="pending" />;
}
