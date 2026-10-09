"use client";

import React, { useEffect, useState, useMemo } from "react";
import { useParkingStore } from "../../../../store/useParkingStore";
import { ParkingScene } from "../../../../components/viz/ParkingScene";
import { SlotStatus } from "../../../../domain/types";

// Prototype helpers
const p2 = (n: number) => String(n).padStart(2, '0');
const vnd = (n: number) => Math.round(n).toLocaleString('vi-VN') + ' đ';
const short = (n: number) => n >= 1e9 ? (n / 1e9).toFixed(2).replace('.', ',') + ' tỷ' : (n / 1e6).toFixed(1).replace('.', ',') + ' tr';
const hs = (s: string) => { let x = 7; for (let i = 0; i < s.length; i++) x = (x * 33 + s.charCodeAt(i)) % 10007; return x / 10007; };
const dayRev = (y: number, m: number, d: number) => { const w = new Date(y, m - 1, d).getDay(); return (3.2e6 + hs(y + '-' + m + '-' + d) * 2.4e6) * (w == 0 || w == 6 ? 1.3 : 1) * (1 + (y - 2025) * .1); };

const ST: Record<string, [string, string]> = {
  available: ['Trống', '#16a36a'],
  reserved: ['Đã đặt trước', '#2f80ed'],
  occupied: ['Có xe', '#e5484d'],
  maintenance: ['Bảo trì', '#f2a007'],
  verify: ['Cần xác minh', '#8a5cf6']
};
const K = Object.keys(ST);
const FL = ['T1', 'T2', 'T3'];

// Convert domain status
const mapStatus = (s: SlotStatus): string => {
  if (s === "available") return "available";
  if (s === "assigned") return "reserved";
  if (s === "occupied") return "occupied";
  if (s === "maintenance") return "maintenance";
  if (s === "needs_verification") return "verify";
  return "available";
};

