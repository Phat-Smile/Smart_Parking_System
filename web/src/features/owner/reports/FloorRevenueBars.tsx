'use client';

import React from 'react';
import { FloorRevenueItem } from './revenueAdapter';

interface FloorRevenueBarsProps {
  items: FloorRevenueItem[];
}

export function FloorRevenueBars({ items }: FloorRevenueBarsProps) {
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
        Doanh thu theo từng tầng
      </h2>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
        {items.map((item) => (
          <div key={item.floor}>
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                fontSize: 13,
                marginBottom: 6,
              }}
            >
              <span style={{ fontWeight: 600, color: '#0f1b3d' }}>{item.name}</span>
              <span style={{ fontWeight: 700, color: '#2563eb' }}>
                {item.revenueText} ({item.pct}%)
              </span>
            </div>
            <div
              style={{
                width: '100%',
                height: 8,
                backgroundColor: '#f3f6fb',
                borderRadius: 99,
                overflow: 'hidden',
              }}
            >
              <div
                style={{
                  width: `${item.pct}%`,
                  height: '100%',
                  backgroundColor: '#2563eb',
                  borderRadius: 99,
                }}
              />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
