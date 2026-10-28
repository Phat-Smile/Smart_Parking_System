'use client';

import React, { useState } from 'react';
import { useOwnerMapStore } from './useOwnerMapStore';
import { useLotInfoStore } from './lotInfo';
import { useOwnerStore } from '@/store/useOwnerStore';
import { ParkingLot } from '@/types/owner';
import { registerParkingLotApi } from '@/features/owner/api/ownerApi';
import { ApiError } from '@/shared/errors/api-error';

export function AddLotModal() {
  const { isAddModalOpen, setAddModalOpen, addOverlayLot } = useOwnerMapStore();

  const [name, setName] = useState('');
  const [addr, setAddr] = useState('');
  const [cc, setCc] = useState(200);
  const [cm, setCm] = useState(100);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  if (!isAddModalOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;

    setIsSubmitting(true);
    setErrorMsg('');

    let registeredId = Date.now();

    try {
      const res = await registerParkingLotApi({
        name: name.trim(),
        address: addr.trim() || 'TP.HCM',
        latitude: 10.7745,
        longitude: 106.7035,
        type: 'Outdoor',
      });
      if (res?.id) {
        registeredId = Math.abs(res.id.split('').reduce((acc, char) => (acc << 5) - acc + char.charCodeAt(0), 0) % 100000) + 10;
      }
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        if (err.status === 409) {
          setErrorMsg('Tên bãi đỗ xe này đã được đăng ký trước đó. Vui lòng chọn tên khác.');
          setIsSubmitting(false);
          return;
        }
        if (err.status === 400) {
          setErrorMsg(err.problem?.detail || 'Thông tin bãi đỗ xe chưa đúng định dạng.');
          setIsSubmitting(false);
          return;
        }
      }
      console.warn('Backend API registration call skipped or offline:', err);
    }

    const newLot: ParkingLot = {
      id: registeredId,
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
    useOwnerStore.getState().addLot(newLot);

    // Initialize lotInfo with submittedAt = today
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

    setIsSubmitting(false);
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

        {errorMsg && (
          <div
            style={{
              background: '#fef2f2',
              border: '1px solid #fecaca',
              color: '#991b1b',
              padding: '10px 14px',
              borderRadius: 10,
              fontSize: 13,
              marginBottom: 14,
            }}
          >
            ⚠️ {errorMsg}
          </div>
        )}

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
              disabled={isSubmitting}
              style={{
                background: isSubmitting ? '#93c5fd' : '#2563eb',
                border: 'none',
                borderRadius: 10,
                padding: '8px 18px',
                fontSize: 13,
                fontWeight: 700,
                cursor: isSubmitting ? 'not-allowed' : 'pointer',
                color: '#fff',
              }}
            >
              {isSubmitting ? 'Đang gửi duyệt...' : 'Gửi phê duyệt'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
