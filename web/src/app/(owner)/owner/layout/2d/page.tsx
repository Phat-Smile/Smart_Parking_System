'use client';

import React, { useEffect, useState } from 'react';
import { OwnerPageHeader as PageHeader } from '../../../../../components/layout/OwnerPageHeader';
import { Plan2DGrid } from '../../../../../components/viz/Plan2DGrid';
import { useOwnerStore } from '../../../../../store/useOwnerStore';
import { ST, FLOORS, Floor, ParkingSpot, SlotStatusKey } from '../../../../../types/owner';
import { K_VISIBLE } from '../../../../../lib/mapStatus';
import { SlotStatusChips } from '@/features/owner/layout/SlotStatusChips';
import { SlotStatusListPanel } from '@/features/owner/layout/SlotStatusListPanel';

// ──────────────────────────────────────────────────────────
// SlotModal – cửa sổ nhỏ hiển thị chi tiết ô được bấm
// ──────────────────────────────────────────────────────────
function SlotModal({ spot, onClose }: { spot: ParkingSpot; onClose: () => void }) {
  const updateSpotStatus = useOwnerStore((s) => s.updateSpotStatus);
  const statusInfo = ST[spot.s];

  return (
    <div className="sm-backdrop" onClick={onClose} role="dialog" aria-modal="true" aria-label={`Chi tiết ô ${spot.id}`}>
      <div className="sm-box" onClick={(e) => e.stopPropagation()}>
        <div className="sm-header">
          <h3 className="sm-title">Ô {spot.id}</h3>
          <button className="sm-close" onClick={onClose} aria-label="Đóng">✕</button>
        </div>

        <p className="mut sm-sub">Tầng {spot.f[1]} · Dãy {spot.r} · Vị trí {spot.n + 1}</p>

        <p style={{ margin: '10px 0' }}>
          <span className="tag" style={{ '--c': statusInfo[1] } as React.CSSProperties}>
            {statusInfo[0]}
          </span>
        </p>

        {(spot.s === 'occupied' || spot.s === 'reserved') && (
          <p style={{ fontSize: 13 }}>Biển số <b>{spot.p}</b></p>
        )}

        <p className="mut" style={{ fontSize: 11, marginTop: 10 }}>Đổi trạng thái:</p>
        <div className="chips" style={{ marginTop: 4 }}>
          {K_VISIBLE.map((k) => (
            <button
              key={k}
              className="chip"
              style={spot.s === k ? { borderColor: ST[k][1], fontWeight: 600 } : {}}
              onClick={() => updateSpotStatus(spot.id, k)}
            >
              {ST[k][0]}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

// ──────────────────────────────────────────────────────────
// Layout2DPage – trang chính
// ──────────────────────────────────────────────────────────
export default function Layout2DPage() {
  const spots = useOwnerStore((s) => s.spots);
  const activeFloor = useOwnerStore((s) => s.activeFloor);
  const setActiveFloor = useOwnerStore((s) => s.setActiveFloor);
  const selectSpot = useOwnerStore((s) => s.selectSpot);
  const lots = useOwnerStore((s) => s.lots);
  const simulateTick = useOwnerStore((s) => s.simulateTick);

  const [modalSpot, setModalSpot] = useState<ParkingSpot | null>(null);
  const [selectedStatus, setSelectedStatus] = useState<SlotStatusKey | null>(null);
  const [searchQuery, setSearchQuery] = useState('');

  // Auto-live simulation
  useEffect(() => {
    const timer = setInterval(() => simulateTick(), 5000);
    return () => clearInterval(timer);
  }, [simulateTick]);

  const mainLot = lots[0] || { name: 'Bãi đỗ xe Vincom Đồng Khởi' };

  const handleSelect = (spot: ParkingSpot) => {
    selectSpot(spot.id);
    setModalSpot(spot);
  };

  const handleClose = () => {
    setModalSpot(null);
    selectSpot(null);
  };

  return (
    <>
      <PageHeader
        breadcrumb="/owner/layout/2d"
        title="Sơ đồ 2D"
        subtitle={`${mainLot.name} · theo dõi trực tiếp`}
      />

      <div className="card" style={{ width: '100%' }}>
        <div className="wh" style={{ marginBottom: 16 }}>
          <h3 style={{ fontSize: 18, fontWeight: 800 }}>Mặt bằng 2D</h3>
          <div style={{ position: 'relative', width: '100%', maxWidth: 280 }}>
            <input
              type="text"
              placeholder="Nhập biển số để tìm ô"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              style={{
                width: '100%',
                padding: '7px 12px 7px 32px',
                fontSize: 13,
                borderRadius: 999,
                border: '1px solid #e2e8f0',
                outline: 'none',
                background: '#fff',
                color: '#1e293b',
              }}
            />
            <svg
              style={{ position: 'absolute', left: 10, top: 9, color: '#94a3b8' }}
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
            >
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
          </div>
        </div>

        {/* Chip thống kê tổng bấm được */}
        <SlotStatusChips
          selectedStatus={selectedStatus}
          onToggleStatus={(k) => setSelectedStatus((prev) => (prev === k ? null : k))}
          onClearStatus={() => setSelectedStatus(null)}
        />

        {/* Tab chọn tầng */}
        <div style={{ marginBottom: 16 }}>
          <div className="segs" id="ftabs">
            {(['all', ...FLOORS] as const).map((f) => (
              <button
                key={f}
                className={activeFloor === f ? 'on' : ''}
                onClick={() => setActiveFloor(f as Floor | 'all')}
              >
                {f === 'all' ? 'Tất cả tầng' : `Tầng ${f[1]}`}
              </button>
            ))}
          </div>
        </div>

        {/* Khung liệt kê ô theo trạng thái đang chọn (ở dưới tab tầng) */}
        {selectedStatus && (
          <SlotStatusListPanel
            status={selectedStatus}
            onClose={() => setSelectedStatus(null)}
            onSelectSpot={(id) => {
              const sp = spots.find((s) => s.id === id);
              if (sp) setModalSpot(sp);
            }}
          />
        )}

        {/* Lưới 2D thẳng hàng theo dãy như mô hình 3D, không xuống hàng */}
        <Plan2DGrid onSelect={handleSelect} searchQuery={searchQuery} />

        {/* Legend */}
        <div className="lgd" style={{ marginTop: 12 }}>
          {K_VISIBLE.map((k) => (
            <span key={k}>
              <i style={{ background: ST[k][1] }} />
              {ST[k][0]}
            </span>
          ))}
        </div>

        <p className="mut" style={{ marginTop: 10, fontSize: 12 }}>
          <span className="live" />
          Bấm một ô để xem chi tiết. Trạng thái cập nhật trực tiếp.
        </p>
      </div>

      {/* Modal chi tiết ô */}
      {modalSpot && <SlotModal spot={modalSpot} onClose={handleClose} />}

      {/* CSS scoped cho modal */}
      <style>{`
        .sm-backdrop {
          position: fixed;
          inset: 0;
          background: rgba(0,0,0,0.35);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 50;
          backdrop-filter: blur(2px);
          animation: smFadeIn 0.15s ease;
        }
        @keyframes smFadeIn { from { opacity: 0; } to { opacity: 1; } }

        .sm-box {
          background: var(--card);
          border: 1px solid var(--line);
          border-radius: 18px;
          padding: 20px;
          min-width: 260px;
          max-width: 340px;
          width: 90vw;
          box-shadow: 0 16px 48px rgba(0,0,0,0.18);
          animation: smSlideUp 0.18s ease;
        }
        @keyframes smSlideUp {
          from { transform: translateY(12px); opacity: 0; }
          to   { transform: translateY(0);    opacity: 1; }
        }

        .sm-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 4px;
        }
        .sm-title { margin: 0; font-size: 18px; font-weight: 700; }
        .sm-close {
          border: 1px solid var(--line);
          background: var(--soft);
          border-radius: 8px;
          width: 28px;
          height: 28px;
          cursor: pointer;
          font-size: 14px;
          display: grid;
          place-items: center;
          color: var(--mut);
          flex-shrink: 0;
        }
        .sm-close:hover { color: var(--ink); }
        .sm-sub { margin: 2px 0 0; font-size: 12px; }
      `}</style>
    </>
  );
}
