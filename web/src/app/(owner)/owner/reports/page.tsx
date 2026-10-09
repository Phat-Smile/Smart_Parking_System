"use client";

import React, { useState } from "react";

const p2 = (n: number) => String(n).padStart(2, '0');
const vnd = (n: number) => Math.round(n).toLocaleString('vi-VN') + ' đ';
const short = (n: number) => n >= 1e9 ? (n / 1e9).toFixed(2).replace('.', ',') + ' tỷ' : (n / 1e6).toFixed(1).replace('.', ',') + ' tr';
const hs = (s: string) => { let x = 7; for (let i = 0; i < s.length; i++) x = (x * 33 + s.charCodeAt(i)) % 10007; return x / 10007; };
const pd = (s: string) => s.split('-').map(Number);
const dim = (y: number, m: number) => new Date(y, m, 0).getDate();
const dayRev = (y: number, m: number, d: number) => { const w = new Date(y, m - 1, d).getDay(); return (3.2e6 + hs(y + '-' + m + '-' + d) * 2.4e6) * (w == 0 || w == 6 ? 1.3 : 1) * (1 + (y - 2025) * .1); };
const monRev = (y: number, m: number) => { let t = 0; for (let d = 1; d <= dim(y, m); d++) t += dayRev(y, m, d); return t; };
const yrRev = (y: number) => { let t = 0; for (let m = 1; m <= 12; m++) t += monRev(y, m); return t; };
const HW = [1, 1, 1, 1, 1, 2, 4, 7, 9, 8, 7, 8, 9, 8, 7, 7, 8, 10, 9, 7, 5, 3, 2, 1];
const hourly = (y: number, m: number, d: number) => { const t = dayRev(y, m, d), s = HW.reduce((a, b) => a + b); return HW.map(w => t * w / s); };

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

export default function ReportsPage() {
  const [date, setDate] = useState('2025-10-20');
  const [mode, setMode] = useState('day');

  const [y, m, d] = pd(date);
  const D = `${p2(d)}/${p2(m)}/${y}`;
  const S = mode === 'day' ? { v: hourly(y, m, d), l: HW.map((_, i) => i + 'h'), t: 'theo giờ, ngày ' + D }
          : mode === 'month' ? { v: [...Array(dim(y, m))].map((_, i) => dayRev(y, m, i + 1)), l: [...Array(dim(y, m))].map((_, i) => String(i + 1)), t: `theo ngày, tháng ${p2(m)}/${y}` }
          : { v: [...Array(12)].map((_, i) => monRev(y, i + 1)), l: [...Array(12)].map((_, i) => 'T' + (i + 1)), t: 'theo tháng, năm ' + y };

  const pv = new Date(y, m - 1, d - 1), pm = new Date(y, m - 2, 1);
  const a = dayRev(y, m, d), b = monRev(y, m), c = yrRev(y);
  const T = mode === 'day' ? a : mode === 'month' ? b : c;
  const car = .6 + hs(date) * .15, rs = .35 + hs(date + 'r') * .2;

  const delta = (v1: number, v2: number) => {
    const diff = (v1 - v2) / v2 * 100;
    return (
      <span className="dl" style={{ color: diff >= 0 ? '#16a36a' : '#e5484d' }}>
        {diff >= 0 ? '▲' : '▼'} {Math.abs(diff).toFixed(1).replace('.', ',')}% so với kỳ trước
      </span>
    );
  };

  const K3 = [
    ['day', 'Ngày ' + D, a, delta(a, dayRev(pv.getFullYear(), pv.getMonth() + 1, pv.getDate()))],
    ['month', `Tháng ${p2(m)}/${y}`, b, delta(b, monRev(pm.getFullYear(), pm.getMonth() + 1))],
    ['year', 'Năm ' + y, c, delta(c, yrRev(y - 1))]
  ];

  const bd = (l: string, x: number, col: string) => (
    <div className="bd">
      <div className="rowb" style={{ margin: 0 }}><span>{l}</span><b>{vnd(T * x)}</b></div>
      <div className="seg"><i style={{ width: `${x * 100}%`, background: col }}></i></div>
    </div>
  );

  return (
    <>
      <div className="head rowb">
        <div>
          <h2>Báo cáo doanh thu</h2>
          <p className="mut" style={{ margin: 0 }}>Chọn ngày để xem doanh thu ngày, tháng và năm tương ứng</p>
        </div>
        <label>Ngày xem <input type="date" id="dt" value={date} onChange={(e) => setDate(e.target.value)} /></label>
      </div>

      <div className="rk">
        {K3.map((k: any) => (
          <div key={k[0]} className={`card ${mode === k[0] ? 'on' : ''}`} onClick={() => setMode(k[0])}>
            <span className="mut">{k[1]}</span>
            <b>{vnd(k[2])}</b>
            {k[3]}
          </div>
        ))}
      </div>

      <div className="g8">
        <div className="card">
          <div className="rowb">
            <b>Doanh thu {S.t}</b>
            <div className="segs">
              {[['day', 'Ngày'], ['month', 'Tháng'], ['year', 'Năm']].map(x => (
                <button key={x[0]} className={mode === x[0] ? 'on' : ''} onClick={() => setMode(x[0])}>{x[1]}</button>
              ))}
            </div>
          </div>
          <ChartLine id="cr" vals={S.v} labels={S.l} />
        </div>

        <div className="card">
          <b>Cơ cấu doanh thu</b>
          <p className="mut" style={{ margin: '2px 0 10px' }}>Tổng {vnd(T)}</p>
          {bd('Ô tô', car, '#2457e6')}
          {bd('Xe máy', 1 - car, '#7aa2f7')}
          {bd('Đặt trước', rs, '#16a36a')}
          {bd('Vãng lai', 1 - rs, '#9adfbd')}
        </div>
      </div>
    </>
  );
}
