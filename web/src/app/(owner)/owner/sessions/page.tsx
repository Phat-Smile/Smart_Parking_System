'use client';

import React from 'react';
import { OwnerPageHeader } from '@/components/layout/OwnerPageHeader';
import { SessionsTableV2 } from '@/features/owner/sessions/SessionsTableV2';

export default function SessionsPage() {
  return (
    <div style={{ maxWidth: 1400, margin: '0 auto', paddingBottom: 48 }}>
      <OwnerPageHeader
        breadcrumb="/owner/sessions"
        title="Phiên đỗ xe"
        subtitle="Danh sách xe đang đỗ và lịch sử các lượt ra vào"
      />

      <SessionsTableV2 />
    </div>
  );
}
