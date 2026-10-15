'use client';

/**
 * Plan2DGrid – lưới sơ đồ 2D lấp đầy chiều ngang.
 * Component MỚI, KHÔNG sửa Plan2D gốc (vẫn dùng ở nơi khác).
 *
 * Thiết kế:
 *   - 20 cột trên màn hình lớn, 10 cột màn hình vừa (≤900px), 5 cột màn hình nhỏ (≤600px).
 *   - Ô lấp đầy ngang khung (auto-fit), không cuộn ngang.
 *   - Bấm ô → gọi onSelect(spot).
 */

import React from 'react';
import { useOwnerStore } from '../../store/useOwnerStore';
import { ST, Floor, FLOORS, ParkingSpot } from '../../types/owner';

interface Plan2DGridProps {
  overrideFloor?: Floor | 'all';
  onSelect?: (spot: ParkingSpot) => void;
}

const EMOJI: Record<string, string> = {
  occupied: '🚗',
  reserved: '🔖',
  maintenance: '🔧',
};

export function Plan2DGrid({ overrideFloor, onSelect }: Plan2DGridProps) {
  const spots = useOwnerStore((s) => s.spots);
  const storeFloor = useOwnerStore((s) => s.activeFloor);
  const activeFloor = overrideFloor ?? storeFloor;
  const selectedId = useOwnerStore((s) => s.selectedId);
  const selectSpot = useOwnerStore((s) => s.selectSpot);

  const displayFloors = activeFloor === 'all' ? FLOORS : [activeFloor as Floor];

  const handleClick = (s: ParkingSpot) => {
    selectSpot(s.id);
    if (onSelect) onSelect(s);
  };

  const renderRow = (f: Floor, r: 'A' | 'B' | 'C' | 'D') => {
    const rowSpots = spots.filter((s) => s.f === f && s.r === r);
    return (
      <div className="pg-row">
        {rowSpots.map((s) => {
          const isSelected = selectedId === s.id;
          const statusInfo = ST[s.s];
          return (
            <button
              key={s.id}
              className={`pg-cell ${isSelected ? 'pg-sel' : ''}`}
              style={{ '--c': statusInfo[1] } as React.CSSProperties}
              onClick={() => handleClick(s)}
              title={`${s.id} · ${statusInfo[0]}`}
              aria-pressed={isSelected}
              aria-label={`${s.id}: ${statusInfo[0]}`}
            >
              <small>{s.r}{s.n + 1}</small>
              {EMOJI[s.s] ?? ''}
            </button>
          );
        })}
      </div>
    );
  };

  return (
    <div id="plan-grid">
      {displayFloors.map((f) => (
        <div key={f} className="pg-floor">
          <h4 className="pg-floor-title">Tầng {f[1]}</h4>
          {renderRow(f, 'A')}
          <div className="pg-lane">Lối xe chạy</div>
          {renderRow(f, 'B')}
          {renderRow(f, 'C')}
          <div className="pg-lane">Lối xe chạy</div>
          {renderRow(f, 'D')}
        </div>
      ))}

      <style>{`
        #plan-grid { width: 100%; }

        .pg-floor { margin-bottom: 24px; }
        .pg-floor-title { margin: 0 0 8px; font-size: 13px; font-weight: 600; }

        .pg-row {
          display: grid;
          /* 20 cột lớn, 10 vừa, 5 nhỏ – lấp đầy chiều ngang, không cuộn */
          grid-template-columns: repeat(20, minmax(0, 1fr));
          gap: 3px;
          margin-bottom: 3px;
        }

        .pg-cell {
          aspect-ratio: 1 / 1.3;
          border: 2px solid var(--c);
          background: color-mix(in srgb, var(--c) 14%, #fff);
          border-radius: 6px;
          cursor: pointer;
          display: grid;
          place-items: center;
          font-size: 10px;
          line-height: 1;
          color: var(--ink);
          transition: transform 0.1s ease, outline 0.1s ease;
          padding: 1px;
        }
        .pg-cell small {
          font-size: 7px;
          font-weight: 600;
          display: block;
        }
        .pg-cell:hover { transform: scale(1.08); }
        .pg-sel {
          outline: 2px solid var(--pw-lime, #AAEC38);
          outline-offset: 1px;
          transform: scale(1.05);
        }

        .pg-lane {
          text-align: center;
          color: var(--mut);
          font-size: 10px;
          padding: 4px 0;
          margin: 4px 0;
          border-bottom: 2px dashed var(--line);
        }

        @media (max-width: 900px) {
          .pg-row { grid-template-columns: repeat(10, minmax(0, 1fr)); }
        }
        @media (max-width: 600px) {
          .pg-row { grid-template-columns: repeat(5, minmax(0, 1fr)); }
        }
      `}</style>
    </div>
  );
}

export default Plan2DGrid;
