'use client';

import React, { useState, useMemo } from 'react';
import { useOwnerSearchStore } from '@/features/owner/search/useOwnerSearchStore';
import { ReportTableRow } from './revenueAdapter';

interface ReportDetailTableProps {
  rows: ReportTableRow[];
}

export function ReportDetailTable({ rows }: ReportDetailTableProps) {
  const { query, clearQuery } = useOwnerSearchStore();
  const [page, setPage] = useState(1);
  const pageSize = 8;

  // Lọc dòng theo nhãn thời gian từ ô tìm kiếm Header
  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter((r) => r.time.toLowerCase().includes(q));
  }, [rows, query]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const currentRows = filtered.slice((page - 1) * pageSize, page * pageSize);

  return (
    <div
      className="ov2-card"
      style={{
        background: '#fff',
        borderRadius: 18,
        border: '1px solid #e3e8f2',
        padding: 24,
        marginTop: 20,
      }}
    >
      {/* Top title & Clear filters if active */}
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
          Chi tiết doanh thu theo mốc
        </h2>

        {query && (
          <button
            type="button"
            onClick={() => {
              clearQuery();
              setPage(1);
            }}
            aria-label="Xóa bộ lọc mốc thời gian"
            style={{
              border: '1px solid #e2e8f0',
              background: '#fff',
              color: '#64748b',
              borderRadius: 8,
              padding: '6px 12px',
              fontSize: 12.5,
              fontWeight: 600,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 6,
            }}
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
            Xóa bộ lọc
          </button>
        )}
      </div>

      {/* Table */}
      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
          <thead>
            <tr style={{ borderBottom: '1px solid #e3e8f2' }}>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Thời gian
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Lượt đỗ xe
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Doanh thu
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Phương thức phổ biến
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Trạng thái
              </th>
            </tr>
          </thead>
          <tbody>
            {currentRows.length > 0 ? (
              currentRows.map((r, idx) => (
                <tr
                  key={idx}
                  style={{
                    borderBottom: '1px solid #f5f7fa',
                    fontSize: 13,
                    color: '#0f1b3d',
                  }}
                >
                  <td style={{ padding: '12px 14px', fontWeight: 600 }}>{r.time}</td>
                  <td style={{ padding: '12px 14px' }}>{r.sessions} lượt</td>
                  <td style={{ padding: '12px 14px', fontWeight: 700, color: '#16a36a' }}>
                    {r.revenueText}
                  </td>
                  <td style={{ padding: '12px 14px', color: '#6a7491' }}>{r.popularMethod}</td>
                  <td style={{ padding: '12px 14px' }}>
                    <span
                      style={{
                        background: '#def7ec',
                        color: '#0e9f6e',
                        padding: '3px 8px',
                        borderRadius: 999,
                        fontSize: 11.5,
                        fontWeight: 600,
                      }}
                    >
                      {r.status}
                    </span>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={5} style={{ padding: '36px 16px', textAlign: 'center' }}>
                  <div style={{ color: '#6a7491', fontSize: 14, fontWeight: 500, marginBottom: 12 }}>
                    Không tìm thấy kết quả
                  </div>
                  {query && (
                    <button
                      type="button"
                      onClick={() => {
                        clearQuery();
                        setPage(1);
                      }}
                      style={{
                        background: '#eff6ff',
                        color: '#2563eb',
                        border: '1px solid #bfdbfe',
                        borderRadius: 8,
                        padding: '6px 14px',
                        fontSize: 12.5,
                        fontWeight: 600,
                        cursor: 'pointer',
                      }}
                    >
                      Xóa bộ lọc
                    </button>
                  )}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div
          style={{
            display: 'flex',
            justifyContent: 'flex-end',
            alignItems: 'center',
            gap: 8,
            marginTop: 16,
          }}
        >
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            style={{
              border: '1px solid #e3e8f2',
              background: '#fff',
              borderRadius: 6,
              padding: '4px 10px',
              fontSize: 12,
              cursor: page <= 1 ? 'not-allowed' : 'pointer',
              opacity: page <= 1 ? 0.5 : 1,
            }}
          >
            Trước
          </button>
          <span style={{ fontSize: 12, color: '#6a7491' }}>
            Trang {page} / {totalPages}
          </span>
          <button
            type="button"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            style={{
              border: '1px solid #e3e8f2',
              background: '#fff',
              borderRadius: 6,
              padding: '4px 10px',
              fontSize: 12,
              cursor: page >= totalPages ? 'not-allowed' : 'pointer',
              opacity: page >= totalPages ? 0.5 : 1,
            }}
          >
            Sau
          </button>
        </div>
      )}
    </div>
  );
}
