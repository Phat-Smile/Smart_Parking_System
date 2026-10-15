'use client';

import React, { useState } from 'react';
import { useAllLots, useOwnerMapStore } from './useOwnerMapStore';
import { useLotInfo, formatVnd, formatDateVi } from './lotInfo';
import { LotPin } from './LotPin';

interface SchematicMapProps {
  filteredLots: ReturnType<typeof useAllLots>;
}

// Relative pin coordinates matching the mockup layout in HCMC
const SCHEMATIC_COORDS: Record<number, { x: number; y: number }> = {
  1: { x: 280, y: 125 }, // Bãi xe Nguyễn Huệ (gần Đồng Khởi / UBND TP.HCM)
  2: { x: 245, y: 148 }, // Bãi xe Bến Thành (gần Chợ Bến Thành / Hàm Nghi)
  3: { x: 365, y: 125 }, // Bãi xe Thủ Thiêm (gần Mai Chí Thọ / Thủ Thiêm)
  4: { x: 340, y: 185 }, // Bãi xe Phú Mỹ Hưng (gần Quận 7)
};

export function SchematicMap({ filteredLots }: SchematicMapProps) {
  const { selectedLotId, setSelectedLotId } = useOwnerMapStore();
  const selectedLot = filteredLots.find((l) => l.id === selectedLotId);
  const selectedLotInfo = useLotInfo(selectedLotId);

  const [zoomLevel, setZoomLevel] = useState(1);

  const handleZoomIn = () => setZoomLevel((z) => Math.min(1.6, z + 0.15));
  const handleZoomOut = () => setZoomLevel((z) => Math.max(0.85, z - 0.15));
  const handleResetZoom = () => setZoomLevel(1);

  // Status badge colors
  const statusBg =
    selectedLot?.ap === 'approved'
      ? '#def7ec'
      : selectedLot?.ap === 'pending'
      ? '#fef3c7'
      : '#fde8e8';
  const statusColor =
    selectedLot?.ap === 'approved'
      ? '#0e9f6e'
      : selectedLot?.ap === 'pending'
      ? '#b45309'
      : '#e5484d';
  const statusText =
    selectedLot?.ap === 'approved'
      ? 'Đang hoạt động'
      : selectedLot?.ap === 'pending'
      ? 'Chờ duyệt'
      : 'Bị từ chối';

  let dateText = '';
  if (selectedLot?.ap === 'approved') {
    dateText = `Hoạt động từ ${formatDateVi(selectedLotInfo?.openedAt)}`;
  } else if (selectedLot?.ap === 'pending') {
    dateText = `Gửi hồ sơ ngày ${formatDateVi(selectedLotInfo?.submittedAt)}`;
  } else if (selectedLot?.ap === 'rejected') {
    dateText = `Bị từ chối ngày ${formatDateVi(selectedLotInfo?.rejectedAt)}`;
  }

  const selectedCoord = selectedLot ? SCHEMATIC_COORDS[selectedLot.id] || { x: 280, y: 140 } : null;

  return (
    <div
      style={{
        position: 'relative',
        width: '100%',
        height: '100%',
        overflow: 'hidden',
        background: '#f2f4f7',
        userSelect: 'none',
      }}
      onClick={() => {
        // Click on map background closes popup
        if (selectedLotId) setSelectedLotId(null);
      }}
    >
      {/* Interactive SVG Street Map of HCMC */}
      <svg
        viewBox="0 0 600 240"
        preserveAspectRatio="xMidYMid meet"
        style={{
          width: '100%',
          height: '100%',
          display: 'block',
          transform: `scale(${zoomLevel})`,
          transformOrigin: selectedCoord ? `${selectedCoord.x}px ${selectedCoord.y}px` : 'center center',
          transition: 'transform 0.25s ease',
        }}
      >
        <defs>
          {/* City block pattern */}
          <pattern id="streetGrid" width="40" height="40" patternUnits="userSpaceOnUse">
            <rect width="36" height="36" fill="#ffffff" rx="3" />
            <rect x="36" y="0" width="4" height="40" fill="#e9ecf0" />
            <rect x="0" y="36" width="40" height="4" fill="#e9ecf0" />
          </pattern>
        </defs>

        {/* Background grid */}
        <rect width="600" height="240" fill="url(#streetGrid)" opacity="0.85" />

        {/* Sông Sài Gòn (Curved Blue River) */}
        <path
          d="M 330 0 C 350 50, 310 90, 310 130 C 310 160, 340 180, 380 200 C 410 215, 430 240, 430 240 L 470 240 C 470 240, 440 205, 410 190 C 370 170, 345 150, 345 125 C 345 90, 385 45, 365 0 Z"
          fill="#cbe3fb"
          stroke="#b8d7f7"
          strokeWidth="1.5"
        />

        {/* Yellow Bridge across River */}
        <path
          d="M 295 120 C 320 123, 355 126, 380 128"
          stroke="#fbbf24"
          strokeWidth="6"
          strokeLinecap="round"
        />

        {/* Main Road Lines */}
        <line x1="80" y1="80" x2="310" y2="80" stroke="#fff" strokeWidth="10" />
        <line x1="80" y1="120" x2="300" y2="120" stroke="#fff" strokeWidth="10" />
        <line x1="80" y1="160" x2="330" y2="160" stroke="#fff" strokeWidth="10" />
        <line x1="170" y1="20" x2="170" y2="220" stroke="#fff" strokeWidth="10" />
        <line x1="230" y1="20" x2="230" y2="220" stroke="#fff" strokeWidth="10" />
        <line x1="380" y1="128" x2="550" y2="128" stroke="#fff" strokeWidth="12" />

        {/* River Label */}
        <text
          x="320"
          y="80"
          fontSize="9"
          fill="#5a8ec6"
          fontFamily="system-ui"
          fontWeight="600"
          transform="rotate(65 320,80)"
          letterSpacing="1"
        >
          Sông Sài Gòn
        </text>

        {/* Street Name Labels */}
        <text x="175" y="78" fontSize="8" fill="#8795ab" fontFamily="system-ui">
          Đồng Khởi
        </text>
        <text x="175" y="118" fontSize="8" fill="#8795ab" fontFamily="system-ui">
          Hàm Nghi
        </text>
        <text x="180" y="158" fontSize="8" fill="#8795ab" fontFamily="system-ui">
          Nam Kỳ Khởi Nghĩa
        </text>
        <text x="400" y="123" fontSize="8.5" fill="#8795ab" fontFamily="system-ui">
          Mai Chí Thọ
        </text>

        {/* District Labels */}
        <text x="440" y="55" fontSize="11" fontWeight="800" fill="#9aa8bc" fontFamily="system-ui" letterSpacing="1.5">
          THỦ THIÊM
        </text>
        <text x="365" y="228" fontSize="10" fontWeight="800" fill="#9aa8bc" fontFamily="system-ui" letterSpacing="1.2">
          QUẬN 7
        </text>

        {/* Landmarks with Small Colored Indicators */}
        <g transform="translate(265, 75)">
          <circle cx="0" cy="0" r="3" fill="#ec4899" />
          <text x="6" y="2.5" fontSize="7.5" fill="#5a6881" fontFamily="system-ui">
            Nhà hát Thành phố
          </text>
        </g>

        <g transform="translate(295, 88)">
          <circle cx="0" cy="0" r="3" fill="#2563eb" />
          <text x="6" y="2.5" fontSize="7.5" fill="#5a6881" fontFamily="system-ui">
            Bến Bạch Đằng
          </text>
        </g>

        <g transform="translate(255, 98)">
          <circle cx="0" cy="0" r="3" fill="#10b981" />
          <text x="6" y="2.5" fontSize="7.5" fill="#5a6881" fontFamily="system-ui">
            UBND TP.HCM
          </text>
        </g>

        <g transform="translate(285, 138)">
          <circle cx="0" cy="0" r="3" fill="#3b82f6" />
          <text x="6" y="2.5" fontSize="7.5" fill="#5a6881" fontFamily="system-ui">
            Bitexco
          </text>
        </g>

        <g transform="translate(215, 155)">
          <circle cx="0" cy="0" r="3" fill="#f59e0b" />
          <text x="6" y="2.5" fontSize="7.5" fill="#5a6881" fontFamily="system-ui">
            Chợ Bến Thành
          </text>
        </g>

        {/* User location pulse dot */}
        <circle cx="225" cy="95" r="7" fill="#3b82f6" opacity="0.25" />
        <circle cx="225" cy="95" r="3.5" fill="#2563eb" />
      </svg>

      {/* Pins Layer Overlay */}
      <div
        style={{
          position: 'absolute',
          inset: 0,
          pointerEvents: 'none',
          transform: `scale(${zoomLevel})`,
          transformOrigin: selectedCoord ? `${(selectedCoord.x / 600) * 100}% ${(selectedCoord.y / 240) * 100}%` : 'center center',
          transition: 'transform 0.25s ease',
        }}
      >
        {filteredLots.map((lot) => {
          const coord = SCHEMATIC_COORDS[lot.id] || { x: 280, y: 140 };
          const leftPct = (coord.x / 600) * 100;
          const topPct = (coord.y / 240) * 100;
          const isSelected = selectedLotId === lot.id;

          return (
            <div
              key={lot.id}
              onClick={(e) => {
                e.stopPropagation();
                setSelectedLotId(isSelected ? null : lot.id);
              }}
              style={{
                position: 'absolute',
                left: `${leftPct}%`,
                top: `${topPct}%`,
                transform: 'translate(-50%, -50%)',
                pointerEvents: 'auto',
                cursor: 'pointer',
                zIndex: isSelected ? 40 : 10,
              }}
            >
              <LotPin
                lot={lot}
                isSelected={isSelected}
                onClick={() => setSelectedLotId(isSelected ? null : lot.id)}
              />
            </div>
          );
        })}
      </div>

      {/* Floating Small Card / Popup on selected pin */}
      {selectedLot && selectedCoord && (
        <div
          onClick={(e) => e.stopPropagation()}
          role="dialog"
          aria-label={`Thông tin nhanh bãi ${selectedLot.name}`}
          style={{
            position: 'absolute',
            left: `${(selectedCoord.x / 600) * 100}%`,
            top: `${(selectedCoord.y / 240) * 100}%`,
            transform: 'translate(-50%, -125%)',
            background: '#fff',
            borderRadius: 14,
            padding: '12px 16px',
            boxShadow: '0 12px 32px rgba(15,27,61,0.18)',
            border: '1px solid #e3e8f2',
            zIndex: 60,
            minWidth: 220,
            maxWidth: 280,
            animation: 'fadeIn 0.15s ease',
          }}
        >
          {/* Close button */}
          <button
            type="button"
            onClick={() => setSelectedLotId(null)}
            aria-label="Đóng thẻ thông tin"
            style={{
              position: 'absolute',
              top: 8,
              right: 10,
              border: 'none',
              background: 'none',
              fontSize: 16,
              color: '#9aa2b4',
              cursor: 'pointer',
              padding: 0,
            }}
          >
            ✕
          </button>

          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4, paddingRight: 16 }}>
            <strong style={{ fontSize: 13.5, color: '#0f1b3d' }}>
              {selectedLot.name}
            </strong>
            <span
              style={{
                fontSize: 10.5,
                padding: '2px 8px',
                borderRadius: 99,
                background: statusBg,
                color: statusColor,
                fontWeight: 700,
                whiteSpace: 'nowrap',
              }}
            >
              {statusText}
            </span>
          </div>

          {dateText && (
            <div style={{ fontSize: 11.5, color: '#6a7491', marginBottom: 4 }}>
              {dateText}
            </div>
          )}

          <div style={{ fontSize: 12.5, fontWeight: 700, color: '#2563eb', marginBottom: 2 }}>
            Ô tô từ {formatVnd(selectedLotInfo?.prices?.carFirstHour)}/giờ đầu
          </div>

          <div style={{ fontSize: 11.5, color: '#6a7491' }}>
            Giờ mở cửa: {selectedLotInfo?.openingHours || `${selectedLot.op} – ${selectedLot.cl2}`}
          </div>

          {/* Pointer triangle */}
          <div
            style={{
              position: 'absolute',
              bottom: -6,
              left: '50%',
              transform: 'translateX(-50%) rotate(45deg)',
              width: 12,
              height: 12,
              backgroundColor: '#fff',
              borderRight: '1px solid #e3e8f2',
              borderBottom: '1px solid #e3e8f2',
            }}
          />
        </div>
      )}

      {/* Bottom-left Badge: "Bản đồ minh họa" */}
      <div
        style={{
          position: 'absolute',
          bottom: 12,
          left: 12,
          background: 'rgba(255, 255, 255, 0.92)',
          backdropFilter: 'blur(4px)',
          borderRadius: 6,
          padding: '4px 10px',
          fontSize: 11,
          fontWeight: 600,
          color: '#6a7491',
          boxShadow: '0 2px 6px rgba(15,27,61,0.06)',
          border: '1px solid #e3e8f2',
          display: 'flex',
          alignItems: 'center',
          gap: 6,
          zIndex: 20,
        }}
      >
        <span style={{ width: 6, height: 6, borderRadius: '50%', backgroundColor: '#16a36a' }} />
        <span>Bản đồ minh họa</span>
      </div>

      {/* Zoom and Target Controls (Right side) */}
      <div
        style={{
          position: 'absolute',
          bottom: 12,
          right: 12,
          display: 'flex',
          flexDirection: 'column',
          background: '#fff',
          borderRadius: 8,
          boxShadow: '0 4px 12px rgba(15,27,61,0.1)',
          border: '1px solid #e3e8f2',
          overflow: 'hidden',
          zIndex: 20,
        }}
      >
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            handleZoomIn();
          }}
          aria-label="Phóng to bản đồ"
          style={{
            border: 'none',
            background: '#fff',
            width: 32,
            height: 32,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 16,
            fontWeight: 700,
            color: '#0f1b3d',
            cursor: 'pointer',
            borderBottom: '1px solid #f0f2f5',
          }}
        >
          +
        </button>
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            handleZoomOut();
          }}
          aria-label="Thu nhỏ bản đồ"
          style={{
            border: 'none',
            background: '#fff',
            width: 32,
            height: 32,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 16,
            fontWeight: 700,
            color: '#0f1b3d',
            cursor: 'pointer',
            borderBottom: '1px solid #f0f2f5',
          }}
        >
          −
        </button>
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            handleResetZoom();
          }}
          aria-label="Định vị lại trung tâm"
          style={{
            border: 'none',
            background: '#fff',
            width: 32,
            height: 32,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: '#0f1b3d',
            cursor: 'pointer',
          }}
        >
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="12" cy="12" r="10" />
            <circle cx="12" cy="12" r="3" />
          </svg>
        </button>
      </div>
    </div>
  );
}
