'use client';

import React, { useState, useMemo } from 'react';
import { getTrafficData, TrafficData } from '@/features/owner/dashboard/trafficAdapter';

export function TrafficChart() {
  const [period, setPeriod] = useState<'day' | 'week' | 'month'>('week');
  const [hoveredIdx, setHoveredIdx] = useState<number | null>(null);

  const data: TrafficData = useMemo(() => getTrafficData(period), [period]);

  // SVG Chart Dimensions
  const width = 680;
  const height = 280;
  const padLeft = 52;
  const padRight = 24;
  const padTop = 38;
  const padBottom = 35;

  const chartW = width - padLeft - padRight;
  const chartH = height - padTop - padBottom;

  const maxVal = useMemo(() => {
    let max = 0;
    data.points.forEach((p) => {
      if (p.inCount > max) max = p.inCount;
      if (p.outCount > max) max = p.outCount;
    });
    if (period === 'month') return 40000;
    if (period === 'day') return 140;
    return 1600;
  }, [data, period]);

  // Coordinates calculation
  const coords = useMemo(() => {
    const n = data.points.length;
    return data.points.map((p, i) => {
      const x = padLeft + (i / Math.max(n - 1, 1)) * chartW;
      const yIn = padTop + chartH - Math.max(0, (p.inCount / maxVal) * chartH);
      const yOut = padTop + chartH - Math.max(0, (p.outCount / maxVal) * chartH);
      return { x, yIn, yOut, point: p, index: i };
    });
  }, [data, chartW, chartH, maxVal]);

  // Build smooth bezier path
  const makeSmoothPath = (pts: { x: number; y: number }[]) => {
    if (pts.length === 0) return '';
    if (pts.length === 1) return `M ${pts[0].x} ${pts[0].y}`;
    let d = `M ${pts[0].x} ${pts[0].y}`;
    for (let i = 0; i < pts.length - 1; i++) {
      const p0 = pts[i];
      const p1 = pts[i + 1];
      const cx = (p0.x + p1.x) / 2;
      d += ` C ${cx} ${p0.y}, ${cx} ${p1.y}, ${p1.x} ${p1.y}`;
    }
    return d;
  };

  const inLinePath = useMemo(
    () => makeSmoothPath(coords.map((c) => ({ x: c.x, y: c.yIn }))),
    [coords]
  );
  const outLinePath = useMemo(
    () => makeSmoothPath(coords.map((c) => ({ x: c.x, y: c.yOut }))),
    [coords]
  );

  const inAreaPath = useMemo(() => {
    if (coords.length === 0) return '';
    const bottom = padTop + chartH;
    return `${inLinePath} L ${coords[coords.length - 1].x} ${bottom} L ${coords[0].x} ${bottom} Z`;
  }, [inLinePath, coords, chartH]);

  const outAreaPath = useMemo(() => {
    if (coords.length === 0) return '';
    const bottom = padTop + chartH;
    return `${outLinePath} L ${coords[coords.length - 1].x} ${bottom} L ${coords[0].x} ${bottom} Z`;
  }, [outLinePath, coords, chartH]);

  // Điểm cao điểm chính xác = điểm có isPeak (max(in, out) lớn nhất)
  const peakCoord = coords.find((c) => c.point.isPeak);

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
      {/* Top Header & Period Switcher */}
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
          Lưu lượng xe
        </h2>

        <div
          style={{
            display: 'flex',
            background: '#f3f6fb',
            borderRadius: 999,
            padding: 4,
            gap: 2,
          }}
        >
          {(['day', 'week', 'month'] as const).map((p) => {
            const labels = { day: 'Ngày', week: 'Tuần', month: 'Tháng' };
            const isActive = period === p;
            return (
              <button
                key={p}
                type="button"
                onClick={() => {
                  setPeriod(p);
                  setHoveredIdx(null);
                }}
                aria-pressed={isActive}
                style={{
                  border: 'none',
                  background: isActive ? '#2563EB' : 'transparent',
                  color: isActive ? '#fff' : '#6a7491',
                  fontWeight: isActive ? 700 : 500,
                  fontSize: 13,
                  padding: '6px 14px',
                  borderRadius: 999,
                  cursor: 'pointer',
                  transition: 'all 0.15s ease',
                }}
              >
                {labels[p]}
              </button>
            );
          })}
        </div>
      </div>

      {/* Legend & Stats */}
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          alignItems: 'center',
          gap: 16,
          marginBottom: 12,
          fontSize: 13,
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
          <span style={{ fontWeight: 700, color: '#0f1b3d' }}>Xe vào</span>
          <span style={{ color: '#6a7491', fontSize: 12 }}>
            {data.totalIn.toLocaleString('vi-VN')} lượt · {data.dateRangeText}
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span
            style={{
              width: 10,
              height: 10,
              borderRadius: '50%',
              backgroundColor: '#E5484D',
            }}
          />
          <span style={{ fontWeight: 700, color: '#0f1b3d' }}>Xe ra</span>
          <span style={{ color: '#6a7491', fontSize: 12 }}>
            {data.totalOut.toLocaleString('vi-VN')} lượt
          </span>
        </div>
      </div>

      {/* Thông tin cao điểm & max in / max out */}
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: 8,
          marginBottom: 16,
        }}
      >
        <span
          style={{
            background: '#eff6ff',
            border: '1px solid #dbeafe',
            borderRadius: 8,
            padding: '5px 12px',
            fontSize: 12,
            color: '#1e40af',
          }}
        >
          {data.peakPeriodTitle}: <strong>{data.peakLabel}</strong> ({data.peakDetails})
        </span>
        <span
          style={{
            background: '#f3f6fb',
            borderRadius: 8,
            padding: '5px 12px',
            fontSize: 12,
            color: '#6a7491',
          }}
        >
          Vào nhiều nhất: <strong style={{ color: '#2563EB' }}>{data.maxInLabel}</strong>
        </span>
        <span
          style={{
            background: '#f3f6fb',
            borderRadius: 8,
            padding: '5px 12px',
            fontSize: 12,
            color: '#6a7491',
          }}
        >
          Ra nhiều nhất: <strong style={{ color: '#E5484D' }}>{data.maxOutLabel}</strong>
        </span>
      </div>

      {/* SVG Chart */}
      <div style={{ position: 'relative', width: '100%', overflowX: 'auto' }}>
        <svg
          viewBox={`0 0 ${width} ${height}`}
          style={{ width: '100%', minWidth: 520, height: 'auto', display: 'block' }}
          aria-label={`Biểu đồ lưu lượng xe theo ${period}`}
        >
          <defs>
            <linearGradient id="ov2InGrad" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#2563EB" stopOpacity="0.22" />
              <stop offset="100%" stopColor="#2563EB" stopOpacity="0.0" />
            </linearGradient>
            <linearGradient id="ov2OutGrad" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#E5484D" stopOpacity="0.18" />
              <stop offset="100%" stopColor="#E5484D" stopOpacity="0.0" />
            </linearGradient>
          </defs>

          {/* Grid lines (horizontal) */}
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
                  stroke="#f1f3f7"
                  strokeDasharray="4 4"
                />
                <text
                  x={padLeft - 8}
                  y={y + 4}
                  textAnchor="end"
                  fontSize="11"
                  fill="#94a3b8"
                  fontWeight="500"
                >
                  {val >= 1000 ? (val / 1000).toFixed(val % 1000 === 0 ? 0 : 1) + '.000' : val}
                </text>
              </g>
            );
          })}

          {/* Dải nền xanh nhạt tại điểm cao điểm */}
          {peakCoord && (
            <g>
              <rect
                x={peakCoord.x - (period === 'day' ? 14 : 26)}
                y={padTop}
                width={period === 'day' ? 28 : 52}
                height={chartH}
                fill="#eff6ff"
                rx={6}
                opacity={0.85}
              />
              <rect
                x={peakCoord.x - 26}
                y={padTop - 22}
                width={52}
                height={18}
                rx={9}
                fill="#2563EB"
              />
              <text
                x={peakCoord.x}
                y={padTop - 10}
                textAnchor="middle"
                fontSize="9.5"
                fontWeight="700"
                fill="#fff"
              >
                Cao điểm
              </text>
            </g>
          )}

          {/* Filled Areas */}
          <path d={inAreaPath} fill="url(#ov2InGrad)" />
          <path d={outAreaPath} fill="url(#ov2OutGrad)" />

          {/* Lines */}
          <path
            d={inLinePath}
            fill="none"
            stroke="#2563EB"
            strokeWidth="2.5"
            strokeLinecap="round"
          />
          <path
            d={outLinePath}
            fill="none"
            stroke="#E5484D"
            strokeWidth="2.5"
            strokeLinecap="round"
          />

          {/* Dots on line points */}
          {coords.map((c, i) => {
            const isHovered = hoveredIdx === i;
            const isPeak = c.point.isPeak;

            // Only show dots on hover or peak, or spaced out on month/week
            if (period === 'day' && !isPeak && !isHovered) return null;

            return (
              <g
                key={i}
                onMouseEnter={() => setHoveredIdx(i)}
                onMouseLeave={() => setHoveredIdx(null)}
                style={{ cursor: 'pointer' }}
              >
                {/* Blue In Dot */}
                <circle
                  cx={c.x}
                  cy={c.yIn}
                  r={isPeak ? 5 : 4}
                  fill="#fff"
                  stroke="#2563EB"
                  strokeWidth={2.5}
                />
                {/* Red Out Dot */}
                <circle
                  cx={c.x}
                  cy={c.yOut}
                  r={isPeak ? 5 : 4}
                  fill="#fff"
                  stroke="#E5484D"
                  strokeWidth={2.5}
                />

                {/* Số của cả hai đường tại điểm Cao điểm */}
                {isPeak && (
                  <>
                    <text
                      x={c.x}
                      y={Math.min(c.yIn, c.yOut) - 8}
                      textAnchor="middle"
                      fontSize="9.5"
                      fontWeight="800"
                      fill="#2563EB"
                    >
                      {c.point.inCount.toLocaleString('vi-VN')}
                    </text>
                    <text
                      x={c.x}
                      y={Math.max(c.yIn, c.yOut) + 14}
                      textAnchor="middle"
                      fontSize="9.5"
                      fontWeight="800"
                      fill="#E5484D"
                    >
                      {c.point.outCount.toLocaleString('vi-VN')}
                    </text>
                  </>
                )}
              </g>
            );
          })}

          {/* 24 giờ đủ nhãn ở chế độ Ngày + đủ nhãn cho Tuần / Tháng */}
          {coords.map((c, i) => {
            return (
              <g
                key={i}
                onMouseEnter={() => setHoveredIdx(i)}
                onMouseLeave={() => setHoveredIdx(null)}
                style={{ cursor: 'pointer' }}
              >
                {/* Hit zone */}
                <rect
                  x={c.x - (period === 'day' ? 10 : 20)}
                  y={padTop}
                  width={period === 'day' ? 20 : 40}
                  height={chartH + 20}
                  fill="transparent"
                />
                <text
                  x={c.x}
                  y={height - 10}
                  textAnchor="middle"
                  fontSize={period === 'day' ? '9' : '11'}
                  fill={c.point.isPeak ? '#2563EB' : '#64748b'}
                  fontWeight={c.point.isPeak ? '800' : '500'}
                >
                  {c.point.label}
                </text>
              </g>
            );
          })}
        </svg>

        {/* Tooltip nền trắng khi hover */}
        {hoveredIdx !== null && coords[hoveredIdx] && (
          <div
            style={{
              position: 'absolute',
              top: 10,
              left: Math.min(
                width - 190,
                Math.max(10, coords[hoveredIdx].x - 80)
              ),
              background: '#ffffff',
              borderRadius: 12,
              padding: '10px 14px',
              boxShadow: '0 8px 24px rgba(15, 27, 61, 0.16)',
              border: '1px solid #e3e8f2',
              pointerEvents: 'none',
              zIndex: 10,
              minWidth: 160,
            }}
          >
            <div style={{ fontWeight: 800, fontSize: 13, color: '#0f1b3d', marginBottom: 6 }}>
              {period === 'day' ? `Thời điểm: ${coords[hoveredIdx].point.label}` : coords[hoveredIdx].point.label}
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, marginBottom: 3 }}>
              <span style={{ color: '#2563EB', fontWeight: 600 }}>Xe vào:</span>
              <strong style={{ color: '#0f1b3d' }}>
                {coords[hoveredIdx].point.inCount.toLocaleString('vi-VN')}
              </strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, marginBottom: 3 }}>
              <span style={{ color: '#E5484D', fontWeight: 600 }}>Xe ra:</span>
              <strong style={{ color: '#0f1b3d' }}>
                {coords[hoveredIdx].point.outCount.toLocaleString('vi-VN')}
              </strong>
            </div>
            {coords[hoveredIdx].point.inLotCount !== undefined && (
              <div
                style={{
                  borderTop: '1px solid #f1f3f7',
                  paddingTop: 4,
                  marginTop: 4,
                  display: 'flex',
                  justifyContent: 'space-between',
                  fontSize: 12,
                }}
              >
                <span style={{ color: '#64748b' }}>Xe trong bãi:</span>
                <strong style={{ color: '#0f1b3d' }}>
                  {coords[hoveredIdx].point.inLotCount} xe
                </strong>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
