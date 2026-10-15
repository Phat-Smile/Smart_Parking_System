'use client';

import React from 'react';
import { OwnerPageHeader as PageHeader } from '../../../../components/layout/OwnerPageHeader';
import { useOwnerStore } from '../../../../store/useOwnerStore';
import { RC } from '../../../../types/owner';

export default function ReservationsPage() {
  const reservations = useOwnerStore((s) => s.reservations);
  const search = useOwnerStore((s) => s.reservationSearch);
  const setSearch = useOwnerStore((s) => s.setReservationSearch);

  const q = search.q.toLowerCase();
  const filtered = reservations.filter((r) => {
    const matchesStatus = !search.s || r.status === search.s;
    const matchesQuery =
      !q ||
      r.id.toLowerCase().includes(q) ||
      r.plate.toLowerCase().includes(q) ||
      r.slot.toLowerCase().includes(q);
    return matchesStatus && matchesQuery;
  });

  return (
    <>
      <PageHeader
        breadcrumb="/owner/reservations"
        title="Đặt chỗ"
        subtitle="Giữ chỗ và đặt trước tại bãi của bạn"
      />

      <div className="card">
        <div className="wh">
          <input
            className="sr"
            style={{ maxWidth: '280px' }}
            placeholder="Tìm biển số, mã, ô…"
            value={search.q}
            onChange={(e) => setSearch({ q: e.target.value })}
          />
          <select
            className="sr"
            style={{ maxWidth: '200px' }}
            value={search.s}
            onChange={(e) => setSearch({ s: e.target.value })}
          >
            <option value="">Tất cả trạng thái</option>
            {Object.keys(RC).map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
        </div>

        {filtered.length > 0 ? (
          <div className="tw" id="rtb">
            <table>
              <thead>
                <tr>
                  <th>Mã</th>
                  <th>Loại</th>
                  <th>Xe</th>
                  <th>Bãi / Ô</th>
                  <th>Thời gian</th>
                  <th>Trạng thái</th>
                  <th>Thanh toán</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((r) => (
                  <tr key={r.id}>
                    <td>{r.id}</td>
                    <td>{r.type}</td>
                    <td>{r.plate}</td>
                    <td>Vincom · {r.slot}</td>
                    <td>{r.time}</td>
                    <td>
                      <span
                        className="tag"
                        style={{ '--c': RC[r.status] } as React.CSSProperties}
                      >
                        {r.status}
                      </span>
                    </td>
                    <td>
                      <span
                        className="tag"
                        style={
                          {
                            '--c': r.paid ? '#1a7f37' : '#b7791f',
                          } as React.CSSProperties
                        }
                      >
                        {r.paid ? 'Đã thanh toán' : 'Chưa thanh toán'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="mut" style={{ margin: '14px 0 0' }}>
            Không có đặt chỗ phù hợp.
          </p>
        )}
      </div>
    </>
  );
}
