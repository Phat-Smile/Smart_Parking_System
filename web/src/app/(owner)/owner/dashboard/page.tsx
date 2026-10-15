'use client';

import React, { useEffect } from 'react';
import { OwnerPageHeader as PageHeader } from '@/components/layout/OwnerPageHeader';
import { DashboardKpis } from '@/components/dashboard/DashboardKpis';
import { DashboardRevenueCard } from '@/components/dashboard/DashboardRevenueCard';
import { StatusDonut } from '@/components/dashboard/StatusDonut';
import { FloorBars } from '@/components/dashboard/FloorBars';
import { RecentActivity } from '@/components/dashboard/RecentActivity';
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
    <>
      <PageHeader
        breadcrumb="/owner/dashboard"
        title="Tổng quan"
        subtitle="Doanh thu, xe trong bãi và tình hình vận hành"
      />

      <DashboardKpis />

      <div className="grid">
        <DashboardRevenueCard />

        <div className="card c4">
          <div className="wh">
            <h3>Trạng thái ô</h3>
          </div>
          <StatusDonut />
        </div>

        <div className="card c4">
          <div className="wh">
            <h3>Theo từng tầng</h3>
          </div>
          <FloorBars />
        </div>

        <div className="card c8">
          <div className="wh">
            <h3>Hoạt động gần đây</h3>
          </div>
          <RecentActivity />
        </div>
      </div>
    </>
  );
}
