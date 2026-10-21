'use client';

import React, { useEffect, useRef } from 'react';
import { usePathname } from 'next/navigation';
import { useOwnerSearchStore } from './useOwnerSearchStore';

export const ROUTE_SEARCH_CONFIG: Record<string, { placeholder: string; label: string }> = {
  '/owner/parking-lots': {
    placeholder: 'Tìm bãi theo tên hoặc địa chỉ',
    label: 'Tìm kiếm bãi đỗ xe',
  },
  '/owner/reservations': {
    placeholder: 'Tìm mã đặt chỗ hoặc biển số',
    label: 'Tìm kiếm lượt đặt chỗ',
  },
  '/owner/sessions': {
    placeholder: 'Tìm biển số hoặc mã ô',
    label: 'Tìm kiếm phiên đỗ xe',
  },
  '/owner/reports': {
    placeholder: 'Tìm theo ngày hoặc tháng, ví dụ 12/10',
    label: 'Tìm kiếm báo cáo doanh thu',
  },
};

export function HeaderSearch() {
  const pathname = usePathname();
  const { query, setQuery, clearQuery } = useOwnerSearchStore();
  const prevPathRef = useRef(pathname);

  // Tìm config phù hợp với pathname hiện tại (chính xác hoặc tiền tố)
  const matchingKey = Object.keys(ROUTE_SEARCH_CONFIG).find((route) => {
    return pathname === route || (pathname.startsWith(route + '/') && !pathname.includes('/new') && !pathname.includes('/pricing'));
  });

  const config = matchingKey ? ROUTE_SEARCH_CONFIG[matchingKey] : null;

  // Tự động xóa query khi đổi trang
  useEffect(() => {
    if (prevPathRef.current !== pathname) {
      clearQuery();
      prevPathRef.current = pathname;
    }
  }, [pathname, clearQuery]);

  // Không hiển thị ở trang không thuộc danh sách cấu hình
  if (!config) {
    return null;
  }

  return (
    <div className="ov2-header-search-wrapper" style={{ position: 'relative' }}>
      <label htmlFor="ov2-header-search-input" className="sr-only" style={{ position: 'absolute', width: 1, height: 1, padding: 0, margin: -1, overflow: 'hidden', clip: 'rect(0, 0, 0, 0)', whiteSpace: 'nowrap', border: 0 }}>
        {config.label}
      </label>
      <div
        className="ov2-header-search-box"
        style={{
          display: 'flex',
          alignItems: 'center',
          background: '#f3f6fb',
          borderRadius: 999,
          padding: '6px 16px',
          width: 300,
          maxWidth: '100%',
          minHeight: 44,
          border: '1.5px solid #e3e8f2',
          transition: 'all 0.2s ease',
        }}
      >
        <svg
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="none"
          stroke="#6a7491"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          style={{ marginRight: 10, flexShrink: 0 }}
          aria-hidden="true"
        >
          <circle cx="11" cy="11" r="8" />
          <line x1="21" y1="21" x2="16.65" y2="16.65" />
        </svg>
        <input
          id="ov2-header-search-input"
          type="text"
          placeholder={config.placeholder}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          aria-label={config.placeholder}
          style={{
            border: 'none',
            background: 'transparent',
            outline: 'none',
            width: '100%',
            fontSize: 13,
            color: '#0f1b3d',
            minHeight: 32,
          }}
        />
        {query && (
          <button
            type="button"
            onClick={() => clearQuery()}
            aria-label="Xóa nội dung tìm kiếm"
            style={{
              background: 'none',
              border: 'none',
              cursor: 'pointer',
              padding: 4,
              minWidth: 28,
              minHeight: 28,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#94a3b8',
              borderRadius: '50%',
            }}
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        )}
      </div>
    </div>
  );
}
