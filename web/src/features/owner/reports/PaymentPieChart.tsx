'use client';

import React from 'react';
import { PaymentMethodItem } from './revenueAdapter';

interface PaymentPieChartProps {
  items: PaymentMethodItem[];
}

export function PaymentPieChart({ items }: PaymentPieChartProps) {
  const size = 140;
  const strokeWidth = 22;
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;

  // Pure functional calculation of strokeDashoffset
  const segments = items.map((item, i) => {
    const pct = item.pct / 100;
    const prevSum = items.slice(0, i).reduce((sum, prev) => sum + prev.pct / 100, 0);
    const strokeDasharray = `${pct * circumference} ${circumference}`;
    const strokeDashoffset = -prevSum * circumference;
    return {
      ...item,
      strokeDasharray,
      strokeDashoffset,
    };
  });

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
      <h2 style={{ fontSize: 18, fontWeight: 800, margin: '0 0 16px', color: '#0f1b3d' }}>
        Phương thức thanh toán
      </h2>

      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 20,
          flexWrap: 'wrap',
          flex: 1,
        }}
      >
        <div style={{ position: 'relative', width: size, height: size, flexShrink: 0, margin: '0 auto' }}>
          <svg
            width={size}
            height={size}
            viewBox={`0 0 ${size} ${size}`}
            style={{ transform: 'rotate(-90deg)' }}
            aria-label="Biểu đồ tỷ lệ phương thức thanh toán"
          >
            <circle
              cx={size / 2}
              cy={size / 2}
              r={radius}
              fill="none"
              stroke="#f3f6fb"
              strokeWidth={strokeWidth}
            />
            {segments.map((seg) => (
              <circle
                key={seg.name}
                cx={size / 2}
                cy={size / 2}
                r={radius}
                fill="none"
                stroke={seg.color}
                strokeWidth={strokeWidth}
                strokeDasharray={seg.strokeDasharray}
                strokeDashoffset={seg.strokeDashoffset}
              />
            ))}
          </svg>
        </div>

        <div style={{ flex: 1, minWidth: 150, display: 'flex', flexDirection: 'column', gap: 10 }}>
          {items.map((m) => (
            <div
              key={m.name}
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                fontSize: 13,
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <span
                  style={{
                    width: 8,
                    height: 8,
                    borderRadius: '50%',
                    backgroundColor: m.color,
                  }}
                />
                <span style={{ color: '#0f1b3d', fontWeight: 500 }}>{m.name}</span>
              </div>
              <span style={{ fontWeight: 700, color: '#0f1b3d' }}>
                {m.pct}%
              </span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
