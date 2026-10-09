"use client";

import React from "react";

const chip = (t: string, c: string) => (
  <span className="tag" style={{ '--c': c } as any}>{t}</span>
);

export default function ReservationsPage() {
  const R = [
    ['R-1042','51A-123.45','T1-A3','14:00 – 16:00','Đặt trước','Đã xác nhận','#16a36a',1],
    ['R-1043','59X1-678.90','T2-B5','14:30 – 15:00','Giữ chỗ','Đang giữ','#2f80ed',0],
    ['R-1044','30H-555.12','T1-B2','17:00 – 19:00','Đặt trước','Đã xác nhận','#16a36a',1],
    ['R-1045','51F-222.33','T3-A8','18:00 – 20:00','Đặt trước','Chờ thanh toán','#f2a007',0],
    ['R-1039','72A-111.22','T2-A1','09:00 – 10:00','Giữ chỗ','Hết hạn','#6a7491',0]
  ];

  return (
    <>
      <div className="head">
        <h2>Đặt chỗ</h2>
        <p className="mut" style={{ margin: 0 }}>Giữ chỗ và đặt trước tại bãi của bạn</p>
      </div>
      <div className="card">
        <div className="tw">
          <table>
            <thead>
              <tr>
                {['Mã','Xe','Ô','Thời gian','Loại','Trạng thái','Thanh toán'].map(x => <th key={x}>{x}</th>)}
              </tr>
            </thead>
            <tbody>
              {R.map((r, i) => (
                <tr key={i}>
                  <td>{r[0]}</td>
                  <td>{r[1]}</td>
                  <td>{r[2]}</td>
                  <td>{r[3]}</td>
                  <td>{r[4]}</td>
                  <td>{chip(r[5] as string, r[6] as string)}</td>
                  <td>{chip(r[7] ? 'Đã thanh toán' : 'Chưa thanh toán', r[7] ? '#16a36a' : '#f2a007')}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
}
