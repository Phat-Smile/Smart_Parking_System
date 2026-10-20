'use client';

import React from 'react';
import { useOwnerStore, getSpotCounts } from '@/store/useOwnerStore';
import { ST, SlotStatusKey } from '@/types/owner';
import { K_VISIBLE } from '@/lib/mapStatus';

interface SlotStatusChipsProps {
  selectedStatus: SlotStatusKey | null;
  onToggleStatus: (status: SlotStatusKey) => void;
  onClearStatus: () => void;
}

export function SlotStatusChips({
  selectedStatus,
  onToggleStatus,
  onClearStatus,
}: SlotStatusChipsProps) {
  const spots = useOwnerStore((s) => s.spots);
  const counts = getSpotCounts(spots);

  return (
    <div
      id="vchips"
      role="toolbar"
      aria-label="Lọc ô theo trạng thái"
      style={{
        marginBottom: 14,
        display: 'flex',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: 8,
      }}
    >
      {K_VISIBLE.map((k) => {
        const isSelected = selectedStatus === k;
        const [label, color] = ST[k];
        const count = counts[k] || 0;

        return (
          <button
            key={k}
            type="button"
            onClick={() => onToggleStatus(k)}
            aria-pressed={isSelected}
            aria-label={`${label}: ${count} ô`}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 8,
              padding: '6px 14px',
              borderRadius: 999,
              fontSize: 12.5,
              fontWeight: isSelected ? 800 : 600,
              cursor: 'pointer',
              background: isSelected ? `${color}18` : '#ffffff',
              color: isSelected ? color : '#334155',
              border: isSelected ? `2px solid ${color}` : '1.5px solid #e2e8f0',
              boxShadow: isSelected
                ? `0 2px 8px ${color}33`
                : '0 1px 2px rgba(15, 27, 61, 0.04)',
              transition: 'all 0.15s ease',
              minHeight: 36,
            }}
          >
            <span
              style={{
                width: 8,
                height: 8,
                borderRadius: '50%',
                backgroundColor: color,
                flexShrink: 0,
              }}
            />
            <span>
              {label} <strong style={{ marginLeft: 2 }}>{count}</strong>
            </span>
          </button>
        );
      })}

      {selectedStatus && (
        <button
          type="button"
          onClick={onClearStatus}
          aria-label="Bỏ lọc trạng thái"
          style={{
            border: '1px solid #e2e8f0',
            background: '#f8faff',
            color: '#64748b',
            borderRadius: 999,
            padding: '6px 12px',
            fontSize: 12,
            fontWeight: 600,
            cursor: 'pointer',
            display: 'inline-flex',
            alignItems: 'center',
            gap: 4,
            minHeight: 36,
          }}
        >
          ✕ Bỏ lọc
        </button>
      )}
    </div>
  );
}
