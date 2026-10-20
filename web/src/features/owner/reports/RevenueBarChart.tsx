'use client';

import React from 'react';
import { RevenueBarItem } from './revenueAdapter';

interface RevenueBarChartProps {
  items: RevenueBarItem[];
  title?: string;
}

export function RevenueBarChart({
  items,
  title = 'Biểu đồ doanh thu theo thời gian',
}: RevenueBarChartProps) {
  const width = 800;
  const height = 280;
  const padLeft = 45;
  const padRight = 20;
  const padTop = 30;
  const padBottom = 35;

  const chartW = width - padLeft - padRight;
  const chartH = height - padTop - padBottom;

  const maxVal = Math.max(...items.map((i) => i.revenue), 1000);
  const gap = chartW / items.length;
  const barW = Math.max(Math.min(gap * 0.6, 24), 8);

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
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <h2 style={{ fontSize: 18, fontWeight: 800, margin: 0, color: '#0f1b3d' }}>
          {title}
        </h2>
      </div>

      <div style={{ position: 'relative', width: '100%', overflowX: 'auto' }}>
        <svg
          viewBox={`0 0 ${width} ${height}`}
          style={{ width: '100%', minWidth: 500, height: 'auto', display: 'block' }}
          aria-label={title}
        >
          {/* Grid lines */}
          {[0, 0.25, 0.5, 0.75, 1].map((pct, idx) => {
            const y = padTop + chartH * (1 - pct);
            const val = Math.round(maxVal * pct);
            return (
              <g key={idx}>
                <line
                  x1={padLeft}
                  y1={y}
                  x2={width - padRight}
                  y2={y}
                  stroke="#f0f2f5"
                  strokeDasharray="4 4"
                />
                <text
                  x={padLeft - 8}
                  y={y + 4}
                  textAnchor="end"
                  fontSize="11"
                  fill="#9aa2b4"
                >
                  {val >= 1e6 ? `${(val / 1e6).toFixed(1)} tr` : val.toLocaleString('vi-VN')}
                </text>
              </g>
            );
          })}

          {/* Bars */}
          {items.map((item, idx) => {
            const x = padLeft + idx * gap + gap / 2 - barW / 2;
            const bH = (item.revenue / maxVal) * chartH;
            const y = padTop + chartH - bH;
            const isHighlight = item.isPeak;

            return (
              <g key={idx}>
                <rect
                  x={x}
                  y={y}
                  width={barW}
                  height={bH}
                  rx={5}
                  fill={isHighlight ? '#2563eb' : '#93c5fd'}
                />
                <text
                  x={x + barW / 2}
                  y={height - 12}
                  textAnchor="middle"
                  fontSize="11"
                  fill={isHighlight ? '#0f1b3d' : '#6a7491'}
                  fontWeight={isHighlight ? '700' : '500'}
                >
                  {item.label}
                </text>
              </g>
            );
          })}
        </svg>
      </div>
    </div>
  );
}
