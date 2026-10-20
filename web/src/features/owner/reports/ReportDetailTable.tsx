'use client';

import React, { useState } from 'react';
import { ReportTableRow } from './revenueAdapter';

interface ReportDetailTableProps {
  rows: ReportTableRow[];
}

export function ReportDetailTable({ rows }: ReportDetailTableProps) {
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 8;

  const filtered = rows.filter(
    (r) =>
      r.time.toLowerCase().includes(searchTerm.toLowerCase()) ||
      r.popularMethod.toLowerCase().includes(searchTerm.toLowerCase())
  );

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
      {/* Top search & title */}
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

        <input
          type="text"
          placeholder="Tìm mốc thời gian..."
          value={searchTerm}
          onChange={(e) => {
            setSearchTerm(e.target.value);
            setPage(1);
          }}
          aria-label="Tìm kiếm trong bảng chi tiết"
          style={{
            border: '1px solid #e3e8f2',
            background: '#f3f6fb',
            borderRadius: 8,
            padding: '6px 12px',
            fontSize: 12.5,
            color: '#0f1b3d',
            outline: 'none',
            width: 200,
          }}
        />
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
                <td colSpan={5} style={{ padding: '24px', textAlign: 'center', color: '#6a7491' }}>
                  Không tìm thấy kết quả phù hợp
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
