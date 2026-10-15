'use client';

import React, { useEffect } from 'react';
import { OwnerPageHeader } from '@/components/layout/OwnerPageHeader';
import { DashboardKpisV2 } from '@/components/dashboard/DashboardKpisV2';
import { TrafficChart } from '@/components/dashboard/TrafficChart';
import { WeeklyParkingChart } from '@/components/dashboard/WeeklyParkingChart';
import { StatusDonutV2 } from '@/components/dashboard/StatusDonutV2';
import { RecentActivityV2 } from '@/components/dashboard/RecentActivityV2';
import { FloorCardsV2 } from '@/components/dashboard/FloorCardsV2';
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

      {/* 4 Thẻ KPI */}
      <DashboardKpisV2 />

      {/* Hàng 1: Lưu lượng xe (2/3) + Lượt đỗ trong tuần (1/3) */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'minmax(0, 1.9fr) minmax(320px, 1.1fr)',
          gap: 16,
          marginBottom: 20,
        }}
        className="ov2-row-traffic"
      >
        <TrafficChart />
        <WeeklyParkingChart />
      </div>

      {/* Hàng 2: Trạng thái ô (1/3) + Hoạt động gần đây (2/3) */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'minmax(300px, 1.1fr) minmax(0, 1.9fr)',
          gap: 16,
          marginBottom: 20,
        }}
        className="ov2-row-status"
      >
        <StatusDonutV2 />
        <RecentActivityV2 />
      </div>

      {/* Hàng 3: Theo từng tầng (5 thẻ) */}
      <FloorCardsV2 />
    </div>
  );
}
