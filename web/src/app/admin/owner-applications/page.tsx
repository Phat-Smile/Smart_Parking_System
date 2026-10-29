import type { Metadata } from "next";
import { AdminOwnerApplicationsPage } from "@/features/admin/AdminOwnerApplicationsPage";

export const metadata: Metadata = {
    title: "Duyệt hồ sơ Chủ bãi · ParkWise Admin",
    description: "Xét duyệt và phê duyệt hồ sơ đối tác chủ bãi đỗ xe",
};

export default function AdminOwnerApplicationsRoute() {
    return <AdminOwnerApplicationsPage />;
}
