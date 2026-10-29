import type { Metadata } from "next";
import { Manrope } from "next/font/google";
import { OwnerRegisterPage } from "@/features/owner/auth/OwnerRegisterPage";
import "@/app/parkwise.css";
import "@/app/login/login.css";

const manrope = Manrope({ subsets: ["latin", "vietnamese"], display: "swap" });

export const metadata: Metadata = {
    title: "Đăng ký Đối tác Chủ bãi đỗ xe | ParkWise Partner",
    description:
        "Đăng ký tài khoản Đối tác Chủ bãi đỗ xe ParkWise để tối ưu công suất và doanh thu với sơ đồ 2D/3D thông minh.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default function OwnerRegisterRoute() {
    return <OwnerRegisterPage fontClassName={manrope.className} />;
}
