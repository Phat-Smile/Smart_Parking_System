'use client';

import React from 'react';
import { OwnerPageHeader } from '@/components/layout/OwnerPageHeader';
import { OwnerMapbox } from '@/features/owner/lots/OwnerMapbox';
import { LotStatusPanel } from '@/features/owner/lots/LotStatusPanel';
import { AddLotModal } from '@/features/owner/lots/AddLotModal';
import { useAllLots, useOwnerMapStore } from '@/features/owner/lots/useOwnerMapStore';

export default function ParkingLotsPage() {
  const lots = useAllLots();
  const {
    selectedLotId,
    setSelectedLotId,
    filterStatus,
    setFilterStatus,
  } = useOwnerMapStore();

  // Counts for filter pills
  const totalCount = lots.length;
  const approvedCount = lots.filter((l) => l.ap === 'approved').length;
  const pendingCount = lots.filter((l) => l.ap === 'pending').length;
  const rejectedCount = lots.filter((l) => l.ap === 'rejected').length;

  const filteredLots = lots.filter((lot) => {
    if (filterStatus === 'all') return true;
    return lot.ap === filterStatus;
  });

  return (
    <div style={{ maxWidth: 1400, margin: '0 auto', paddingBottom: 48 }}>
      <OwnerPageHeader
        breadcrumb="/owner/lots"
        title="Bãi của tôi"
        subtitle="Quản lý các bãi đỗ xe của bạn"
      />

      {/* Map & Status Row */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'minmax(0, 1.9fr) minmax(320px, 1.1fr)',
          gap: 16,
          marginBottom: 24,
        }}
        className="ov2-row-traffic"
      >
        <OwnerMapbox />
        <LotStatusPanel />
      </div>

      {/* Filter Status Pills */}
      <div
        style={{
          display: 'flex',
          gap: 8,
          marginBottom: 20,
          flexWrap: 'wrap',
        }}
      >
        {[
          { key: 'all', label: `Tất cả (${totalCount})` },
          { key: 'approved', label: `Đang hoạt động (${approvedCount})` },
          { key: 'pending', label: `Chờ duyệt (${pendingCount})` },
          { key: 'rejected', label: `Bị từ chối (${rejectedCount})` },
        ].map((item) => {
          const isActive = filterStatus === item.key;
          return (
            <button
              key={item.key}
              type="button"
              onClick={() => setFilterStatus(item.key as typeof filterStatus)}
              style={{
                border: 'none',
                background: isActive ? '#0f1b3d' : '#fff',
                color: isActive ? '#fff' : '#0f1b3d',
                borderRadius: 999,
                padding: '8px 18px',
                fontSize: 13,
                fontWeight: 700,
                cursor: 'pointer',
                boxShadow: '0 1px 2px rgba(15,27,61,0.06)',
                borderWidth: 1,
                borderStyle: 'solid',
                borderColor: isActive ? '#0f1b3d' : '#e3e8f2',
                transition: 'all 0.15s ease',
              }}
            >
              {item.label}
            </button>
          );
        })}
      </div>

      {/* Lot Cards Grid */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
          gap: 16,
        }}
      >
        {filteredLots.map((lot) => {
          const isSelected = selectedLotId === lot.id;
          const statusBg =
            lot.ap === 'approved'
              ? '#def7ec'
              : lot.ap === 'pending'
              ? '#fef3c7'
              : '#fde8e8';
          const statusColor =
            lot.ap === 'approved'
              ? '#0e9f6e'
              : lot.ap === 'pending'
              ? '#b45309'
              : '#e5484d';
          const statusText =
            lot.ap === 'approved'
              ? 'Đang hoạt động'
              : lot.ap === 'pending'
              ? 'Chờ duyệt'
              : 'Bị từ chối';

          return (
            <div
              key={lot.id}
              onClick={() => setSelectedLotId(lot.id)}
              role="button"
              tabIndex={0}
              onKeyDown={(e) => e.key === 'Enter' && setSelectedLotId(lot.id)}
              aria-label={`Bãi xe ${lot.name}, ${lot.addr}, ${lot.cc} ô đỗ`}
              style={{
                background: '#fff',
                borderRadius: 18,
                border: isSelected ? '2px solid #2563eb' : '1px solid #e3e8f2',
                padding: 22,
                cursor: 'pointer',
                boxShadow: isSelected
                  ? '0 8px 24px rgba(37,99,235,0.12)'
                  : '0 2px 6px rgba(15,27,61,0.04)',
                transition: 'all 0.15s ease',
              }}
            >
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'flex-start',
                  marginBottom: 10,
                }}
              >
                <div style={{ fontSize: 16, fontWeight: 800, color: '#0f1b3d' }}>
                  {lot.name}
                </div>
                <span
                  style={{
                    background: statusBg,
                    color: statusColor,
                    borderRadius: 999,
                    padding: '3px 10px',
                    fontSize: 11.5,
                    fontWeight: 700,
                    whiteSpace: 'nowrap',
                  }}
                >
                  {statusText}
                </span>
              </div>

              <div style={{ fontSize: 13, color: '#6a7491', marginBottom: 14 }}>
                {lot.addr}
              </div>

              <div style={{ fontSize: 14, fontWeight: 700, color: '#0f1b3d' }}>
                {lot.cc} ô đỗ
              </div>
            </div>
          );
        })}
      </div>

      {/* Add Lot Modal */}
      <AddLotModal />
    </div>
  );
}
