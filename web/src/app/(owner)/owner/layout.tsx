"use client";

import { Be_Vietnam_Pro } from "next/font/google";
import Link from "next/link";
import { ReactNode, useEffect } from "react";
import "../../../styles/owner-v4.css";
import { MSWProvider } from "../../../components/providers/MSWProvider";
import { QueryProvider } from "../../../components/providers/QueryProvider";
import { useParkingStore } from "../../../store/useParkingStore";
import { MOCK_SLOTS } from "../../../mocks/seed";

const beVietnamPro = Be_Vietnam_Pro({
  subsets: ["latin", "vietnamese"],
  weight: ["400", "500", "600", "700", "800"],
  variable: "--font-be-vietnam",
});

// Client component to sync mock data globally
function GlobalMockSync() {
  const setSlots = useParkingStore(s => s.setSlots);
  const slots = useParkingStore(s => s.slots);
  useEffect(() => {
    if (Object.keys(slots).length === 0) {
      setSlots(MOCK_SLOTS);
    }
  }, [slots, setSlots]);
  return null;
}

export default function OwnerLayout({ children }: { children: ReactNode }) {
  return (
    <div className={`owner-portal ${beVietnamPro.className} font-sans min-h-screen`} style={{ background: 'var(--bg)', color: 'var(--ink)' }}>
      <MSWProvider>
        <QueryProvider>
          <GlobalMockSync />
          <header>
            <h1>Smart Parking <i>AI</i> · Chủ bãi</h1>
            <nav id="nav">
              <Link href="/owner/dashboard">Tổng quan</Link>
              <Link href="/owner/parking-lots">Bãi của tôi</Link>
              <Link href="/owner/dashboard/map">Sơ đồ 2D / 3D</Link>
              <Link href="/owner/reservations">Đặt chỗ</Link>
              <Link href="/owner/sessions">Phiên đỗ xe</Link>
              <Link href="/owner/reports">Báo cáo doanh thu</Link>
            </nav>
          </header>
          <main id="m">
            {children}
          </main>
        </QueryProvider>
      </MSWProvider>
    </div>
  );
}

