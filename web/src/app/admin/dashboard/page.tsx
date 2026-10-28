import type { Metadata } from "next";
import { AdminDashboardPage } from "@/features/admin/AdminDashboardPage";

export const metadata: Metadata = { title: "Dashboard | ParkWise Admin" };

export default function Page() {
    return <AdminDashboardPage />;
}
