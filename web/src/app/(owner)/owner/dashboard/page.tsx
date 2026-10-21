'use client';

import React, { useEffect } from 'react';
import { OwnerPageHeader } from '@/components/layout/OwnerPageHeader';
import { DashboardKpisV2 } from '@/components/dashboard/DashboardKpisV2';
import { TrafficChart } from '@/components/dashboard/TrafficChart';
import { WeeklyParkingChart } from '@/components/dashboard/WeeklyParkingChart';
import { FloorCardsV2 } from '@/components/dashboard/FloorCardsV2';
import { StatusDonutV2 } from '@/components/dashboard/StatusDonutV2';
import { RecentActivityV2 } from '@/components/dashboard/RecentActivityV2';
import { useOwnerStore } from '@/store/useOwnerStore';

export default function DashboardPage() {
  const simulateTick = useOwnerStore((s) => s.simulateTick);

  useEffect(() => {
    const timer = setInterval(() => {
      simulateTick();
    }, 5000);
    return () => clearInterval(timer);
  }, [simulateTick]);

  return (
    <div style={{ maxWidth: 1400, margin: '0 auto', paddingBottom: 48 }}>
      <OwnerPageHeader
        breadcrumb="/owner/dashboard"
        title="Tổng quan"
        subtitle="Doanh thu, xe trong bãi và tình hình vận hành"
      />

      {/* Khối 1: 4 Thẻ tổng (KPIs) */}
      <DashboardKpisV2 />

      {/* Khối 2: Hàng 2 biểu đồ (Lưu lượng xe + Lượt đỗ trong tuần) */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'minmax(0, 1.35fr) minmax(380px, 1.15fr)',
          gap: 16,
          marginBottom: 20,
        }}
        className="ov2-row-traffic"
      >
        <TrafficChart />
        <WeeklyParkingChart />
      </div>

      {/* Khối 3: Khung "Theo từng tầng" (5 thẻ, nằm trên cặp Trạng thái ô / Hoạt động gần đây) */}
      <div style={{ marginBottom: 20 }}>
        <FloorCardsV2 />
      </div>

      {/* Khối 4: Hàng cuối (Trạng thái ô 1/3 + Hoạt động gần đây 2/3) */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'minmax(300px, 1.1fr) minmax(0, 1.9fr)',
          gap: 16,
        }}
        className="ov2-row-status"
      >
        <StatusDonutV2 />
        <RecentActivityV2 />
      </div>
    </div>
  );
}
