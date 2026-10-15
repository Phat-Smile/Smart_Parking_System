'use client';

import React from 'react';
import dynamic from 'next/dynamic';
import { getPublicMapboxToken } from '@/shared/config/env';
import { useAllLots, useOwnerMapStore } from './useOwnerMapStore';
import 'mapbox-gl/dist/mapbox-gl.css';

const OwnerMapScene = dynamic(() => import('./OwnerMapScene'), { ssr: false });

export function OwnerMapbox() {
  const token = getPublicMapboxToken();
  const lots = useAllLots();
  const { selectedLotId, setSelectedLotId, mapSearchQuery, setMapSearchQuery, setAddModalOpen } =
    useOwnerMapStore();

  const filteredLots = lots.filter(
    (l) =>
      !mapSearchQuery ||
      l.name.toLowerCase().includes(mapSearchQuery.toLowerCase()) ||
      l.addr.toLowerCase().includes(mapSearchQuery.toLowerCase())
  );

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
      }}
    >
      {/* Header */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: 12,
          marginBottom: 16,
        }}
      >
        <h2 style={{ fontSize: 18, fontWeight: 800, margin: 0, color: '#0f1b3d' }}>
          Vị trí các bãi
        </h2>

        <button
          type="button"
          onClick={() => setAddModalOpen(true)}
          style={{
            background: '#d9f99d',
            color: '#143806',
            border: 'none',
            borderRadius: 10,
            padding: '8px 16px',
            fontSize: 13,
            fontWeight: 700,
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: 6,
          }}
        >
          + Thêm bãi mới
        </button>
      </div>

      {/* Map Container */}
      <div
        style={{
          position: 'relative',
          width: '100%',
          height: 380,
          borderRadius: 14,
          overflow: 'hidden',
          background: '#eef3fb',
          border: '1px solid #e3e8f2',
        }}
      >
        {/* Search bar inside map */}
        <div
          style={{
            position: 'absolute',
            top: 14,
            left: 14,
            zIndex: 10,
            width: 'calc(100% - 28px)',
            maxWidth: 280,
          }}
        >
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              background: '#fff',
              borderRadius: 10,
              padding: '7px 12px',
              boxShadow: '0 4px 12px rgba(15,27,61,0.08)',
              border: '1px solid #e3e8f2',
            }}
          >
            <input
              type="text"
              placeholder="Tìm bãi trên bản đồ"
              value={mapSearchQuery}
              onChange={(e) => setMapSearchQuery(e.target.value)}
              aria-label="Tìm bãi trên bản đồ"
              style={{
                border: 'none',
                background: 'transparent',
                outline: 'none',
                width: '100%',
                fontSize: 12.5,
                color: '#0f1b3d',
              }}
            />
          </div>
        </div>

        {/* Map Render or Fallback */}
        {token ? (
          <OwnerMapScene
            token={token}
            selectedLotId={selectedLotId}
            onSelectLot={(id: number) => setSelectedLotId(id)}
            lots={filteredLots}
          />
        ) : (
          <div
            style={{
              width: '100%',
              height: '100%',
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#6a7491',
              padding: 24,
              textAlign: 'center',
            }}
          >
            <svg
              width="48"
              height="48"
              viewBox="0 0 24 24"
              fill="none"
              stroke="#9aa2b4"
              strokeWidth="1.5"
              style={{ marginBottom: 12 }}
            >
              <polygon points="1 6 1 22 8 18 16 22 23 18 23 2 16 6 8 2 1 6" />
              <line x1="8" y1="2" x2="8" y2="18" />
              <line x1="16" y1="6" x2="16" y2="22" />
            </svg>
            <div style={{ fontSize: 14, fontWeight: 700, color: '#0f1b3d' }}>
              Chưa cấu hình bản đồ
            </div>
            <div style={{ fontSize: 12.5, marginTop: 4 }}>
              Danh sách các bãi đỗ xe bên dưới vẫn hoạt động bình thường.
            </div>
          </div>
        )}
      </div>

      {/* Map Legend */}
      <div
        style={{
          display: 'flex',
          gap: 20,
          marginTop: 14,
          fontSize: 12.5,
          color: '#6a7491',
          flexWrap: 'wrap',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span style={{ width: 8, height: 8, borderRadius: '50%', backgroundColor: '#16a36a' }} />
          <span>Đang hoạt động</span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span style={{ width: 8, height: 8, borderRadius: '50%', backgroundColor: '#f59e0b' }} />
          <span>Chờ duyệt</span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span style={{ width: 8, height: 8, borderRadius: '50%', backgroundColor: '#e5484d' }} />
          <span>Bị từ chối</span>
        </div>
      </div>
    </div>
  );
}
