'use client';

import React, { useState, useMemo } from 'react';
import { getWeeklyParkingData } from '@/features/owner/dashboard/trafficAdapter';

export function WeeklyParkingChart() {
  const [selectedWeek, setSelectedWeek] = useState('current');
  const [hoveredIdx, setHoveredIdx] = useState<number | null>(null);

  const items = useMemo(() => getWeeklyParkingData(selectedWeek), [selectedWeek]);

  // Tổng lượt đặt trước và vãng lai
  const totalReserved = useMemo(
    () => items.reduce((acc, it) => acc + it.reserved, 0),
    [items]
  );
  const totalWalkIn = useMemo(
    () => items.reduce((acc, it) => acc + it.walkIn, 0),
    [items]
  );

  const width = 540;
  const height = 370;
  const padLeft = 60; // Lề trái đủ rộng để số 1.000 / 750 không bao giờ bị cắt
  const padRight = 20;
  const padTop = 52;
  const padBottom = 38;

  const chartW = width - padLeft - padRight;
  const chartH = height - padTop - padBottom;
  const maxVal = 1000;

  const stepTicks = [0, 250, 500, 750, 1000];
  const slotW = chartW / items.length;
  const barW = Math.min(22, (slotW - 12) / 2);

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
        position: 'relative',
      }}
    >
      {/* Header with Dropdown */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: 16,
        }}
      >
        <h2 style={{ fontSize: 20, fontWeight: 800, margin: 0, color: '#0f1b3d' }}>
          Lượt đỗ trong tuần
        </h2>
        <select
          value={selectedWeek}
          onChange={(e) => setSelectedWeek(e.target.value)}
          aria-label="Chọn tuần để xem lượt đỗ"
          style={{
            border: '1px solid #e3e8f2',
            background: '#f3f6fb',
            borderRadius: 8,
            padding: '4px 10px',
            fontSize: 12.5,
            color: '#0f1b3d',
            outline: 'none',
            fontWeight: 600,
            cursor: 'pointer',
          }}
        >
          <option value="current">Tuần này</option>
          <option value="last">Tuần trước</option>
          <option value="2weeks">2 tuần trước</option>
        </select>
      </div>

      {/* Legend with Totals */}
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: 16,
          marginBottom: 12,
          fontSize: 12.5,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span
            style={{
              width: 10,
              height: 10,
              borderRadius: '50%',
              backgroundColor: '#2563EB',
            }}
          />
          <span style={{ color: '#0f1b3d', fontWeight: 700 }}>
            Đặt trước {totalReserved.toLocaleString('vi-VN')} lượt
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span
            style={{
              width: 10,
              height: 10,
              borderRadius: '50%',
              backgroundColor: '#8B5CF6',
            }}
          />
          <span style={{ color: '#6a7491', fontWeight: 600 }}>
            Vãng lai {totalWalkIn.toLocaleString('vi-VN')} lượt
          </span>
        </div>
      </div>

      {/* SVG Grouped Bar Chart */}
      <div style={{ position: 'relative', width: '100%', flex: 1 }}>
        <svg
          viewBox={`0 0 ${width} ${height}`}
          style={{ width: '100%', height: 'auto', display: 'block' }}
          aria-label="Biểu đồ cột nhóm lượt đỗ trong tuần"
        >
          {/* Horizontal Grid lines & Y-axis labels */}
          {stepTicks.map((val) => {
            const y = padTop + chartH * (1 - val / maxVal);
            return (
              <g key={val}>
                <line
                  x1={padLeft}
                  y1={y}
                  x2={width - padRight}
                  y2={y}
                  stroke="#f1f3f7"
                  strokeDasharray="4 4"
                />
                <text
                  x={padLeft - 10}
                  y={y + 4}
                  textAnchor="end"
                  fontSize="12"
                  fill="#64748b"
                  fontWeight="600"
                >
                  {val >= 1000 ? (val / 1000).toFixed(0) + '.000' : val}
                </text>
              </g>
            );
          })}

          {/* Grouped Bars per day */}
          {items.map((item, idx) => {
            const slotX = padLeft + idx * slotW;
            const bottomY = padTop + chartH;

            const resH = Math.min(chartH, Math.max(6, (item.reserved / maxVal) * chartH));
            const walkH = Math.min(chartH, Math.max(6, (item.walkIn / maxVal) * chartH));

            const resY = bottomY - resH;
            const walkY = bottomY - walkH;

            const isHovered = hoveredIdx === idx;
            const groupCenterX = slotX + slotW / 2;
            const resX = groupCenterX - barW - 2;
            const walkX = groupCenterX + 2;

            return (
              <g
                key={item.day}
                onMouseEnter={() => setHoveredIdx(idx)}
                onMouseLeave={() => setHoveredIdx(null)}
                style={{ cursor: 'pointer' }}
              >
                {/* Background highlight for Peak day or Hover */}
                {(item.isPeak || isHovered) && (
                  <rect
                    x={slotX + 4}
                    y={padTop - 28}
                    width={slotW - 8}
                    height={chartH + 28}
                    rx={8}
                    fill={item.isPeak ? '#eff6ff' : '#f8faff'}
                    opacity={item.isPeak ? 0.9 : 0.6}
                  />
                )}

                {/* Badge "Đông nhất" if peak */}
                {item.isPeak && (
                  <g>
                    <rect
                      x={groupCenterX - 34}
                      y={padTop - 28}
                      width={68}
                      height={20}
                      rx={10}
                      fill="#2563EB"
                    />
                    <text
                      x={groupCenterX}
                      y={padTop - 14}
                      textAnchor="middle"
                      fontSize="10.5"
                      fontWeight="800"
                      fill="#fff"
                    >
                      Đông nhất
                    </text>
                  </g>
                )}

                {/* Bar 1: Đặt trước (#2563EB) */}
                <rect
                  x={resX}
                  y={resY}
                  width={barW}
                  height={resH}
                  rx={5}
                  fill="#2563EB"
                  style={{
                    filter: isHovered ? 'brightness(1.15)' : 'none',
                    transition: 'all 0.15s ease',
                  }}
                />
                {/* Number on top of reserved bar */}
                <text
                  x={resX + barW / 2}
                  y={resY - 6}
                  textAnchor="middle"
                  fontSize="11"
                  fontWeight="800"
                  fill="#2563EB"
                >
                  {item.reserved}
                </text>

                {/* Bar 2: Vãng lai (#8B5CF6) */}
                <rect
                  x={walkX}
                  y={walkY}
                  width={barW}
                  height={walkH}
                  rx={5}
                  fill="#8B5CF6"
                  style={{
                    filter: isHovered ? 'brightness(1.15)' : 'none',
                    transition: 'all 0.15s ease',
                  }}
                />
                {/* Number on top of walk-in bar */}
                <text
                  x={walkX + barW / 2}
                  y={walkY - 6}
                  textAnchor="middle"
                  fontSize="11"
                  fontWeight="800"
                  fill="#8B5CF6"
                >
                  {item.walkIn}
                </text>

                {/* X axis Day label */}
                <text
                  x={groupCenterX}
                  y={height - 12}
                  textAnchor="middle"
                  fontSize="13"
                  fill={item.isPeak ? '#2563EB' : '#334155'}
                  fontWeight={item.isPeak ? '800' : '700'}
                >
                  {item.day}
                </text>
              </g>
            );
          })}
        </svg>

        {/* Hover / Touch Tooltip popover with white background */}
        {hoveredIdx !== null && items[hoveredIdx] && (
          <div
            style={{
              position: 'absolute',
              top: 10,
              left: Math.min(
                width - 180,
                Math.max(10, padLeft + hoveredIdx * slotW + slotW / 2 - 75)
              ),
              background: '#ffffff',
              borderRadius: 12,
              padding: '10px 14px',
              boxShadow: '0 8px 24px rgba(15, 27, 61, 0.16)',
              border: '1px solid #e3e8f2',
              pointerEvents: 'none',
              zIndex: 10,
              minWidth: 150,
            }}
          >
            <div style={{ fontWeight: 800, fontSize: 13, color: '#0f1b3d', marginBottom: 6 }}>
              Thứ {items[hoveredIdx].day}
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, marginBottom: 3 }}>
              <span style={{ color: '#2563EB', fontWeight: 600 }}>Đặt trước:</span>
              <strong style={{ color: '#0f1b3d' }}>{items[hoveredIdx].reserved.toLocaleString('vi-VN')}</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, marginBottom: 4 }}>
              <span style={{ color: '#8B5CF6', fontWeight: 600 }}>Vãng lai:</span>
              <strong style={{ color: '#0f1b3d' }}>{items[hoveredIdx].walkIn.toLocaleString('vi-VN')}</strong>
            </div>
            <div
              style={{
                borderTop: '1px solid #f1f3f7',
                paddingTop: 4,
                display: 'flex',
                justifyContent: 'space-between',
                fontSize: 12,
              }}
            >
              <span style={{ color: '#64748b' }}>Tổng cộng:</span>
              <strong style={{ color: '#0f1b3d' }}>{items[hoveredIdx].total.toLocaleString('vi-VN')}</strong>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
