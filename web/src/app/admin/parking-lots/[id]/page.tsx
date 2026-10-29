import type { Metadata } from "next";
import { ParkingLotReviewPage } from "@/features/admin/ParkingLotReviewPage";

export const metadata: Metadata = {
    title: "Review parking lot | ParkWise Admin",
};

export default async function Page({
    params,
}: {
    params: Promise<{ id: string }>;
}) {
    const { id } = await params;
    return <ParkingLotReviewPage key={id} id={decodeURIComponent(id)} />;
}
