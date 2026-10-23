import type { Metadata } from "next";
import "./staff.css";
import { StaffShell } from "@/features/staff/StaffShell";

export const metadata: Metadata = {
    title: "Staff Barrier Station | ParkWise",
    description:
        "Gate control, ANPR detection, session management, and fee collection.",
};

export default function StaffLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    return <StaffShell>{children}</StaffShell>;
}
