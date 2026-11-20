import type { Metadata } from "next";
import { ParkWiseLanding } from "@/features/parkwise/ParkWiseLanding";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import "./parkwise.css";
import "./parkwise-motion.css";

export const metadata: Metadata = {
    title: "ParkWise | Tìm kiếm chỗ đỗ xe thuận tiện và tiết kiệm",
    description:
        "Khám phá ParkWise và tìm hiểu cách người lái xe có thể lên kế hoạch đỗ xe thuận tiện và tiết kiệm.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default function Home() {
    return <ParkWiseLanding fontClassName={parkwiseFont.className} />;
}
