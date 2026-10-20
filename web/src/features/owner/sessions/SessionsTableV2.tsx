'use client';

import React, { useState, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useOwnerStore } from '@/store/useOwnerStore';
import { ExcelExportButton } from '@/components/common/ExcelExportButton';
import { Floor, FLOORS } from '@/types/owner';

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

export function SessionsTableV2() {
  const router = useRouter();
  const spots = useOwnerStore((s) => s.spots);
  const selectSpot = useOwnerStore((s) => s.selectSpot);
  const setActiveFloor = useOwnerStore((s) => s.setActiveFloor);

  const [searchQuery, setSearchQuery] = useState('');
  const [floorFilter, setFloorFilter] = useState<'all' | Floor>('all');
  const [statusFilter, setStatusFilter] = useState<'all' | 'Đang đỗ' | 'Đã ra'>('all');
  const [page, setPage] = useState(1);
  const pageSize = 10;

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

  // Filter sessions
  const filtered = useMemo(() => {
    return sessions.filter((s) => {
      const matchesSearch =
        !searchQuery ||
        s.plate.toLowerCase().includes(searchQuery.toLowerCase()) ||
        s.slot.toLowerCase().includes(searchQuery.toLowerCase());
      const matchesFloor = floorFilter === 'all' || s.floor === floorFilter;
      const matchesStatus = statusFilter === 'all' || s.status === statusFilter;
      return matchesSearch && matchesFloor && matchesStatus;
    });
  }, [sessions, searchQuery, floorFilter, statusFilter]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const currentRows = filtered.slice((page - 1) * pageSize, page * pageSize);

  // Navigate to 2D plan and highlight the spot
  const handleViewSpot = (spotId: string, floor: Floor) => {
    selectSpot(spotId);
    setActiveFloor(floor);
    router.push('/owner/layout/2d');
  };

  // Excel data export
  const excelData = useMemo(() => {
    return [
      ['DANH SÁCH PHIÊN ĐỖ XE – SMART PARKING SYSTEM'],
      ['Tổng số phiên:', filtered.length],
      [],
      ['Mã phiên', 'Biển số', 'Vị trí ô', 'Tầng', 'Giờ vào', 'Thời gian đỗ', 'Phương thức', 'Trạng thái'],
      ...filtered.map((s) => [s.id, s.plate, s.slot, s.floor, s.entryTime, s.duration, s.method, s.status]),
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
      {/* Top Filter and Search Bar */}
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
            placeholder="Tìm biển số hoặc vị trí ô..."
            value={searchQuery}
            onChange={(e) => {
              setSearchQuery(e.target.value);
              setPage(1);
            }}
            aria-label="Tìm kiếm phiên đỗ"
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
              padding: '8px 12px',
              fontSize: 13,
              color: '#0f1b3d',
              outline: 'none',
              fontWeight: 600,
            }}
          >
            <option value="all">Tất cả tầng</option>
            {FLOORS.map((f) => (
              <option key={f} value={f}>
                Tầng {f}
              </option>
            ))}
          </select>

          {/* Status filter dropdown */}
          <select
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value as typeof statusFilter);
              setPage(1);
            }}
            aria-label="Lọc theo trạng thái"
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
            <option value="Đang đỗ">Đang đỗ</option>
            <option value="Đã ra">Đã ra</option>
          </select>
        </div>

        {/* Excel Export Button */}
        <ExcelExportButton
          filename="danh-sach-phien-do-xe.xlsx"
          sheetName="Phiên đỗ xe"
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
                  </td>
                  <td style={{ padding: '12px 14px', textAlign: 'right' }}>
                    <button
                      type="button"
                      onClick={() => handleViewSpot(s.slot, s.floor)}
                      style={{
                        border: '1px solid #e3e8f2',
                        background: '#fff',
                        borderRadius: 8,
                        padding: '5px 12px',
                        fontSize: 12,
                        fontWeight: 600,
                        color: '#2563eb',
                        cursor: 'pointer',
                      }}
                    >
                      Xem ô
                    </button>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={8} style={{ padding: '28px', textAlign: 'center', color: '#6a7491' }}>
                  Không tìm thấy phiên đỗ xe nào phù hợp
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
    </div>
  );
}
