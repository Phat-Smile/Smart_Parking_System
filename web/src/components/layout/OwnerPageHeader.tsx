'use client';

import React from 'react';
import { HeaderSearch } from '@/features/owner/search/HeaderSearch';
import { NotificationBell } from '@/features/owner/notifications/NotificationBell';

export interface OwnerPageHeaderProps {
  breadcrumb?: string; // Giữ prop để tương thích ngược nhưng không hiển thị
  title: string;
  subtitle?: string;
  actions?: React.ReactNode;
}

export function OwnerPageHeader({
  title,
  subtitle,
  actions,
}: OwnerPageHeaderProps) {
  return (
    <header
      className="ov2-header"
      style={{
        position: 'relative',
        marginBottom: 24,
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: 16,
      }}
    >
      {/* Tiêu đề & mô tả (không hiển thị breadcrumb) */}
      <div className="ov2-header-left">
        <h1 style={{ fontSize: 26, fontWeight: 800, margin: 0, color: '#0f1b3d' }}>
          {title}
        </h1>
        {subtitle && (
          <div style={{ fontSize: 13.5, color: '#6a7491', marginTop: 4 }}>
            {subtitle}
          </div>
        )}
      </div>

      {/* Header Right */}
      <div
        className="ov2-header-right"
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 12,
          flexWrap: 'wrap',
        }}
      >
        {/* Nút hành động của từng trang (nếu có) */}
        {actions && <div className="ov2-header-actions">{actions}</div>}

        {/* Ô tìm kiếm theo từng trang (chỉ hiện ở 4 trang được cấu hình) */}
        <HeaderSearch />

        {/* Chuông thông báo mobile (chỉ hiện dưới 1024px) */}
        <div className="ov2-header-mobile-bell">
          <NotificationBell variant="header" />
        </div>
      </div>
    </header>
  );
}
