'use client';

import React from 'react';
import { OwnerPageHeader as PageHeader } from '../../../../components/layout/OwnerPageHeader';
import { RevenueChart } from '../../../../components/common/RevenueChart';
import { useOwnerStore } from '../../../../store/useOwnerStore';
import {
  pd,
  p2,
  dim,
  hourly,
  dayRev,
  monRev,
  yrRev,
  HW,
  hs,
  vnd,
} from '../../../../types/owner';

export default function ReportsPage() {
  const reportFilter = useOwnerStore((s) => s.reportFilter);
  const setReportFilter = useOwnerStore((s) => s.setReportFilter);

  const [y, m, d] = pd(reportFilter.date || '2025-10-20');
  const Dt = `${p2(d)}/${p2(m)}/${y}`;

  const mode = reportFilter.mode;

  const S =
    mode === 'day'
      ? {
          v: hourly(y, m, d),
          l: HW.map((_, i) => i + 'h'),
          t: 'theo giờ, ngày ' + Dt,
        }
      : mode === 'month'
      ? {
          v: Array.from({ length: dim(y, m) }, (_, i) => dayRev(y, m, i + 1)),
          l: Array.from({ length: dim(y, m) }, (_, i) => String(i + 1)),
          t: `theo ngày, tháng ${p2(m)}/${y}`,
        }
      : {
          v: Array.from({ length: 12 }, (_, i) => monRev(y, i + 1)),
          l: Array.from({ length: 12 }, (_, i) => 'T' + (i + 1)),
          t: 'theo tháng, năm ' + y,
        };

  const pv = new Date(y, m - 1, d - 1);
  const pm = new Date(y, m - 2, 1);

  const a = dayRev(y, m, d);
  const b = monRev(y, m);
  const c = yrRev(y);

  const T = mode === 'day' ? a : mode === 'month' ? b : c;
  const car = 0.6 + hs(reportFilter.date) * 0.15;
  const rs = 0.35 + hs(reportFilter.date + 'r') * 0.2;

  const getDeltaBadge = (curr: number, prev: number) => {
    const q = ((curr - prev) / prev) * 100;
    return (
      <span className="dl">
        {q >= 0 ? '▲' : '▼'} {Math.abs(q).toFixed(1).replace('.', ',')}% so với kỳ trước
      </span>
    );
  };

  const K3 = [
    {
      mode: 'day' as const,
      title: 'Ngày ' + Dt,
      val: a,
      delta: getDeltaBadge(a, dayRev(pv.getFullYear(), pv.getMonth() + 1, pv.getDate())),
    },
    {
      mode: 'month' as const,
      title: `Tháng ${p2(m)}/${y}`,
      val: b,
      delta: getDeltaBadge(b, monRev(pm.getFullYear(), pm.getMonth() + 1)),
    },
    {
      mode: 'year' as const,
      title: 'Năm ' + y,
      val: c,
      delta: getDeltaBadge(c, yrRev(y - 1)),
    },
  ];

  return (
    <>
      <PageHeader
        breadcrumb="/owner/reports"
        title="Báo cáo doanh thu"
        subtitle="Chọn ngày để xem doanh thu ngày, tháng và năm tương ứng"
        actions={
          <label className="f" style={{ margin: 0 }}>
            <span>Ngày xem</span>
            <input
              type="date"
              id="dt"
              value={reportFilter.date}
              onChange={(e) => {
                if (e.target.value) {
                  setReportFilter({ date: e.target.value });
                }
              }}
            />
          </label>
        }
      />

      {/* 3 KPI period cards */}
      <div className="kpis">
        {K3.map((k) => (
          <div
            key={k.mode}
            className="card kp"
            style={{
              cursor: 'pointer',
              ...(mode === k.mode
                ? {
                    borderColor: 'var(--ink)',
                    boxShadow: '0 0 0 2px var(--ink)',
                  }
                : {}),
            }}
            onClick={() => setReportFilter({ mode: k.mode })}
          >
            <span>{k.title}</span>
            <b>{vnd(k.val)}</b>
            {k.delta}
          </div>
        ))}
      </div>

      <div className="grid">
        {/* Chart card */}
        <div className="card c8">
          <div className="wh">
            <h3>Doanh thu {S.t}</h3>
            <div className="segs">
              {[
                ['day', 'Ngày'],
                ['month', 'Tháng'],
                ['year', 'Năm'],
              ].map(([val, label]) => (
                <button
                  key={val}
                  className={mode === val ? 'on' : ''}
                  onClick={() => setReportFilter({ mode: val as 'day' | 'month' | 'year' })}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>
          <RevenueChart vals={S.v} labels={S.l} />
        </div>

        {/* Breakdown card */}
        <div className="card c4">
          <div className="wh">
            <h3>Cơ cấu doanh thu</h3>
          </div>
          <p className="mut" style={{ margin: 0 }}>
            Tổng {vnd(T)}
          </p>

          <div style={{ margin: '14px 0' }}>
            <div className="wh" style={{ margin: 0 }}>
              <span>Ô tô</span>
              <b>{vnd(T * car)}</b>
            </div>
            <div className="bar">
              <i style={{ width: `${car * 100}%` }} />
            </div>
          </div>

          <div style={{ margin: '14px 0' }}>
            <div className="wh" style={{ margin: 0 }}>
              <span>Xe máy</span>
              <b>{vnd(T * (1 - car))}</b>
            </div>
            <div className="bar">
              <i style={{ width: `${(1 - car) * 100}%` }} />
            </div>
          </div>

          <div style={{ margin: '14px 0' }}>
            <div className="wh" style={{ margin: 0 }}>
              <span>Đặt trước</span>
              <b>{vnd(T * rs)}</b>
            </div>
            <div className="bar">
              <i style={{ width: `${rs * 100}%` }} />
            </div>
          </div>

          <div style={{ margin: '14px 0' }}>
            <div className="wh" style={{ margin: 0 }}>
              <span>Vãng lai</span>
              <b>{vnd(T * (1 - rs))}</b>
            </div>
            <div className="bar">
              <i style={{ width: `${(1 - rs) * 100}%` }} />
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
