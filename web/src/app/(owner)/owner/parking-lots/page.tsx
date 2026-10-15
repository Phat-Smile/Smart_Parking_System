'use client';

import React from 'react';
import Link from 'next/link';
import { OwnerPageHeader as PageHeader } from '../../../../components/layout/OwnerPageHeader';
import { useOwnerStore, getSpotCounts } from '../../../../store/useOwnerStore';
import { AP, HC } from '../../../../types/owner';

export default function ParkingLotsPage() {
  const lots = useOwnerStore((s) => s.lots);
  const spots = useOwnerStore((s) => s.spots);
  const counts = getSpotCounts(spots);

  const occPct = Math.round(((counts.occupied + counts.reserved) / (spots.length || 500)) * 100);

  return (
    <>
      <PageHeader
        breadcrumb="/owner/parking-lots"
        title="Bãi của tôi"
        subtitle="Danh sách bãi, sức chứa và trạng thái duyệt"
        actions={
          <Link href="/owner/parking-lots/new" className="btn p">
            + Thêm bãi mới
          </Link>
        }
      />

      <div className="lots">
        {lots.map((l) => {
          const cap = l.cc + l.cm;
          const u = l.ap === 'approved' ? occPct : 0;
          const h1 = HC[l.ap][0];
          const h2 = HC[l.ap][1];
          const apInfo = AP[l.ap];

          return (
            <div key={l.id} className="card lot">
              <div
                className="top2"
                style={{ '--h1': h1, '--h2': h2 } as React.CSSProperties}
              >
                <span
                  className="tag"
                  style={{ '--c': apInfo[1] } as React.CSSProperties}
                >
                  {apInfo[0]}
                </span>
              </div>
              <h3 style={{ margin: 0 }}>{l.name}</h3>
              <p className="mut" style={{ margin: 0 }}>
                {l.addr}
              </p>
              <p style={{ margin: '8px 0 0' }}>
                {l.fls} tầng · {cap} chỗ · {l.op} – {l.cl2}
              </p>
              <div className="bar">
                <i style={{ width: `${u}%` }} />
              </div>
              <p className="mut" style={{ margin: '0 0 12px' }}>
                {l.ap === 'approved'
                  ? `${u}% đang sử dụng`
                  : l.ap === 'rejected'
                  ? `Lý do: ${l.why}`
                  : 'Chưa có dữ liệu vận hành'}
              </p>
              <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                <Link href={`/owner/parking-lots/${l.id}`} className="btn p">
                  Xem / Quản lý
                </Link>
                {l.ap === 'approved' && (
                  <Link href="/owner/layout" className="btn">
                    Sơ đồ
                  </Link>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </>
  );
}
