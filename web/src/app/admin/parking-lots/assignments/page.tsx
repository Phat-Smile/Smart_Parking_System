import type { Metadata } from "next";
import { AdminLotAssignmentsPage } from "@/features/admin/AdminLotAssignmentsPage";

export const metadata: Metadata = {
    title: "Phân công Nhân viên trực bãi · ParkWise Admin",
    description: "Quản lý phân công nhân viên và chủ bãi cho từng bãi đỗ xe",
};

export default function AdminLotAssignmentsRoute() {
    return <AdminLotAssignmentsPage />;
}
