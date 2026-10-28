'use client';

import React, { useState, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useOwnerStore } from '@/store/useOwnerStore';
import { ExcelExportButton } from '@/components/common/ExcelExportButton';
import { Floor, FLOORS } from '@/types/owner';
import { cancelParkingSessionApi } from '@/features/owner/api/ownerApi';

interface ParkingSession {
  id: string;
  plate: string;
  slot: string;
  floor: Floor;
  entryTime: string;
  duration: string;
  method: string;
  status: 'Đang đỗ' | 'Đã ra';
}

const PAYMENT_METHODS = ['Ví Smart Parking', 'VNPay QR', 'Tiền mặt'];

export function SessionsTableV2() {
  const router = useRouter();
  const spots = useOwnerStore((s) => s.spots);
  const selectSpot = useOwnerStore((s) => s.selectSpot);
  const setActiveFloor = useOwnerStore((s) => s.setActiveFloor);
  const [query, setQuery] = useState('');
  const clearQuery = () => setQuery('');

  const [floorFilter, setFloorFilter] = useState<'all' | Floor>('all');
  const [methodFilter, setMethodFilter] = useState<string>('all');
  const [page, setPage] = useState(1);
  const pageSize = 10;

  // Session cancellation state (US-PARK-003)
  const [cancellingSession, setCancellingSession] = useState<ParkingSession | null>(null);
  const [cancelReason, setCancelReason] = useState('Xe không vào sau khi mở barie');
  const [isCancelling, setIsCancelling] = useState(false);
  const [cancelledSessionIds, setCancelledSessionIds] = useState<Set<string>>(new Set());

  const handleConfirmCancel = async () => {
    if (!cancellingSession) return;
    setIsCancelling(true);
    try {
      await cancelParkingSessionApi(cancellingSession.id, cancelReason);
    } catch (err) {
      console.warn('Backend API cancel session call offline/fallback:', err);
    } finally {
      useOwnerStore.getState().updateSpotStatus(cancellingSession.slot, 'available');
      setCancelledSessionIds((prev) => new Set(prev).add(cancellingSession.id));
      setIsCancelling(false);
      setCancellingSession(null);
    }
  };

  // Build sessions list from active occupied spots and mock recent checkout sessions
  const sessions: ParkingSession[] = useMemo(() => {
    const list: ParkingSession[] = [];

    // Occupied spots currently in the parking lot
    spots
      .filter((s) => s.s === 'occupied')
      .forEach((s, idx) => {
        const hoursAgo = (idx * 17) % 6;
        const minsAgo = (idx * 23) % 60;
        list.push({
          id: `SES-IN-${s.id}`,
          plate: s.p || `51A-${100 + idx * 7}.89`,
          slot: s.id,
          floor: s.f,
          entryTime: `${String(14 - hoursAgo).padStart(2, '0')}:${String(minsAgo).padStart(2, '0')}`,
          duration: `${hoursAgo} giờ ${minsAgo} phút`,
          method: idx % 2 === 0 ? 'Ví Smart Parking' : idx % 3 === 0 ? 'VNPay QR' : 'Tiền mặt',
          status: 'Đang đỗ',
        });
      });

    // Mock completed sessions
    const completedPlates = [
      '51A-455.34', '51A-573.12', '51A-691.80', '60A-882.19', '72A-341.05',
      '51H-992.41', '59X-441.22', '30H-119.82', '50Z-661.34', '51F-774.90'
    ];
    completedPlates.forEach((p, idx) => {
      const fl = FLOORS[idx % FLOORS.length];
      list.push({
        id: `SES-OUT-${idx + 1}`,
        plate: p,
        slot: `${fl}-B${idx + 1}`,
        floor: fl,
        entryTime: `0${8 + (idx % 4)}:${String(10 + idx * 4).padStart(2, '0')}`,
        duration: '1 giờ 45 phút',
        method: idx % 2 === 0 ? 'Ví Smart Parking' : 'VNPay QR',
        status: 'Đã ra',
      });
    });

    return list;
  }, [spots]);

  // Filter sessions using header search, floor, and payment method
  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return sessions.filter((s) => {
      const matchesSearch =
        !q ||
        s.plate.toLowerCase().includes(q) ||
        s.slot.toLowerCase().includes(q) ||
        s.id.toLowerCase().includes(q);
      const matchesFloor = floorFilter === 'all' || s.floor === floorFilter;
      const matchesMethod = methodFilter === 'all' || s.method === methodFilter;
      return matchesSearch && matchesFloor && matchesMethod;
    });
  }, [sessions, query, floorFilter, methodFilter]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const currentRows = filtered.slice((page - 1) * pageSize, page * pageSize);

  const handleClearFilters = () => {
    clearQuery();
    setFloorFilter('all');
    setMethodFilter('all');
    setPage(1);
  };

  // Navigate to 2D plan and highlight the spot
  const handleViewSpot = (spotId: string, floor: Floor) => {
    selectSpot(spotId);
    setActiveFloor(floor);
    router.push('/owner/layout/2d');
  };

  const excelData = useMemo(() => {
    return [
      ['DANH SÁCH PHIÊN ĐỖ XE – SMART PARKING SYSTEM'],
      ['Tổng số phiên:', filtered.length],
      [],
      ['Mã phiên', 'Biển số', 'Vị trí ô', 'Tầng', 'Giờ vào', 'Thời gian đỗ', 'Phương thức', 'Trạng thái'],
      ...filtered.map((s) => [s.id, s.plate, s.slot, s.floor, s.entryTime, s.duration, s.method, s.status]),
    ];
  }, [filtered]);

  const hasActiveFilters = Boolean(query || floorFilter !== 'all' || methodFilter !== 'all');

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
      {/* Top Filter Bar: Floor and Payment dropdowns on left, Clear & Excel on right */}
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
          {/* Floor filter dropdown */}
          <select
            value={floorFilter}
            onChange={(e) => {
              setFloorFilter(e.target.value as typeof floorFilter);
              setPage(1);
            }}
            aria-label="Lọc theo tầng"
            style={{
              border: '1px solid #e3e8f2',
              background: '#fff',
              borderRadius: 10,
              padding: '8px 14px',
              fontSize: 13,
              color: '#0f1b3d',
              outline: 'none',
              fontWeight: 600,
              minHeight: 38,
            }}
          >
            <option value="all">Tất cả tầng</option>
            {FLOORS.map((f) => (
              <option key={f} value={f}>
                Tầng {f}
              </option>
            ))}
          </select>

          {/* Payment method dropdown */}
          <select
            value={methodFilter}
            onChange={(e) => {
              setMethodFilter(e.target.value);
              setPage(1);
            }}
            aria-label="Lọc theo phương thức thanh toán"
            style={{
              border: '1px solid #e3e8f2',
              background: '#fff',
              borderRadius: 10,
              padding: '8px 14px',
              fontSize: 13,
              color: '#0f1b3d',
              outline: 'none',
              fontWeight: 600,
              minHeight: 38,
            }}
          >
            <option value="all">Tất cả thanh toán</option>
            {PAYMENT_METHODS.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </select>
        </div>

        {/* Right Buttons: Clear filter & Excel export */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          {hasActiveFilters && (
            <button
              type="button"
              onClick={handleClearFilters}
              aria-label="Xóa bộ lọc phiên đỗ xe"
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
            filename="danh-sach-phien-do-xe.xlsx"
            sheetName="Phiên đỗ xe"
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
                Biển số
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Vị trí ô
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Tầng
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Giờ vào
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Thời gian đỗ
              </th>
              <th style={{ padding: '12px 14px', fontSize: 12, color: '#6a7491', fontWeight: 600 }}>
                Phương thức
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
              currentRows.map((s) => (
                <tr
                  key={s.id}
                  style={{
                    borderBottom: '1px solid #f5f7fa',
                    fontSize: 13,
                    color: '#0f1b3d',
                  }}
                >
                  <td style={{ padding: '12px 14px', fontWeight: 700 }}>{s.plate}</td>
                  <td style={{ padding: '12px 14px' }}>
                    <span
                      style={{
                        background: '#f3f6fb',
                        padding: '3px 8px',
                        borderRadius: 6,
                        fontWeight: 600,
                      }}
                    >
                      {s.slot}
                    </span>
                  </td>
                  <td style={{ padding: '12px 14px', color: '#6a7491' }}>{s.floor}</td>
                  <td style={{ padding: '12px 14px' }}>{s.entryTime}</td>
                  <td style={{ padding: '12px 14px', color: '#6a7491' }}>{s.duration}</td>
                  <td style={{ padding: '12px 14px' }}>{s.method}</td>
                  <td style={{ padding: '12px 14px' }}>
                    {cancelledSessionIds.has(s.id) ? (
                      <span
                        style={{
                          background: '#f1f5f9',
                          color: '#64748b',
                          padding: '3px 10px',
                          borderRadius: 999,
                          fontSize: 11.5,
                          fontWeight: 700,
                        }}
                      >
                        Đã hủy
                      </span>
                    ) : (
                      <span
                        style={{
                          background: s.status === 'Đang đỗ' ? '#fde8e8' : '#def7ec',
                          color: s.status === 'Đang đỗ' ? '#e5484d' : '#0e9f6e',
                          padding: '3px 10px',
                          borderRadius: 999,
                          fontSize: 11.5,
                          fontWeight: 700,
                        }}
                      >
                        {s.status}
                      </span>
                    )}
                  </td>
                  <td style={{ padding: '12px 14px', textAlign: 'right' }}>
                    <div style={{ display: 'inline-flex', gap: 6 }}>
                      <button
                        type="button"
                        onClick={() => handleViewSpot(s.slot, s.floor)}
                        style={{
                          border: '1px solid #e3e8f2',
                          background: '#fff',
                          borderRadius: 8,
                          padding: '5px 10px',
                          fontSize: 12,
                          fontWeight: 600,
                          color: '#2563eb',
                          cursor: 'pointer',
                        }}
                      >
                        Xem ô
                      </button>
                      {s.status === 'Đang đỗ' && !cancelledSessionIds.has(s.id) && (
                        <button
                          type="button"
                          onClick={() => setCancellingSession(s)}
                          style={{
                            border: '1px solid #fecaca',
                            background: '#fff',
                            borderRadius: 8,
                            padding: '5px 10px',
                            fontSize: 12,
                            fontWeight: 600,
                            color: '#dc2626',
                            cursor: 'pointer',
                          }}
                        >
                          Hủy phiên
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={8} style={{ padding: '36px 16px', textAlign: 'center' }}>
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

      {/* Session Cancellation Modal (US-PARK-003) */}
      {cancellingSession && (
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
              width: '100%',
              maxWidth: 440,
              boxShadow: '0 20px 40px rgba(15,27,61,0.2)',
            }}
          >
            <h3 style={{ margin: '0 0 10px', fontSize: 16, fontWeight: 700, color: '#0f1b3d' }}>
              Xác nhận hủy phiên đỗ xe
            </h3>
            <p style={{ fontSize: 13, color: '#6a7491', margin: '0 0 14px' }}>
              Phiên đỗ của xe <strong>{cancellingSession.plate}</strong> tại ô <strong>{cancellingSession.slot}</strong> sẽ được hủy bỏ và ô đỗ sẽ chuyển về trạng thái Trống.
            </p>
            <div style={{ marginBottom: 16 }}>
              <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: '#0f1b3d', marginBottom: 4 }}>
                Lý do hủy phiên (Bắt buộc theo chuẩn BE)
              </label>
              <input
                type="text"
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                style={{
                  width: '100%',
                  border: '1px solid #e3e8f2',
                  borderRadius: 8,
                  padding: '8px 12px',
                  fontSize: 13,
                  outline: 'none',
                }}
              />
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <button
                type="button"
                onClick={() => setCancellingSession(null)}
                disabled={isCancelling}
                style={{
                  background: '#f3f6fb',
                  border: '1px solid #e3e8f2',
                  borderRadius: 8,
                  padding: '7px 14px',
                  fontSize: 13,
                  fontWeight: 600,
                  cursor: 'pointer',
                  color: '#6a7491',
                }}
              >
                Đóng
              </button>
              <button
                type="button"
                onClick={handleConfirmCancel}
                disabled={isCancelling || !cancelReason.trim()}
                style={{
                  background: isCancelling ? '#fca5a5' : '#dc2626',
                  border: 'none',
                  borderRadius: 8,
                  padding: '7px 16px',
                  fontSize: 13,
                  fontWeight: 700,
                  cursor: isCancelling ? 'not-allowed' : 'pointer',
                  color: '#fff',
                }}
              >
                {isCancelling ? 'Đang hủy...' : 'Xác nhận hủy'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
