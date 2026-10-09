"use client";

import React, { useMemo } from "react";
import { useParkingStore } from "../../../../store/useParkingStore";
import { useRouter } from "next/navigation";

export default function ParkingLotsPage() {
  const { slots } = useParkingStore();
  const router = useRouter();

  const occ = Object.values(slots).filter(s => s.status === 'occupied' || s.status === 'assigned').length;
  const total = Math.max(1, Object.keys(slots).length);
  const pc = Math.round((occ / total) * 100) || 0;

  const L = [
    ['Bãi đỗ xe Vincom Đồng Khởi', '72 Lê Thánh Tôn, Q.1', 3, total, 'ok', pc],
    ['Bãi xe Nguyễn Huệ', '25 Nguyễn Huệ, Q.1', 2, 40, 'pe', 0],
    ['Bãi xe Pasteur', '12 Pasteur, Q.3', 4, 96, 'rj', 0]
  ];
  const A: any = {
    ok: ['Đã duyệt', '#16a36a'],
    pe: ['Chờ duyệt', '#f2a007'],
    rj: ['Bị từ chối', '#e5484d']
  };

  return (
    <>
      <div className="head rowb">
        <div>
          <h2>Bãi của tôi</h2>
          <p className="mut" style={{ margin: 0 }}>Quản lý nhiều bãi, mỗi bãi nhiều tầng</p>
        </div>
        <button className="btn p" onClick={() => router.push('/owner/parking-lots/new')}>Thêm bãi mới</button>
      </div>
      <div className="lots">
        {L.map((l: any, i) => (
          <div key={i} className="card lot">
            <div className="top">
              <span className="tag" style={{ '--c': A[l[4]][1] } as any}>{A[l[4]][0]}</span>
            </div>
            <h3>{l[0]}</h3>
            <p className="mut" style={{ margin: 0 }}>{l[1]}</p>
            <p style={{ margin: '8px 0 0' }}>{l[2]} tầng · {l[3]} ô</p>
            <div className="bar"><i style={{ width: `${l[4] === 'ok' ? l[5] : 0}%` }}></i></div>
            <p className="mut" style={{ margin: '0 0 12px' }}>
              {l[4] === 'ok' ? `${l[5]}% đang sử dụng` : l[4] === 'rj' ? 'Lý do: ảnh sơ đồ chưa khớp địa chỉ, cần cập nhật GPS.' : 'Đang chờ quản trị viên xét duyệt'}
            </p>
            <button className="btn" onClick={() => router.push('/owner/dashboard/map')}>Xem sơ đồ</button>{" "}
            <button className="btn">Chỉnh sửa</button>
          </div>
        ))}
      </div>
    </>
  );
}
