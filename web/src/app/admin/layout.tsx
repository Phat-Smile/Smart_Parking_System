import type { Metadata } from "next";
import { Manrope } from "next/font/google";
import { AdminShell } from "@/features/admin/AdminShell";
import "./admin.css";

const manrope = Manrope({ subsets: ["latin"], display: "swap" });

export const metadata: Metadata = {
    title: "Admin | ParkWise",
    description:
        "Review parking lot applications and manage ParkWise accounts.",
    robots: { index: false },
};

export default function AdminLayout({
    children,
}: Readonly<{ children: React.ReactNode }>) {
    return (
        <div className={manrope.className}>
            <AdminShell>{children}</AdminShell>
        </div>
    );
}
