'use client';

import React, { useState } from 'react';
import { getWeeklyParkingData } from '@/features/owner/dashboard/trafficAdapter';

export function WeeklyParkingChart() {
  const [selectedWeek, setSelectedWeek] = useState('current');
  const items = getWeeklyParkingData();

  const width = 360;
  const height = 310;
  const padLeft = 40;
  const padRight = 15;
  const padTop = 45;
  const padBottom = 30;

  const chartW = width - padLeft - padRight;
  const chartH = height - padTop - padBottom;
  const maxVal = 2000;

  const colWidth = 16;
  const gap = chartW / items.length;

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
      {/* Header with Dropdown */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: 16,
        }}
      >
        <h2 style={{ fontSize: 18, fontWeight: 800, margin: 0, color: '#0f1b3d' }}>
          Lượt đỗ trong tuần
        </h2>
        <select
          value={selectedWeek}
          onChange={(e) => setSelectedWeek(e.target.value)}
          aria-label="Chọn tuần"
          style={{
            border: '1px solid #e3e8f2',
            background: '#f3f6fb',
            borderRadius: 8,
            padding: '4px 10px',
            fontSize: 12.5,
            color: '#0f1b3d',
            outline: 'none',
            fontWeight: 600,
          }}
        >
          <option value="current">Tuần này</option>
          <option value="last">Tuần trước</option>
          <option value="2weeks">2 tuần trước</option>
        </select>
      </div>

      {/* Legend */}
      <div style={{ display: 'flex', gap: 16, marginBottom: 14, fontSize: 12.5 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span
            style={{
              width: 9,
              height: 9,
              borderRadius: '50%',
              backgroundColor: '#2563eb',
            }}
          />
          <span style={{ color: '#0f1b3d', fontWeight: 600 }}>Đặt trước</span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span
            style={{
              width: 9,
              height: 9,
              borderRadius: '50%',
              backgroundColor: '#bfdbfe',
            }}
          />
          <span style={{ color: '#6a7491' }}>Vãng lai</span>
        </div>
      </div>

      {/* SVG Stacked Bar Chart */}
      <div style={{ position: 'relative', width: '100%', flex: 1 }}>
        <svg
          viewBox={`0 0 ${width} ${height}`}
          style={{ width: '100%', height: 'auto', display: 'block' }}
          aria-label="Biểu đồ lượt đỗ trong tuần"
        >
          {/* Horizontal Grid lines */}
          {[0, 500, 1000, 1500, 2000].map((val) => {
            const y = padTop + chartH * (1 - val / maxVal);
            return (
              <g key={val}>
                <line
                  x1={padLeft}
                  y1={y}
                  x2={width - padRight}
                  y2={y}
                  stroke="#f0f2f5"
                  strokeDasharray="4 4"
                />
                <text
                  x={padLeft - 6}
                  y={y + 4}
                  textAnchor="end"
                  fontSize="11"
                  fill="#9aa2b4"
                >
                  {val >= 1000 ? (val / 1000).toFixed(val % 1000 === 0 ? 0 : 1) + '.000' : val}
                </text>
              </g>
            );
          })}

          {/* Bars */}
          {items.map((item, idx) => {
            const x = padLeft + idx * gap + gap / 2 - colWidth / 2;
            const bottomY = padTop + chartH;

            const resH = (item.reserved / maxVal) * chartH;
            const walkH = (item.walkIn / maxVal) * chartH;
            const totalH = resH + walkH;

            const resY = bottomY - resH;
            const walkY = bottomY - totalH;

            return (
              <g key={item.day}>
                {/* Badge "Đông nhất" if peak */}
                {item.isPeak && (
                  <g>
                    <rect
                      x={x + colWidth / 2 - 32}
                      y={walkY - 34}
                      width={64}
                      height={20}
                      rx={10}
                      fill="#0f1b3d"
                    />
                    <text
                      x={x + colWidth / 2}
                      y={walkY - 20}
                      textAnchor="middle"
                      fontSize="10"
                      fontWeight="700"
                      fill="#fff"
                    >
                      Đông nhất
                    </text>
                  </g>
                )}

                {/* Number above bar */}
                <text
                  x={x + colWidth / 2}
                  y={walkY - 6}
                  textAnchor="middle"
                  fontSize="10"
                  fontWeight="700"
                  fill="#0f1b3d"
                >
                  {item.total.toLocaleString('vi-VN')}
                </text>

                {/* Stacked Bars: Top = Vãng lai (light blue), Bottom = Đặt trước (darker blue) */}
                {/* Walk-in top segment */}
                <rect
                  x={x}
                  y={walkY}
                  width={colWidth}
                  height={walkH}
                  rx={item.reserved === 0 ? 8 : 0}
                  fill="#bfdbfe"
                  style={{ borderTopLeftRadius: 8, borderTopRightRadius: 8 }}
                />
                {/* Reserved bottom segment */}
                <rect
                  x={x}
                  y={resY}
                  width={colWidth}
                  height={resH}
                  fill="#2563eb"
                  rx={0}
                />

                {/* X axis label */}
                <text
                  x={x + colWidth / 2}
                  y={height - 10}
                  textAnchor="middle"
                  fontSize="11.5"
                  fill={item.isPeak ? '#0f1b3d' : '#6a7491'}
                  fontWeight={item.isPeak ? '700' : '500'}
                >
                  {item.day}
                </text>
              </g>
            );
          })}
        </svg>
      </div>
    </div>
  );
}
