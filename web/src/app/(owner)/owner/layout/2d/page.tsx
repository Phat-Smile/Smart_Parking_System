'use client';

import React, { useEffect, useState } from 'react';
import { OwnerPageHeader as PageHeader } from '../../../../../components/layout/OwnerPageHeader';
import { Plan2DGrid } from '../../../../../components/viz/Plan2DGrid';
import { useOwnerStore, getSpotCounts } from '../../../../../store/useOwnerStore';
import { ST, FLOORS, Floor, ParkingSpot } from '../../../../../types/owner';
import { K_VISIBLE } from '../../../../../lib/mapStatus';

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

  // Auto-live simulation
  useEffect(() => {
    const timer = setInterval(() => simulateTick(), 5000);
    return () => clearInterval(timer);
  }, [simulateTick]);

  const mainLot = lots[0] || { name: 'Bãi đỗ xe Vincom Đồng Khởi' };
  const counts = getSpotCounts(spots);

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

      {/* Chip thống kê tổng */}
      <div id="vchips" style={{ marginBottom: 12, display: 'flex', flexWrap: 'wrap', gap: 6 }}>
        {K_VISIBLE.map((k) => (
          <span key={k} className="pill">
            <i
              style={{
                display: 'inline-block',
                width: 8,
                height: 8,
                borderRadius: '50%',
                background: ST[k][1],
                marginRight: 6,
              }}
            />
            {ST[k][0]} {counts[k]}
          </span>
        ))}
      </div>

      <div className="card" style={{ width: '100%' }}>
        <div className="wh">
          <h3>Sơ đồ 2D</h3>
          {/* Tab chọn tầng */}
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

        {/* Lưới 2D lấp đầy chiều ngang */}
        <Plan2DGrid onSelect={handleSelect} />

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
