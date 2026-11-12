import type { Metadata } from "next";
import { VneidConsentPage } from "@/features/simulator/vneid/VneidConsentPage";
import { VneidMobileSimulator } from "@/features/simulator/vneid/VneidMobileSimulator";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import "./vneid-simulator.css";

export const metadata: Metadata = {
    title: "Liên kết định danh điện tử — Demo | ParkWise",
    description:
        "Mô phỏng chia sẻ hồ sơ mẫu với ParkWise. Không có giá trị xác thực.",
};

export default async function VneidSimulatorPage({
    searchParams,
}: {
    searchParams: Promise<{ return?: string }>;
}) {
    const { return: returnTarget } = await searchParams;
    return returnTarget === "complete-profile" ? (
        <VneidConsentPage fontClassName={parkwiseFont.className} />
    ) : (
        <VneidMobileSimulator />
    );
}
