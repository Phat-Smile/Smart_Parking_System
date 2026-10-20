'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { PageHeader } from '@/components/layout/PageHeader';
import { useOwnerStore } from '@/store/useOwnerStore';
import { vnd } from '@/types/owner';
import { Calculator, Check, AlertCircle, ArrowLeft } from 'lucide-react';

export default function LotPricingConfigPage() {
  const params = useParams();
  const lotId = Number(params?.id) || 1;

  const lots = useOwnerStore((s) => s.lots);
  const lot = lots.find((l) => l.id === lotId) || lots[0];

  // Pricing configuration state
  const [pricingType, setPricingType] = useState<'block' | 'turn'>('block');
  const [carHourly, setCarHourly] = useState(lot?.pc || 30000);
  const [motoHourly, setMotoHourly] = useState(lot?.pm || 5000);
  const [blockDuration, setBlockDuration] = useState(60); // minutes
  const [overnightCar, setOvernightCar] = useState(150000);
  const [overnightMoto, setOvernightMoto] = useState(30000);
  const [deposit, setDeposit] = useState(lot?.dep || 20000);

  // Price simulator / preview state
  const [simDuration, setSimDuration] = useState(3); // hours
  const [simVehicle, setSimVehicle] = useState<'car' | 'moto'>('car');
  const [isOvernight, setIsOvernight] = useState(false);
  const [savedToast, setSavedToast] = useState(false);

  // Calculate estimated price
  const calculatePreviewPrice = () => {
    const rate = simVehicle === 'car' ? carHourly : motoHourly;
    const overnightRate = simVehicle === 'car' ? overnightCar : overnightMoto;
    
    if (isOvernight) {
      return overnightRate;
    }
    if (pricingType === 'turn') {
      return rate;
    }
    // Block-based calculation
    const blocks = Math.ceil((simDuration * 60) / blockDuration);
    return blocks * (rate * (blockDuration / 60));
  };

  const handleSave = () => {
    setSavedToast(true);
    setTimeout(() => setSavedToast(false), 2500);
  };

  return (
    <>
      {savedToast && <div className="toast">Đã lưu cấu hình bảng giá thành công!</div>}

      <PageHeader
        breadcrumb={`/owner/lots/${lotId}/pricing`}
        title={`Cấu hình bảng giá · ${lot?.name || 'Bãi xe'}`}
        subtitle="Thiết lập biểu phí theo lượt, theo block, qua đêm và kiểm tra xem trước giá tính toán"
        actions={
          <div style={{ display: 'flex', gap: '8px' }}>
            <Link href={`/owner/parking-lots/${lotId}`} className="btn">
              <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                <ArrowLeft size={14} /> Về chi tiết bãi
              </span>
            </Link>
            <button className="btn p" onClick={handleSave}>
              Lưu bảng giá
            </button>
          </div>
        }
      />

      {/* Backend API status notice */}
      <div
        className="err"
        style={{
          borderColor: '#b7791f',
          background: '#fff9db',
          color: '#744210',
          marginBottom: '18px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: '8px' }}>
          <AlertCircle size={18} style={{ flexShrink: 0, marginTop: '2px' }} />
          <div>
            <b>Thông báo tích hợp Backend (BE-5 / Payment Service):</b>
            <p style={{ margin: '4px 0 0', fontSize: '13px' }}>
              Chức năng cấu hình bảng giá thuộc User Story <code>FE-OWNER-004-1</code> phụ thuộc vào microservice <b>Payment: bảng giá (BE-5)</b>.
              Hiện tại backend <code>Payment.WebAPI</code> chưa mở endpoint public cho bảng giá. Giao diện đang lưu trên state và sẵn sàng kết nối ngay khi BE-5 triển khai.
            </p>
          </div>
        </div>
      </div>

      <div className="grid">
        {/* Form cấu hình */}
        <div className="card c7">
          <div className="wh">
            <h3>Phương thức tính phí & Biểu giá</h3>
          </div>

          <div style={{ marginBottom: '16px' }}>
            <span className="mut" style={{ fontSize: '12px', display: 'block', marginBottom: '6px' }}>
              Hình thức tính phí chính
            </span>
            <div className="segs">
              <button
                className={pricingType === 'block' ? 'on' : ''}
                onClick={() => setPricingType('block')}
              >
                Theo Block thời gian
              </button>
              <button
                className={pricingType === 'turn' ? 'on' : ''}
                onClick={() => setPricingType('turn')}
              >
                Theo Lượt cố định
              </button>
            </div>
          </div>

          <div className="form">
            <label className="f">
              <span>Giá ô tô ({pricingType === 'block' ? 'đ/giờ' : 'đ/lượt'})</span>
              <input
                type="number"
                value={carHourly}
                onChange={(e) => setCarHourly(Number(e.target.value))}
              />
            </label>

            <label className="f">
              <span>Giá xe máy ({pricingType === 'block' ? 'đ/giờ' : 'đ/lượt'})</span>
              <input
                type="number"
                value={motoHourly}
                onChange={(e) => setMotoHourly(Number(e.target.value))}
              />
            </label>

            {pricingType === 'block' && (
              <label className="f">
                <span>Độ dài block tính phí</span>
                <select
                  value={blockDuration}
                  onChange={(e) => setBlockDuration(Number(e.target.value))}
                >
                  <option value={30}>Block 30 phút</option>
                  <option value={60}>Block 60 phút (1 giờ)</option>
                  <option value={120}>Block 120 phút (2 giờ)</option>
                </select>
              </label>
            )}

            <label className="f">
              <span>Phí gửi qua đêm (Ô tô)</span>
              <input
                type="number"
                value={overnightCar}
                onChange={(e) => setOvernightCar(Number(e.target.value))}
              />
            </label>

            <label className="f">
              <span>Phí gửi qua đêm (Xe máy)</span>
              <input
                type="number"
                value={overnightMoto}
                onChange={(e) => setOvernightMoto(Number(e.target.value))}
              />
            </label>

            <label className="f">
              <span>Tiền cọc giữ chỗ trước (đ)</span>
              <input
                type="number"
                value={deposit}
                onChange={(e) => setDeposit(Number(e.target.value))}
              />
            </label>
          </div>

          <div style={{ marginTop: '20px', paddingTop: '16px', borderTop: '1px solid var(--line)' }}>
            <button className="btn p" onClick={handleSave}>
              <Check size={16} style={{ display: 'inline-block', marginRight: '6px' }} />
              Áp dụng bảng giá cho bãi
            </button>
          </div>
        </div>

        {/* Xem trước giá */}
        <div className="card c5">
          <div className="wh">
            <h3>Xem trước giá tính toán</h3>
          </div>
          <p className="mut" style={{ fontSize: '13px', margin: '0 0 16px' }}>
            Thử nghiệm công thức tính toán phí gửi xe cho khách hàng theo thông số thực tế.
          </p>

          <div className="form">
            <label className="f">
              <span>Loại phương tiện thử nghiệm</span>
              <select
                value={simVehicle}
                onChange={(e) => setSimVehicle(e.target.value as 'car' | 'moto')}
              >
                <option value="car">Ô tô</option>
                <option value="moto">Xe máy</option>
              </select>
            </label>

            <label className="f">
              <span>Thời gian đỗ (giờ)</span>
              <input
                type="number"
                min={0.5}
                step={0.5}
                disabled={isOvernight}
                value={simDuration}
                onChange={(e) => setSimDuration(Number(e.target.value))}
              />
            </label>
          </div>

          <div style={{ margin: '14px 0' }}>
            <label className="f ck">
              <input
                type="checkbox"
                checked={isOvernight}
                onChange={(e) => setIsOvernight(e.target.checked)}
              />
              Áp dụng gửi qua đêm (sau 22:00)
            </label>
          </div>

          <div
            style={{
              background: 'var(--soft)',
              padding: '16px',
              borderRadius: '14px',
              marginTop: '16px',
              textAlign: 'center',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '8px', color: 'var(--mut)', fontSize: '13px' }}>
              <Calculator size={16} />
              <span>Tổng tiền dự kiến</span>
            </div>
            <div style={{ fontSize: '32px', fontWeight: 800, marginTop: '6px', color: 'var(--ink)' }}>
              {vnd(calculatePreviewPrice())}
            </div>
            <div style={{ fontSize: '11px', color: 'var(--mut)', marginTop: '4px' }}>
              {isOvernight
                ? 'Tính theo biểu giá qua đêm'
                : pricingType === 'turn'
                ? 'Tính theo lượt cố định'
                : `Tính theo ${Math.ceil((simDuration * 60) / blockDuration)} block (${blockDuration} phút/block)`}
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
