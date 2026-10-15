'use client';

import React from 'react';
import { useRouter } from 'next/navigation';
import { OwnerPageHeader as PageHeader } from '../../../../components/layout/OwnerPageHeader';
import { useOwnerStore } from '../../../../store/useOwnerStore';
import { KIND, p2, ST, Floor } from '../../../../types/owner';

export default function SessionsPage() {
  const router = useRouter();
  const spots = useOwnerStore((s) => s.spots);
  const selectSpot = useOwnerStore((s) => s.selectSpot);
  const setActiveFloor = useOwnerStore((s) => s.setActiveFloor);

  const occupiedSpots = spots.filter((s) => s.s === 'occupied').slice(0, 40);

  const handleViewSpot = (spotId: string, floor: Floor) => {
    selectSpot(spotId);
    setActiveFloor(floor);
    router.push('/owner/layout');
  };

  return (
    <>
      <PageHeader
        breadcrumb="/owner/sessions"
        title="Phiên đỗ xe"
        subtitle="Các xe đang đỗ trong bãi"
      />

      <div className="card">
        <div className="tw" id="stb">
          <table>
            <thead>
              <tr>
                <th>Biển số</th>
                <th>Loại xe</th>
                <th>Giờ vào</th>
                <th>Thời gian đỗ</th>
                <th>Ô được gán</th>
                <th>Trạng thái</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {occupiedSpots.map((s) => {
                const entryTime = `${p2(7 + (s.n % 4))}:${p2(10 + s.n * 5)}`;
                const duration = `${1 + (s.n % 4)} giờ ${p2(s.n * 6)} phút`;
                const statusInfo = ST.occupied;

                return (
                  <tr key={s.id}>
                    <td>{s.p}</td>
                    <td>Ô tô · {KIND[s.i % 9]}</td>
                    <td>{entryTime}</td>
                    <td>{duration}</td>
                    <td>{s.id}</td>
                    <td>
                      <span
                        className="tag"
                        style={{ '--c': statusInfo[1] } as React.CSSProperties}
                      >
                        {statusInfo[0]}
                      </span>
                    </td>
                    <td>
                      <button
                        className="btn sm"
                        onClick={() => handleViewSpot(s.id, s.f)}
                      >
                        Xem ô
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
}
