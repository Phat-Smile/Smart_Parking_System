'use client';

import React from 'react';
import { OwnerPageHeader } from '@/components/layout/OwnerPageHeader';
import { ReservationsTableV2 } from '@/features/owner/reservations/ReservationsTableV2';

export default function ReservationsPage() {
  return (
    <div style={{ maxWidth: 1400, margin: '0 auto', paddingBottom: 48 }}>
      <OwnerPageHeader
        breadcrumb="/owner/reservations"
        title="Đặt chỗ"
        subtitle="Quản lý các lượt đặt và giữ chỗ trước của khách hàng"
      />

      <ReservationsTableV2 />
    </div>
  );
}
