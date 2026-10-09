"use client";

import React, { useMemo } from "react";
import { useParkingStore } from "../../../../../store/useParkingStore";
import { ParkingScene } from "../../../../../components/viz/ParkingScene";
import { SlotStatus } from "../../../../../domain/types";

const ST: Record<string, [string, string]> = {
  available: ['Trống', '#16a36a'],
  reserved: ['Đã đặt trước', '#2f80ed'],
  occupied: ['Có xe', '#e5484d'],
  maintenance: ['Bảo trì', '#f2a007'],
  verify: ['Cần xác minh', '#8a5cf6']
};
const K = Object.keys(ST);
const FL = ['T1', 'T2', 'T3'];

const mapStatus = (s: SlotStatus): string => {
  if (s === "available") return "available";
  if (s === "assigned") return "reserved";
  if (s === "occupied") return "occupied";
  if (s === "maintenance") return "maintenance";
  if (s === "needs_verification") return "verify";
  return "available";
};
const p2 = (n: number) => String(n).padStart(2, '0');
const hs = (s: string) => { let x = 7; for (let i = 0; i < s.length; i++) x = (x * 33 + s.charCodeAt(i)) % 10007; return x / 10007; };
const dayRev = (y: number, m: number, d: number) => { const w = new Date(y, m - 1, d).getDay(); return (3.2e6 + hs(y + '-' + m + '-' + d) * 2.4e6) * (w == 0 || w == 6 ? 1.3 : 1) * (1 + (y - 2025) * .1); };
const short = (n: number) => n >= 1e9 ? (n / 1e9).toFixed(2).replace('.', ',') + ' tỷ' : (n / 1e6).toFixed(1).replace('.', ',') + ' tr';

export default function MapPage() {
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

  const total = Object.keys(slots).length;
  if (total === 0) return <div>Loading...</div>;

  const t = new Date();
  const revToday = dayRev(t.getFullYear(), t.getMonth() + 1, t.getDate());

  const selSlot = selectedSlotId ? slots[selectedSlotId] : null;
  const mSel = selSlot ? mapStatus(selSlot.status) : null;

  return (
    <>
      <div className="head">
        <h2>Sơ đồ 2D / 3D</h2>
        <p className="mut" style={{ margin: 0 }}><span className="live"></span>Hai sơ đồ dùng chung dữ liệu, đổi một nơi là cả hai cùng đổi</p>
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

        <div className="card">
          <div className="rowb"><b>Sơ đồ 2D</b></div>
          <div id="plan">
            {(activeFloor === 'all' ? FL : [activeFloor]).map(f => {
              const rowData = (rStr: string) => {
                const rInt = rStr === 'A' ? 0 : 1;
                return Object.values(slots)
                  .filter(s => s.floorId.replace('f', 'T') === f && s.row === rInt)
                  .sort((a, b) => a.index - b.index)
                  .map(s => {
                    const st = mapStatus(s.status);
                    return (
                      <button 
                        key={s.id} 
                        className={`cell ${selectedSlotId === s.id ? 'sel' : ''}`} 
                        style={{ '--c': ST[st][1] } as any}
                        onClick={() => selectSlot(s.id)}
                        title={`${s.id} · ${ST[st][0]}`}
                      >
                        <small>{rStr}{s.index + 1}</small>
                        {st === 'occupied' ? '🚗' : st === 'reserved' ? '🔖' : st === 'maintenance' ? '🔧' : st === 'verify' ? '❓' : ''}
                      </button>
                    );
                  });
              };
              return (
                <div key={f} className="pf">
                  <h4>Tầng {f[1]}</h4>
                  <div className="pr">{rowData('A')}</div>
                  <div className="ln">Lối xe chạy</div>
                  <div className="pr">{rowData('B')}</div>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </>
  );
}

function invertMapStatus(k: string): SlotStatus {
  if (k === 'available') return 'available';
  if (k === 'reserved') return 'assigned';
  if (k === 'occupied') return 'occupied';
  if (k === 'maintenance') return 'maintenance';
  if (k === 'verify') return 'needs_verification';
  return 'available';
}