function ChartLine({ id, vals, labels }: { id: string, vals: number[], labels: string[] }) {
  const W = 760, H = 250, p = 44;
  const mx = Math.max(...vals) * 1.1;
  const x = (i: number) => p + i * (W - p - 10) / (vals.length - 1);
  const y = (v: number) => H - 24 - v / mx * (H - 44);
  const ln = vals.map((v, i) => (i ? 'L' : 'M') + x(i).toFixed(1) + ' ' + y(v).toFixed(1)).join('');
  const st = Math.ceil(vals.length / 12);
  
  const [hoverIdx, setHoverIdx] = useState<number | null>(null);

  return (
    <svg 
      id={id} 
      viewBox={`0 0 ${W} ${H}`} 
      style={{ width: '100%', height: 'auto', touchAction: 'pan-y' }}
      onPointerMove={(e) => {
        const r = e.currentTarget.getBoundingClientRect();
        const px = (e.clientX - r.left) / r.width * W;
        let i = Math.round((px - 44) / ((W - 54) / (vals.length - 1)));
        i = Math.max(0, Math.min(vals.length - 1, i));
        setHoverIdx(i);
      }}
      onPointerLeave={() => setHoverIdx(null)}
    >
      <defs>
        <linearGradient id={`g${id}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#2457e6" stopOpacity=".28" />
          <stop offset="1" stopColor="#2457e6" stopOpacity="0" />
        </linearGradient>
      </defs>
      {[0, .25, .5, .75, 1].map((t, j) => (
        <g key={j}>
          <line x1={p} x2={W - 10} y1={y(mx * t)} y2={y(mx * t)} stroke="#e8edf6" />
          <text x={p - 6} y={y(mx * t) + 4} textAnchor="end" fontSize="10" fill="#6a7491">{short(mx * t)}</text>
        </g>
      ))}
      <path d={`${ln}L${x(vals.length - 1)} ${H - 24}L${p} ${H - 24}Z`} fill={`url(#g${id})`} />
      <path d={ln} fill="none" stroke="#2457e6" strokeWidth="2.5" strokeLinejoin="round" />
      {labels.map((l, i) => i % st ? null : (
        <text key={i} x={x(i)} y={H - 6} textAnchor="middle" fontSize="10" fill="#6a7491">{l}</text>
      ))}
      {hoverIdx !== null && (
        <g>
          <line x1={x(hoverIdx)} x2={x(hoverIdx)} y1="22" y2="226" stroke="#2457e6" strokeDasharray="3" />
          <circle cx={x(hoverIdx)} cy={y(vals[hoverIdx])} r="5" fill="#fff" stroke="#2457e6" strokeWidth="2.5" />
          <text x={Math.min(Math.max(x(hoverIdx), 70), W - 130)} y="14" textAnchor="middle" fontSize="12" fontWeight="700" fill="#0f1b3d">
            {labels[hoverIdx]}: {vnd(vals[hoverIdx])}
          </text>
        </g>
      )}
    </svg>
  );
}

export default function DashboardPage() {
  const { slots, activeFloor, setActiveFloor, selectedSlotId, selectSlot, updateSlotStatus } = useParkingStore();

  const counts = useMemo(() => {
    const c: Record<string, number> = {};
    K.forEach(k => c[k] = 0);
    Object.values(slots).forEach(s => {
      const ms = mapStatus(s.status);
      if (c[ms] !== undefined) c[ms]++;
    });
    return c;
  }, [slots]);

  // Simulate real-time updates as requested
  useEffect(() => {
    const i = setInterval(() => {
      const keys = Object.keys(slots);
      if(keys.length === 0) return;
      const sId = keys[Math.floor(Math.random() * keys.length)];
      const current = slots[sId];
      if (['available', 'assigned', 'occupied'].includes(current.status)) {
        const next = ['available', 'assigned', 'occupied'][Math.floor(Math.random() * 3)] as SlotStatus;
        updateSlotStatus(sId, next);
      }
    }, 5000);
    return () => clearInterval(i);
  }, [slots, updateSlotStatus]);

  const t = new Date();
  const revToday = dayRev(t.getFullYear(), t.getMonth() + 1, t.getDate());

  const rev7 = useMemo(() => {
    const v = [], l = [];
    for (let i = 6; i >= 0; i--) {
      const d = new Date(t.getFullYear(), t.getMonth(), t.getDate() - i);
      v.push(dayRev(d.getFullYear(), d.getMonth() + 1, d.getDate()));
      l.push(p2(d.getDate()) + '/' + p2(d.getMonth() + 1));
    }
    return { v, l };
  }, [t.getDate()]);

  const total = Object.keys(slots).length;
  if (total === 0) return <div style={{ padding: 40, textAlign: 'center', color: 'var(--mut)' }}>Loading...</div>;

  // Donut
  let a = 0;
  const gradient = K.map(k => { const s = a; a += counts[k] / total * 100; return `${ST[k][1]} ${s}% ${a}%`; }).join(',');
  const occPct = Math.round((counts.occupied + counts.reserved) / total * 100);

  // Selected slot info
  const selSlot = selectedSlotId ? slots[selectedSlotId] : null;
  const mSel = selSlot ? mapStatus(selSlot.status) : null;

  return (
    <>
      <div className="head">
        <h2>Tổng quan</h2>
        <p className="mut" style={{ margin: 0 }}><span className="live"></span>Bãi Vincom Đồng Khởi · cập nhật trực tiếp</p>
      </div>

      <div className="kpis">
        {[
          ['Tổng số ô', total, '#0f1b3d'],
          ['Còn trống', counts.available, ST.available[1]],
          ['Đã đặt trước', counts.reserved, ST.reserved[1]],
          ['Có xe', counts.occupied, ST.occupied[1]],
          ['Doanh thu hôm nay', short(revToday), '#2457e6']
        ].map(([l, v, col], i) => (
          <div key={i} className="card kp"><span>{l}</span><b style={{ color: col as string }}>{v}</b></div>
        ))}
      </div>

      <div className="g8">
        <div className="card">
          <div className="rowb">
            <b>Mô hình 3D bãi xe · 3 tầng</b>
            <div className="segs">
              {['all', ...FL].map(f => (
                <button key={f} className={activeFloor === f ? 'on' : ''} onClick={() => setActiveFloor(f as any)}>
                  {f === 'all' ? 'Tất cả tầng' : 'Tầng ' + f[1]}
                </button>
              ))}
            </div>
          </div>
          <div id="v3" style={{ height: 500 }}>
            <ParkingScene />
          </div>
          <div className="lgd">
            {K.map(k => <span key={k}><i style={{ background: ST[k][1] }}></i>{ST[k][0]}</span>)}
          </div>
          <div className="infobar">
            {!selSlot ? (
              <p className="mut">Bấm vào một ô trên sơ đồ 2D hoặc 3D để xem ô đó đang trống, có xe hay đã đặt trước.</p>
            ) : (
              <>
                <h3>Ô {selSlot.id}</h3>
                <p className="mut">Tầng {selSlot.floorId.replace('f', '')} · Dãy {selSlot.row === 0 ? 'A' : 'B'} · Vị trí {selSlot.index + 1}</p>
                <p><span className="tag" style={{ '--c': ST[mSel!][1] } as any}>{ST[mSel!][0]}</span></p>
                {(mSel === 'occupied' || mSel === 'reserved') && (
                  <p>Biển số <b>{selSlot.vehicle?.plate || plateMock(selSlot.index)}</b><br />
                    <span className="mut">{mSel === 'occupied' ? `Vào lúc 09:${p2(10 + (selSlot.index % 6) * 4)}` : `Giữ chỗ đến 15:${p2(10 + (selSlot.index % 6) * 3)}`}</span>
                  </p>
                )}
                <div className="chips">
                  {K.map(k => (
                    <button key={k} className="chip" onClick={() => updateSlotStatus(selSlot.id, invertMapStatus(k))}>
                      {ST[k][0]}
                    </button>
                  ))}
                </div>
              </>
            )}
          </div>
        </div>

        <div className="col">
          <div className="card">
            <b>Tình trạng ô đỗ</b>
            <div className="dnw">
              <div className="dn" style={{ background: `conic-gradient(${gradient})` }}>
                <div><b>{occPct}%</b><span>lấp đầy</span></div>
              </div>
              <ul className="lg">
                {K.map(k => (
                  <li key={k}><i style={{ background: ST[k][1] }}></i>{ST[k][0]}<b>{counts[k]}</b></li>
                ))}
              </ul>
            </div>
          </div>
          <div className="card">
            <b>Doanh thu 7 ngày gần nhất</b>
            <ChartLine id="c7" vals={rev7.v} labels={rev7.l} />
            <button className="btn" style={{ marginTop: 8 }}>Xem báo cáo chi tiết</button>
          </div>
        </div>
      </div>

      <div className="g2">
        <div className="card">
          <b>Theo từng tầng</b>
          {FL.map(f => {
            const arr = Object.values(slots).filter(s => s.floorId.replace('f', 'T') === f);
            const totalF = arr.length || 1;
            return (
              <div key={f} className="fb">
                <span>Tầng {f[1]}</span>
                <div className="seg">
                  {K.map(k => {
                    const w = arr.filter(s => mapStatus(s.status) === k).length / totalF * 100;
                    return w ? <i key={k} style={{ width: `${w}%`, background: ST[k][1] }}></i> : null;
                  })}
                </div>
                <b>{arr.filter(s => s.status === 'available').length} trống</b>
              </div>
            );
          })}
        </div>
        <div className="card">
          <b>Hoạt động gần đây</b>
          {[
            ['🚗', '51A-123.45 vào ô T1-A3', '2 phút'],
            ['🔖', '59X-678.90 đặt trước ô T2-B5', '8 phút'],
            ['💳', 'Thanh toán 45.000 đ (T1-B2)', '15 phút'],
            ['🔧', 'Ô T3-A7 chuyển sang bảo trì', '32 phút'],
            ['❓', 'Ô T2-A4 cần xác minh biển số', '1 giờ']
          ].map((a, i) => (
            <div key={i} className="act"><span>{a[0]}</span>{a[1]}<time>{a[2]}</time></div>
          ))}
        </div>
      </div>
    </>
  );
}

function plateMock(i: number) {
  return ['51A', '59X', '30H', '51F', '72A', '50Z'][i % 6] + '-' + (100 + i * 37 % 900) + '.' + p2(10 + i * 13 % 90);
}

function invertMapStatus(k: string): SlotStatus {
  if (k === 'available') return 'available';
  if (k === 'reserved') return 'assigned';
  if (k === 'occupied') return 'occupied';
  if (k === 'maintenance') return 'maintenance';
  if (k === 'verify') return 'needs_verification';
  return 'available';
}
