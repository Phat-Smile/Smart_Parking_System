'use client';

import React from 'react';
import { useRouter } from 'next/navigation';
import { useOwnerStore, getSpotCounts } from '@/store/useOwnerStore';

export function DashboardKpisV2() {
  const router = useRouter();
  const spots = useOwnerStore((s) => s.spots);
  const reservations = useOwnerStore((s) => s.reservations);
  const counts = getSpotCounts(spots);

  // Values aligned with approved mock and dynamic store state
  const occupiedCount = counts.occupied || 190;
  const availableCount = counts.available || 127;
  const totalSpots = spots.length || 500;
  const reservedCount = reservations.length || 100;

  return (
    <div
      className="ov2-kpis"
      style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
        gap: 16,
        marginBottom: 20,
      }}
    >
      {/* 1. Tổng doanh thu hôm nay */}
      <div
        className="ov2-kpi-card"
        onClick={() => router.push('/owner/reports')}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => e.key === 'Enter' && router.push('/owner/reports')}
        aria-label="Tổng doanh thu hôm nay 37,6 tr, tăng 12,4%"
        style={{
          background: '#fff',
          borderRadius: 18,
          border: '1px solid #e3e8f2',
          padding: 22,
          cursor: 'pointer',
          position: 'relative',
          transition: 'transform 0.15s ease, box-shadow 0.15s ease',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <div
            style={{
              width: 44,
              height: 44,
              borderRadius: 12,
              background: '#eefdf5',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#16a36a',
            }}
          >
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <line x1="18" y1="20" x2="18" y2="10" />
              <line x1="12" y1="20" x2="12" y2="4" />
              <line x1="6" y1="20" x2="6" y2="14" />
            </svg>
          </div>
        </div>

        <div style={{ marginTop: 18 }}>
          <div style={{ fontSize: 32, fontWeight: 800, color: '#0f1b3d', lineHeight: 1.1 }}>
            37,6 tr
          </div>
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              marginTop: 6,
            }}
          >
            <span style={{ fontSize: 13, color: '#6a7491' }}>Tổng doanh thu hôm nay</span>
            <span style={{ fontSize: 13, fontWeight: 700, color: '#16a36a' }}>
              12,4% ↑
            </span>
          </div>
        </div>
      </div>

      {/* 2. Tổng số xe trong bãi */}
      <div
        className="ov2-kpi-card"
        onClick={() => router.push('/owner/sessions')}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => e.key === 'Enter' && router.push('/owner/sessions')}
        aria-label={`Tổng số xe trong bãi ${occupiedCount}, tăng 8,3%`}
        style={{
          background: '#fff',
          borderRadius: 18,
          border: '1px solid #e3e8f2',
          padding: 22,
          cursor: 'pointer',
          position: 'relative',
          transition: 'transform 0.15s ease, box-shadow 0.15s ease',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <div
            style={{
              width: 44,
              height: 44,
              borderRadius: 12,
              background: '#eefdf5',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#16a36a',
            }}
          >
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.4 2.9A3.7 3.7 0 0 0 2 12v4c0 .6.4 1 1 1h2" />
              <circle cx="7" cy="17" r="2" />
              <path d="M9 17h6" />
              <circle cx="17" cy="17" r="2" />
            </svg>
          </div>
        </div>

        <div style={{ marginTop: 18 }}>
          <div style={{ fontSize: 32, fontWeight: 800, color: '#0f1b3d', lineHeight: 1.1 }}>
            {occupiedCount}
          </div>
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              marginTop: 6,
            }}
          >
            <span style={{ fontSize: 13, color: '#6a7491' }}>Tổng số xe trong bãi</span>
            <span style={{ fontSize: 13, fontWeight: 700, color: '#16a36a' }}>
              8,3% ↑
            </span>
          </div>
        </div>
      </div>

      {/* 3. Chỗ còn trống */}
      <div
        className="ov2-kpi-card"
        onClick={() => router.push('/owner/layout/2d')}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => e.key === 'Enter' && router.push('/owner/layout/2d')}
        aria-label={`Chỗ còn trống ${availableCount}/${totalSpots}`}
        style={{
          background: '#fff',
          borderRadius: 18,
          border: '1px solid #e3e8f2',
          padding: 22,
          cursor: 'pointer',
          position: 'relative',
          transition: 'transform 0.15s ease, box-shadow 0.15s ease',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <div
            style={{
              width: 44,
              height: 44,
              borderRadius: 12,
              background: '#eefdf5',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#16a36a',
            }}
          >
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
              <polyline points="9 22 9 12 15 12 15 22" />
            </svg>
          </div>
        </div>

        <div style={{ marginTop: 18 }}>
          <div style={{ fontSize: 32, fontWeight: 800, color: '#0f1b3d', lineHeight: 1.1 }}>
            {availableCount}/{totalSpots}
          </div>
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              marginTop: 6,
            }}
          >
            <span style={{ fontSize: 13, color: '#6a7491' }}>Chỗ còn trống</span>
            <span style={{ fontSize: 13, fontWeight: 700, color: '#2563eb' }}>
              {Math.round((availableCount / totalSpots) * 100)}%
            </span>
          </div>
        </div>
      </div>

      {/* 4. Đặt chỗ hiện có */}
      <div
        className="ov2-kpi-card"
        onClick={() => router.push('/owner/reservations')}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => e.key === 'Enter' && router.push('/owner/reservations')}
        aria-label={`Đặt chỗ hiện có ${reservedCount}, tăng 5,0%`}
        style={{
          background: '#fff',
          borderRadius: 18,
          border: '1px solid #e3e8f2',
          padding: 22,
          cursor: 'pointer',
          position: 'relative',
          transition: 'transform 0.15s ease, box-shadow 0.15s ease',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <div
            style={{
              width: 44,
              height: 44,
              borderRadius: 12,
              background: '#eefdf5',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#16a36a',
            }}
          >
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
              <line x1="16" y1="2" x2="16" y2="6" />
              <line x1="8" y1="2" x2="8" y2="6" />
              <line x1="3" y1="10" x2="21" y2="10" />
            </svg>
          </div>
        </div>

        <div style={{ marginTop: 18 }}>
          <div style={{ fontSize: 32, fontWeight: 800, color: '#0f1b3d', lineHeight: 1.1 }}>
            {reservedCount}
          </div>
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              marginTop: 6,
            }}
          >
            <span style={{ fontSize: 13, color: '#6a7491' }}>Đặt chỗ hiện có</span>
            <span style={{ fontSize: 13, fontWeight: 700, color: '#16a36a' }}>
              5,0% ↑
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}
