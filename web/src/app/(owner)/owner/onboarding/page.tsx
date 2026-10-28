import type { Metadata } from "next";
import { Manrope } from "next/font/google";
import { OwnerOnboardingWizard } from "@/features/owner/onboarding/OwnerOnboardingWizard";

const manrope = Manrope({ subsets: ["latin", "vietnamese"], display: "swap" });

export const metadata: Metadata = {
  title: "Hoàn tất Hồ sơ Đối tác Chủ bãi đỗ xe | ParkWise Partner",
  description: "Cung cấp thông tin pháp lý và giấy tờ bãi đỗ xe để kích hoạt tài khoản đối tác ParkWise.",
  icons: { icon: "/images/parkwise/logo.png" },
};

export default function OwnerOnboardingRoute() {
  return <OwnerOnboardingWizard fontClassName={manrope.className} />;
}
