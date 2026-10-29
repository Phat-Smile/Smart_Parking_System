import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { PreviewParkingLotDetailPage } from "@/features/customer/PreviewParkingLotDetailPage";
import { previewParkingLots } from "@/features/customer/previewParkingLots";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import "../../../parkwise.css";
import "@/features/customer/customer-shell.css";
import "../../[id]/parking-detail.css";

export const metadata: Metadata = {
    title: "Parking Preview | ParkWise",
    description: "Preview parking lot details with sample data.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default async function PreviewParkingLotRoute({
    params,
}: {
    params: Promise<{ id: string }>;
}) {
    const { id } = await params;
    const lot = previewParkingLots.find((item) => item.id === id);
    if (!lot) notFound();
    return (
        <PreviewParkingLotDetailPage
            lot={lot}
            fontClassName={parkwiseFont.className}
        />
    );
}
