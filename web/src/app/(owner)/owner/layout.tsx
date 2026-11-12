"use client";

import React, { ReactNode, useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { Be_Vietnam_Pro } from "next/font/google";
import { OwnerSidebar } from "../../../components/layout/OwnerSidebar";
import { loadOwnerLots } from "@/features/owner/lots/ownerLotsSync";
import "../../../styles/owner-v4.css";

const beVietnamPro = Be_Vietnam_Pro({
    subsets: ["latin", "vietnamese"],
    weight: ["400", "500", "600", "700", "800"],
    variable: "--font-be-vietnam",
    display: "swap",
});

export default function OwnerRootLayout({ children }: { children: ReactNode }) {
    const pathname = usePathname();
    const isAuthPage =
        pathname === "/owner/register" ||
        pathname?.startsWith("/owner/register") ||
        pathname === "/owner/onboarding" ||
        pathname?.startsWith("/owner/onboarding");
    const [lotsError, setLotsError] = useState("");

    // The portal shows the signed-in owner's lots from Parking (D02.5), not the design list.
    useEffect(() => {
        if (isAuthPage) return;
        loadOwnerLots()
            .then(() => setLotsError(""))
            .catch(() =>
                setLotsError(
                    "Không tải được danh sách bãi của bạn từ máy chủ. Vui lòng đăng nhập lại hoặc thử lại sau.",
                ),
            );
    }, [isAuthPage]);

    if (isAuthPage) {
        return (
            <div
                className={beVietnamPro.className}
                style={{ width: "100%", minHeight: "100vh" }}
            >
                {children}
            </div>
        );
    }

    return (
        <div className={`app ${beVietnamPro.className}`}>
            <OwnerSidebar />
            <main id="m">
                {lotsError && (
                    <p
                        role="alert"
                        style={{
                            margin: "12px 16px",
                            padding: "10px 14px",
                            borderRadius: 8,
                            background: "#fef2f2",
                            color: "#b91c1c",
                        }}
                    >
                        {lotsError}
                    </p>
                )}
                {children}
            </main>
        </div>
    );
}
