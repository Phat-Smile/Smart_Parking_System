'use client';

import React from 'react';
import Link from 'next/link';
import { useAllLots, useOwnerMapStore } from './useOwnerMapStore';
import { AP } from '@/types/owner';

export function LotStatusPanel() {
  const lots = useAllLots();
  const { selectedLotId, updateLotStatus } = useOwnerMapStore();

  const selectedLot = lots.find((l) => l.id === selectedLotId);

  return (
    <div
      className="ov2-card"
      style={{
        background: '#fff',
        borderRadius: 18,
        border: '1px solid #e3e8f2',
        padding: 24,
        display: 'flex',
        flexDirection: 'column',
        minHeight: 380,
      }}
    >
      <h2 style={{ fontSize: 18, fontWeight: 800, margin: '0 0 16px', color: '#0f1b3d' }}>
        Trạng thái bãi
      </h2>

      {!selectedLot ? (
        <div
          style={{
            flex: 1,
            display: 'flex',
            flexDirection: 'column',
            justifyContent: 'center',
            color: '#6a7491',
            fontSize: 13.5,
            lineHeight: 1.6,
          }}
        >
          Bấm vào một ghim trên bản đồ hoặc một thẻ bãi bên dưới để xem bãi đó đang hoạt động, chờ duyệt hay bị từ chối.
        </div>
      ) : (
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 14 }}>
          {/* Lot Title & Status */}
          <div>
            <div style={{ fontSize: 17, fontWeight: 800, color: '#0f1b3d' }}>
              {selectedLot.name}
            </div>
            <div style={{ fontSize: 13, color: '#6a7491', marginTop: 2 }}>
              {selectedLot.addr}
            </div>
          </div>

          <div>
            <span
              style={{
                display: 'inline-block',
                background:
                  selectedLot.ap === 'approved'
                    ? '#def7ec'
                    : selectedLot.ap === 'pending'
                    ? '#fef3c7'
                    : '#fde8e8',
                color:
                  selectedLot.ap === 'approved'
                    ? '#0e9f6e'
                    : selectedLot.ap === 'pending'
                    ? '#b45309'
                    : '#e5484d',
                borderRadius: 999,
                padding: '4px 12px',
                fontSize: 12,
                fontWeight: 700,
              }}
            >
              {AP[selectedLot.ap] ? AP[selectedLot.ap][0] : selectedLot.ap}
            </span>
          </div>

          {/* Rejection / Note reason if pending/rejected */}
          {selectedLot.why && (
            <div
              style={{
                background: '#fff5f5',
                border: '1px solid #fed7d7',
                borderRadius: 10,
                padding: '10px 12px',
                fontSize: 12.5,
                color: '#c53030',
              }}
            >
              <strong>Lưu ý:</strong> {selectedLot.why}
            </div>
          )}

          {/* Capacity stats */}
          <div
            style={{
              background: '#f8faff',
              borderRadius: 12,
              padding: 14,
              display: 'grid',
              gridTemplateColumns: '1fr 1fr',
              gap: 12,
              fontSize: 13,
            }}
          >
            <div>
              <span style={{ color: '#6a7491', fontSize: 12 }}>Sức chứa ô tô</span>
              <div style={{ fontWeight: 800, fontSize: 18, color: '#0f1b3d' }}>
                {selectedLot.cc} ô
              </div>
            </div>
            <div>
              <span style={{ color: '#6a7491', fontSize: 12 }}>Sức chứa xe máy</span>
              <div style={{ fontWeight: 800, fontSize: 18, color: '#0f1b3d' }}>
                {selectedLot.cm} ô
              </div>
            </div>
            <div>
              <span style={{ color: '#6a7491', fontSize: 12 }}>Giờ mở cửa</span>
              <div style={{ fontWeight: 600, color: '#0f1b3d' }}>
                {selectedLot.op} – {selectedLot.cl2}
              </div>
            </div>
            <div>
              <span style={{ color: '#6a7491', fontSize: 12 }}>Số tầng</span>
              <div style={{ fontWeight: 600, color: '#0f1b3d' }}>
                {selectedLot.fls} tầng
              </div>
            </div>
          </div>

          {/* Quick status change / actions */}
          <div style={{ marginTop: 'auto', display: 'flex', flexDirection: 'column', gap: 8, paddingTop: 12 }}>
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
              <button
                type="button"
                onClick={() => updateLotStatus(selectedLot.id, 'approved')}
                style={{
                  flex: 1,
                  background: '#f3f6fb',
                  border: '1px solid #e3e8f2',
                  borderRadius: 8,
                  padding: '6px 10px',
                  fontSize: 12,
                  fontWeight: 600,
                  cursor: 'pointer',
                  color: selectedLot.ap === 'approved' ? '#0e9f6e' : '#6a7491',
                }}
              >
                Kích hoạt
              </button>
              <button
                type="button"
                onClick={() => updateLotStatus(selectedLot.id, 'pending')}
                style={{
                  flex: 1,
                  background: '#f3f6fb',
                  border: '1px solid #e3e8f2',
                  borderRadius: 8,
                  padding: '6px 10px',
                  fontSize: 12,
                  fontWeight: 600,
                  cursor: 'pointer',
                  color: selectedLot.ap === 'pending' ? '#b45309' : '#6a7491',
                }}
              >
                Chờ duyệt
              </button>
            </div>

            <Link
              href={`/owner/parking-lots/${selectedLot.id}`}
              style={{
                textAlign: 'center',
                background: '#0f1b3d',
                color: '#fff',
                borderRadius: 10,
                padding: '8px 14px',
                fontSize: 13,
                fontWeight: 600,
                textDecoration: 'none',
              }}
            >
              Xem chi tiết cấu hình bãi
            </Link>
          </div>
        </div>
      )}
    </div>
  );
}
