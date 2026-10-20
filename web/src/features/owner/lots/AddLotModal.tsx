'use client';

import React, { useState } from 'react';
import { useOwnerMapStore } from './useOwnerMapStore';
import { useLotInfoStore } from './lotInfo';
import { ParkingLot } from '@/types/owner';

export function AddLotModal() {
  const { isAddModalOpen, setAddModalOpen, addOverlayLot } = useOwnerMapStore();

  const [name, setName] = useState('');
  const [addr, setAddr] = useState('');
  const [cc, setCc] = useState(200);
  const [cm, setCm] = useState(100);

  if (!isAddModalOpen) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;

    const newLot: ParkingLot = {
      id: Date.now(),
      name: name.trim(),
      addr: addr.trim() || 'TP.HCM',
      gps: '10.7745, 106.7035',
      op: '06:00',
      cl2: '23:00',
      vc: true,
      vm: cm > 0,
      cc,
      cm,
      fls: 3,
      pc: 30000,
      pm: 5000,
      dep: 20000,
      res: 'on',
      hold: 20,
      maxr: 8,
      fc: 8,
      ap: 'pending',
      zones: [{ n: 'Khu A', t: 'car', c: cc, s: 'open' }],
    };

    addOverlayLot(newLot);
    // Initialize lotInfo with submittedAt = today, rest "Chưa cập nhật"
    const today = new Date().toISOString().split('T')[0];
    useLotInfoStore.getState().setLotInfo(newLot.id, {
      submittedAt: today,
      openingHours: `${newLot.op} – ${newLot.cl2}`,
      phone: 'Chưa cập nhật',
      prices: {
        carFirstHour: newLot.pc,
        carNextHour: newLot.pc,
        reservationDeposit: newLot.dep,
      },
    });
    setAddModalOpen(false);
    setName('');
    setAddr('');
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(15,27,61,0.4)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1000,
        padding: 16,
      }}
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-title"
    >
      <div
        style={{
          background: '#fff',
          borderRadius: 20,
          padding: 28,
          width: '100%',
          maxWidth: 480,
          boxShadow: '0 20px 40px rgba(15,27,61,0.2)',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
          <h2 id="modal-title" style={{ fontSize: 18, fontWeight: 800, margin: 0, color: '#0f1b3d' }}>
            Thêm bãi đỗ xe mới
          </h2>
          <button
            type="button"
            onClick={() => setAddModalOpen(false)}
            aria-label="Đóng"
            style={{ border: 'none', background: 'none', fontSize: 18, cursor: 'pointer', color: '#6a7491' }}
          >
            ✕
          </button>
        </div>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div>
            <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#0f1b3d', marginBottom: 4 }}>
              Tên bãi đỗ xe
            </label>
            <input
              type="text"
              required
              placeholder="VD: Bãi xe Nguyễn Du"
              value={name}
              onChange={(e) => setName(e.target.value)}
              style={{
                width: '100%',
                border: '1px solid #e3e8f2',
                borderRadius: 10,
                padding: '9px 12px',
                fontSize: 13,
                outline: 'none',
              }}
            />
          </div>

          <div>
            <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#0f1b3d', marginBottom: 4 }}>
              Địa chỉ bãi
            </label>
            <input
              type="text"
              required
              placeholder="VD: Quận 1, TP.HCM"
              value={addr}
              onChange={(e) => setAddr(e.target.value)}
              style={{
                width: '100%',
                border: '1px solid #e3e8f2',
                borderRadius: 10,
                padding: '9px 12px',
                fontSize: 13,
                outline: 'none',
              }}
            />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <div>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#0f1b3d', marginBottom: 4 }}>
                Sức chứa ô tô
              </label>
              <input
                type="number"
                min={10}
                max={5000}
                value={cc}
                onChange={(e) => setCc(Number(e.target.value))}
                style={{
                  width: '100%',
                  border: '1px solid #e3e8f2',
                  borderRadius: 10,
                  padding: '9px 12px',
                  fontSize: 13,
                  outline: 'none',
                }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#0f1b3d', marginBottom: 4 }}>
                Sức chứa xe máy
              </label>
              <input
                type="number"
                min={0}
                max={5000}
                value={cm}
                onChange={(e) => setCm(Number(e.target.value))}
                style={{
                  width: '100%',
                  border: '1px solid #e3e8f2',
                  borderRadius: 10,
                  padding: '9px 12px',
                  fontSize: 13,
                  outline: 'none',
                }}
              />
            </div>
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 16 }}>
            <button
              type="button"
              onClick={() => setAddModalOpen(false)}
              style={{
                background: '#f3f6fb',
                border: '1px solid #e3e8f2',
                borderRadius: 10,
                padding: '8px 16px',
                fontSize: 13,
                fontWeight: 600,
                cursor: 'pointer',
                color: '#6a7491',
              }}
            >
              Hủy
            </button>
            <button
              type="submit"
              style={{
                background: '#2563eb',
                border: 'none',
                borderRadius: 10,
                padding: '8px 18px',
                fontSize: 13,
                fontWeight: 700,
                cursor: 'pointer',
                color: '#fff',
              }}
            >
              Gửi phê duyệt
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
