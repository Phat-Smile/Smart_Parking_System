import type { Metadata } from "next";
import { AdminAuditLogsPage } from "@/features/admin/AdminAuditLogsPage";

export const metadata: Metadata = {
    title: "Audit Logs & Reconciliation | ParkWise Admin",
};

export default function Page() {
    return <AdminAuditLogsPage />;
}
