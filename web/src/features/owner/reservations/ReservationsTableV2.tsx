'use client';

import React, { useState, useMemo } from 'react';
import { useOwnerStore } from '@/store/useOwnerStore';
import { useOwnerSearchStore } from '@/features/owner/search/useOwnerSearchStore';
import { ExcelExportButton } from '@/components/common/ExcelExportButton';
import { Reservation, RC } from '@/types/owner';

const STATUS_CHIPS = [
  { key: 'all', label: 'Tất cả' },
  { key: 'Đã xác nhận', label: 'Đã xác nhận' },
  { key: 'Đang giữ', label: 'Đang giữ' },
  { key: 'Chờ thanh toán', label: 'Chờ thanh toán' },
  { key: 'Hết hạn', label: 'Hết hạn' },
  { key: 'Đã hủy', label: 'Đã hủy' },
];

export function ReservationsTableV2() {
  const storeReservations = useOwnerStore((s) => s.reservations);
  const { query, clearQuery } = useOwnerSearchStore();

  const [reservations, setReservations] = useState<Reservation[]>(storeReservations);
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [page, setPage] = useState(1);
  const pageSize = 10;

  // Cancel reservation confirm modal state
  const [cancelingReservation, setCancelingReservation] = useState<Reservation | null>(null);

  // Filter reservations based on header search and status chips
  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return reservations.filter((r) => {
      const matchesSearch =
        !q ||
        r.id.toLowerCase().includes(q) ||
        r.plate.toLowerCase().includes(q) ||
        r.slot.toLowerCase().includes(q);
      const matchesStatus = statusFilter === 'all' || r.status === statusFilter;
      return matchesSearch && matchesStatus;
    });
  }, [reservations, query, statusFilter]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const currentRows = filtered.slice((page - 1) * pageSize, page * pageSize);

  const handleClearFilters = () => {
    clearQuery();
    setStatusFilter('all');
    setPage(1);
  };

  const handleConfirmCancel = () => {
    if (!cancelingReservation) return;
    setReservations((prev) =>
      prev.map((r) =>
        r.id === cancelingReservation.id ? { ...r, status: 'Đã hủy' } : r
      )
    );
    setCancelingReservation(null);
  };

  const excelData = useMemo(() => {
    return [
      ['DANH SÁCH ĐẶT CHỖ – SMART PARKING SYSTEM'],
      ['Tổng số đặt chỗ:', filtered.length],
      [],
      ['Mã đặt chỗ', 'Biển số', 'Vị trí ô', 'Loại đặt chỗ', 'Thời gian giữ', 'Trạng thái'],
      ...filtered.map((r) => [r.id, r.plate, r.slot, r.type, r.time, r.status]),
    ];
  }, [filtered]);

  const hasActiveFilters = Boolean(query || statusFilter !== 'all');

  return (
    <div
      className="ov2-card"
      style={{
        background: '#fff',
        borderRadius: 18,
        border: '1px solid #e3e8f2',
        padding: 24,
      }}
    >
      {/* Top Controls: Status chips on left, Clear filters & Excel export on right */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: 12,
          marginBottom: 20,
        }}
      >
        {/* Status Chips */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
          {STATUS_CHIPS.map((chip) => {
            const isActive = statusFilter === chip.key;
            return (
              <button
                key={chip.key}
                type="button"
                onClick={() => {
                  setStatusFilter(chip.key);
                  setPage(1);
                }}
                aria-pressed={isActive}
                style={{
                  border: isActive ? '1px solid #2563eb' : '1px solid #e3e8f2',
                  background: isActive ? '#eff6ff' : '#f8faff',
                  color: isActive ? '#2563eb' : '#475569',
                  borderRadius: 999,
                  padding: '6px 14px',
                  fontSize: 12.5,
                  fontWeight: isActive ? 700 : 500,
                  cursor: 'pointer',
                  transition: 'all 0.15s ease',
                  minHeight: 36,
                }}
              >
                {chip.label}
              </button>
            );
          })}
        </div>

        {/* Right Action Buttons */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          {hasActiveFilters && (
            <button
              type="button"
              onClick={handleClearFilters}
              aria-label="Xóa bộ lọc đặt chỗ"
              style={{
                border: '1px solid #e2e8f0',
                background: '#fff',
                color: '#64748b',
                borderRadius: 10,
                padding: '8px 14px',
                fontSize: 12.5,
                fontWeight: 600,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                minHeight: 38,
                transition: 'all 0.15s ease',
              }}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
              Xóa bộ lọc
            </button>
          )}

          {/* Excel Export Button */}
          <ExcelExportButton
            filename="danh-sach-dat-cho.xlsx"
            sheetName="Đặt chỗ"
            data={excelData}
            title="Xuất Excel"
          />
        </div>
      </div>

      {/* Table */}
      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
          <thead>
            <tr style={{ borderBottom: '1px solid #e3e8f2' }}>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Mã đặt chỗ
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Biển số
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Vị trí ô
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Loại đặt chỗ
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Thời gian giữ
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Trạng thái
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600, textAlign: 'right' }}>
                Thao tác
              </th>
            </tr>
          </thead>
          <tbody>
            {currentRows.length > 0 ? (
              currentRows.map((r) => {
                const color = RC[r.status] || '#6a7491';
                const canCancel = r.status !== 'Đã hủy' && r.status !== 'Hết hạn';

                return (
                  <tr
                    key={r.id}
                    style={{
                      borderBottom: '1px solid #f5f7fa',
                      fontSize: 13,
                      color: '#0f1b3d',
                    }}
                  >
                    <td style={{ padding: '12px 14px', fontWeight: 700 }}>{r.id}</td>
                    <td style={{ padding: '12px 14px', fontWeight: 600 }}>{r.plate}</td>
                    <td style={{ padding: '12px 14px' }}>
                      <span
                        style={{
                          background: '#f3f6fb',
                          padding: '3px 8px',
                          borderRadius: 6,
                          fontWeight: 600,
                        }}
                      >
                        {r.slot}
                      </span>
                    </td>
                    <td style={{ padding: '12px 14px', color: '#6a7491' }}>{r.type}</td>
                    <td style={{ padding: '12px 14px' }}>{r.time}</td>
                    <td style={{ padding: '12px 14px' }}>
                      <span
                        style={{
                          color,
                          background: `${color}18`,
                          padding: '3px 10px',
                          borderRadius: 999,
                          fontSize: 11.5,
                          fontWeight: 700,
                        }}
                      >
                        {r.status}
                      </span>
                    </td>
                    <td style={{ padding: '12px 14px', textAlign: 'right' }}>
                      {canCancel && (
                        <button
                          type="button"
                          onClick={() => setCancelingReservation(r)}
                          style={{
                            border: '1px solid #fde8e8',
                            background: '#fff',
                            borderRadius: 8,
                            padding: '4px 10px',
                            fontSize: 12,
                            fontWeight: 600,
                            color: '#e5484d',
                            cursor: 'pointer',
                          }}
                        >
                          Hủy đặt
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })
            ) : (
              <tr>
                <td colSpan={7} style={{ padding: '36px 16px', textAlign: 'center' }}>
                  <div style={{ color: '#6a7491', fontSize: 14, fontWeight: 500, marginBottom: 12 }}>
                    Không tìm thấy kết quả
                  </div>
                  {hasActiveFilters && (
                    <button
                      type="button"
                      onClick={handleClearFilters}
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
            marginTop: 20,
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
              padding: '5px 12px',
              fontSize: 12,
              cursor: page <= 1 ? 'not-allowed' : 'pointer',
              opacity: page <= 1 ? 0.5 : 1,
            }}
          >
            Trước
          </button>
          <span style={{ fontSize: 12.5, color: '#6a7491' }}>
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
              padding: '5px 12px',
              fontSize: 12,
              cursor: page >= totalPages ? 'not-allowed' : 'pointer',
              opacity: page >= totalPages ? 0.5 : 1,
            }}
          >
            Sau
          </button>
        </div>
      )}

      {/* Cancel Confirmation Modal */}
      {cancelingReservation && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(15,27,61,0.4)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
            padding: 16,
          }}
          role="dialog"
          aria-modal="true"
        >
          <div
            style={{
              background: '#fff',
              borderRadius: 18,
              padding: 24,
              maxWidth: 400,
              width: '100%',
              boxShadow: '0 20px 40px rgba(15,27,61,0.2)',
            }}
          >
            <h3 style={{ margin: '0 0 10px', fontSize: 16, fontWeight: 800, color: '#0f1b3d' }}>
              Xác nhận hủy đặt chỗ
            </h3>
            <p style={{ margin: '0 0 20px', fontSize: 13.5, color: '#6a7491', lineHeight: 1.5 }}>
              Bạn có chắc chắn muốn hủy đặt chỗ <strong>{cancelingReservation.id}</strong> (biển số {cancelingReservation.plate}, ô {cancelingReservation.slot})?
            </p>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <button
                type="button"
                onClick={() => setCancelingReservation(null)}
                style={{
                  border: '1px solid #e3e8f2',
                  background: '#f3f6fb',
                  borderRadius: 10,
                  padding: '8px 16px',
                  fontSize: 13,
                  fontWeight: 600,
                  cursor: 'pointer',
                  color: '#6a7491',
                }}
              >
                Không hủy
              </button>
              <button
                type="button"
                onClick={handleConfirmCancel}
                style={{
                  border: 'none',
                  background: '#e5484d',
                  borderRadius: 10,
                  padding: '8px 16px',
                  fontSize: 13,
                  fontWeight: 700,
                  cursor: 'pointer',
                  color: '#fff',
                }}
              >
                Xác nhận hủy
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
