"use client";

import React from "react";
import { useParkingStore } from "../../../../store/useParkingStore";

const p2 = (n: number) => String(n).padStart(2, '0');
const tg = (s: string) => <span className="tag" style={{ '--c': '#e5484d' } as any}>Có xe</span>;

export default function SessionsPage() {
  const { slots } = useParkingStore();
  const occ = Object.values(slots).filter(s => s.status === 'occupied').slice(0, 9);

  return (
    <>
      <div className="head">
        <h2>Phiên đỗ xe</h2>
        <p className="mut" style={{ margin: 0 }}>Các xe đang đỗ trong bãi</p>
      </div>
      <div className="card">
        <div className="tw">
          <table>
            <thead>
              <tr>
                {['Xe','Giờ vào','Ô được gán','Trạng thái'].map(x => <th key={x}>{x}</th>)}
              </tr>
            </thead>
            <tbody>
              {occ.map(s => (
                <tr key={s.id}>
                  <td>{s.vehicle?.plate || '51A-123.45'}</td>
                  <td>{'0' + (7 + s.index % 3) + ':' + p2(10 + s.index * 5)}</td>
                  <td>{s.id}</td>
                  <td>{tg('occupied')}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
}
