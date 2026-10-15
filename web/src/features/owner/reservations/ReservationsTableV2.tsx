'use client';

import React, { useState, useMemo } from 'react';
import { useOwnerStore } from '@/store/useOwnerStore';
import { ExcelExportButton } from '@/components/common/ExcelExportButton';
import { Reservation, RC } from '@/types/owner';

export function ReservationsTableV2() {
  const storeReservations = useOwnerStore((s) => s.reservations);

  // Local state overlay for reservations so user can cancel reservations interactively
  const [reservations, setReservations] = useState<Reservation[]>(storeReservations);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [page, setPage] = useState(1);
  const pageSize = 10;

  // Cancel reservation confirm modal state
  const [cancelingReservation, setCancelingReservation] = useState<Reservation | null>(null);

  // Filter reservations
  const filtered = useMemo(() => {
    return reservations.filter((r) => {
      const matchesSearch =
        !searchQuery ||
        r.id.toLowerCase().includes(searchQuery.toLowerCase()) ||
        r.plate.toLowerCase().includes(searchQuery.toLowerCase()) ||
        r.slot.toLowerCase().includes(searchQuery.toLowerCase());
      const matchesStatus = statusFilter === 'all' || r.status === statusFilter;
      return matchesSearch && matchesStatus;
    });
  }, [reservations, searchQuery, statusFilter]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const currentRows = filtered.slice((page - 1) * pageSize, page * pageSize);

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
      {/* Top Controls */}
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
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
          {/* Search box */}
          <input
            type="text"
            placeholder="Tìm mã, biển số, ô đỗ..."
            value={searchQuery}
            onChange={(e) => {
              setSearchQuery(e.target.value);
              setPage(1);
            }}
            aria-label="Tìm kiếm đặt chỗ"
            style={{
              border: '1px solid #e3e8f2',
              background: '#f3f6fb',
              borderRadius: 10,
              padding: '8px 14px',
              fontSize: 13,
              color: '#0f1b3d',
              outline: 'none',
              width: 240,
            }}
          />

          {/* Status filter dropdown */}
          <select
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              setPage(1);
            }}
            aria-label="Lọc theo trạng thái đặt chỗ"
            style={{
              border: '1px solid #e3e8f2',
              background: '#fff',
              borderRadius: 10,
              padding: '8px 12px',
              fontSize: 13,
              color: '#0f1b3d',
              outline: 'none',
              fontWeight: 600,
            }}
          >
            <option value="all">Tất cả trạng thái</option>
            <option value="Đã xác nhận">Đã xác nhận</option>
            <option value="Đang giữ">Đang giữ</option>
            <option value="Chờ thanh toán">Chờ thanh toán</option>
            <option value="Hết hạn">Hết hạn</option>
            <option value="Đã hủy">Đã hủy</option>
          </select>
        </div>

        {/* Excel Export Button */}
        <ExcelExportButton
          filename="danh-sach-dat-cho.xlsx"
          sheetName="Đặt chỗ"
          data={excelData}
          title="Xuất Excel"
        />
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
                <td colSpan={7} style={{ padding: '28px', textAlign: 'center', color: '#6a7491' }}>
                  Không tìm thấy đặt chỗ nào phù hợp
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
