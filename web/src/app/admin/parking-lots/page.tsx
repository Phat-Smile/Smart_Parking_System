import type { Metadata } from "next";
import { ParkingLotsListPage } from "@/features/admin/ParkingLotsListPage";

export const metadata: Metadata = { title: "Parking lots | ParkWise Admin" };

const allowed = ["Pending", "Approved", "Rejected"] as const;

export default async function Page({
    searchParams,
}: {
    searchParams: Promise<{ status?: string }>;
}) {
    const { status } = await searchParams;
    const initial = allowed.find((value) => value === status) ?? "All";
    return (
        <ParkingLotsListPage key={initial} mode="all" initialStatus={initial} />
    );
}
