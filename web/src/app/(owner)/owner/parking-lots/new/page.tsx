'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import dynamic from 'next/dynamic';
import { PageHeader } from '../../../../../components/layout/PageHeader';
import { Plan2D } from '../../../../../components/viz/Plan2D';
import { useOwnerStore } from '../../../../../store/useOwnerStore';
import { AP, vnd, ST, K } from '../../../../../types/owner';

const ParkingScene3DViewer = dynamic(
  () => import('../../../../../components/viz/ParkingScene3DViewer'),
  { ssr: false }
);

interface WizardData {
  name: string;
  addr: string;
  gps: string;
  op: string;
  cl2: string;
  vc: boolean;
  vm: boolean;
  cap: number;
  pc: number;
  pm: number;
  dep: number;
  hold: number;
  layout: string;
}

const INITIAL_DATA: WizardData = {
  name: '',
  addr: '',
  gps: '',
  op: '06:00',
  cl2: '22:00',
  vc: true,
  vm: true,
  cap: 500,
  pc: 30000,
  pm: 5000,
  dep: 20000,
  hold: 15,
  layout: '5',
};

export default function NewParkingLotPage() {
  const addLot = useOwnerStore((s) => s.addLot);

  const [step, setStep] = useState(0);
  const [data, setData] = useState<WizardData>(INITIAL_DATA);
  const [errors, setErrors] = useState<string[]>([]);
  const [previewMode, setPreviewMode] = useState<'3d' | '2d'>('3d');
  const [confirming, setConfirming] = useState(false);
  const [done, setDone] = useState(false);

  const validate = () => {
    const errs: string[] = [];
    if (!data.name.trim()) errs.push('Nhập tên bãi');
    if (!data.addr.trim()) errs.push('Nhập địa chỉ');
    if (!/^-?\d+(\.\d+)?\s*,\s*-?\d+(\.\d+)?$/.test(data.gps.trim())) {
      errs.push('Tọa độ GPS theo dạng "vĩ độ, kinh độ", ví dụ 10.7769, 106.7009');
    }
    if (!(data.cap > 0)) errs.push('Sức chứa phải lớn hơn 0');
    if (!data.vc && !data.vm) errs.push('Chọn ít nhất một loại xe');
    return errs;
  };

  const handleNext = () => {
    if (step === 0) {
      const errs = validate();
      if (errs.length > 0) {
        setErrors(errs);
        return;
      }
      setErrors([]);
      setStep(1);
    } else if (step === 1) {
      setStep(2);
    } else if (step === 2) {
      setConfirming(true);
    }
  };

  const handleBack = () => {
    if (confirming) {
      setConfirming(false);
      return;
    }
    if (step > 0) {
      setStep(step - 1);
    }
  };

  const handleConfirmSend = () => {
    addLot({
      id: (Date.now() % 100000) + 10,
      name: data.name,
      addr: data.addr,
      gps: data.gps,
      op: data.op,
      cl2: data.cl2,
      vc: data.vc,
      vm: data.vm,
      cc: data.vc ? data.cap : 0,
      cm: data.vm ? data.cap : 0,
      fls: Number(data.layout) || 5,
      pc: data.pc,
      pm: data.pm,
      dep: data.dep,
      res: Number(data.hold) > 0 ? 'on' : 'off',
      hold: Number(data.hold) || 15,
      maxr: 6,
      fc: 10,
      ap: 'pending',
      zones: [],
    });
    setDone(true);
  };

  if (done) {
    return (
      <>
        <PageHeader
          breadcrumb="/owner/parking-lots/new"
          title="Đăng ký bãi mới"
          subtitle="Gửi duyệt thành công"
        />
        <div className="ok">
          <h3 style={{ marginTop: 0 }}>Đã gửi duyệt</h3>
          <p>
            Bãi &quot;{data.name}&quot; đang ở trạng thái{' '}
            <span
              className="tag"
              style={{ '--c': AP.pending[1] } as React.CSSProperties}
            >
              {AP.pending[0]}
            </span>
            . Bạn sẽ thấy kết quả trong danh sách bãi khi quản trị viên xử lý.
          </p>
          <Link href="/owner/parking-lots" className="btn p">
            Về danh sách bãi
          </Link>
        </div>
      </>
    );
  }

  const steps = ['Cấu hình', 'Xem trước', 'Gửi duyệt'];

  return (
    <>
      <PageHeader
        breadcrumb="/owner/parking-lots/new"
        title="Đăng ký bãi mới"
        subtitle="Bản nháp → Xem lại → Gửi → Chờ duyệt"
      />

      <div className="steps">
        {steps.map((s, i) => (
          <span key={i} className={i === step ? 'on' : ''}>
            {i + 1}. {s}
          </span>
        ))}
      </div>

      {/* Step 0: Cấu hình */}
      {step === 0 && (
        <>
          {errors.length > 0 && (
            <div className="err">
              {errors.map((e, idx) => (
                <div key={idx}>• {e}</div>
              ))}
            </div>
          )}

          <div className="card">
            <div className="form">
              <label className="f">
                <span>Tên bãi</span>
                <input
                  type="text"
                  value={data.name}
                  onChange={(e) => setData({ ...data, name: e.target.value })}
                />
              </label>

              <label className="f">
                <span>Địa chỉ</span>
                <input
                  type="text"
                  value={data.addr}
                  onChange={(e) => setData({ ...data, addr: e.target.value })}
                />
              </label>

              <label className="f">
                <span>Tọa độ GPS</span>
                <input
                  type="text"
                  value={data.gps}
                  placeholder="10.7769, 106.7009"
                  onChange={(e) => setData({ ...data, gps: e.target.value })}
                />
              </label>

              <label className="f">
                <span>Giờ mở cửa</span>
                <input
                  type="time"
                  value={data.op}
                  onChange={(e) => setData({ ...data, op: e.target.value })}
                />
              </label>

              <label className="f">
                <span>Giờ đóng cửa</span>
                <input
                  type="time"
                  value={data.cl2}
                  onChange={(e) => setData({ ...data, cl2: e.target.value })}
                />
              </label>

              <label className="f">
                <span>Sức chứa tổng</span>
                <input
                  type="number"
                  value={data.cap}
                  onChange={(e) => setData({ ...data, cap: Number(e.target.value) })}
                />
              </label>

              <label className="f">
                <span>Giá ô tô (đ/giờ)</span>
                <input
                  type="number"
                  value={data.pc}
                  onChange={(e) => setData({ ...data, pc: Number(e.target.value) })}
                />
              </label>

              <label className="f">
                <span>Giá xe máy (đ/giờ)</span>
                <input
                  type="number"
                  value={data.pm}
                  onChange={(e) => setData({ ...data, pm: Number(e.target.value) })}
                />
              </label>

              <label className="f">
                <span>Tiền cọc giữ chỗ (đ)</span>
                <input
                  type="number"
                  value={data.dep}
                  onChange={(e) => setData({ ...data, dep: Number(e.target.value) })}
                />
              </label>

              <label className="f">
                <span>Chính sách đặt trước</span>
                <select
                  value={data.hold}
                  onChange={(e) => setData({ ...data, hold: Number(e.target.value) })}
                >
                  <option value={0}>Không cho đặt trước</option>
                  <option value={15}>Giữ chỗ 15 phút</option>
                  <option value={30}>Giữ chỗ 30 phút</option>
                </select>
              </label>

              <label className="f">
                <span>Mẫu sơ đồ</span>
                <select
                  value={data.layout}
                  onChange={(e) => setData({ ...data, layout: e.target.value })}
                >
                  <option value="5">5 tầng · 4 dãy · 500 ô</option>
                  <option value="3">3 tầng · 4 dãy · 300 ô</option>
                </select>
              </label>
            </div>

            <div className="form" style={{ marginTop: '14px' }}>
              <label className="f ck">
                <input
                  type="checkbox"
                  checked={data.vc}
                  onChange={(e) => setData({ ...data, vc: e.target.checked })}
                />
                Nhận ô tô
              </label>
              <label className="f ck">
                <input
                  type="checkbox"
                  checked={data.vm}
                  onChange={(e) => setData({ ...data, vm: e.target.checked })}
                />
                Nhận xe máy
              </label>
            </div>
          </div>
        </>
      )}

      {/* Step 1: Xem trước */}
      {step === 1 && (
        <>
          <div style={{ marginBottom: '12px' }}>
            <div className="segs">
              <button
                className={previewMode === '3d' ? 'on' : ''}
                onClick={() => setPreviewMode('3d')}
              >
                3D
              </button>
              <button
                className={previewMode === '2d' ? 'on' : ''}
                onClick={() => setPreviewMode('2d')}
              >
                2D
              </button>
            </div>
          </div>

          <div className="card">
            {previewMode === '3d' ? <ParkingScene3DViewer /> : <Plan2D />}
            <div className="lgd">
              {K.map((k) => (
                <span key={k}>
                  <i style={{ background: ST[k][1] }} />
                  {ST[k][0]}
                </span>
              ))}
            </div>
          </div>
          <p className="mut" style={{ marginTop: '8px' }}>
            Bố cục xem trước theo mẫu bạn đã chọn. Kiểm tra rồi tiếp tục.
          </p>
        </>
      )}

      {/* Step 2: Gửi duyệt */}
      {step === 2 && (
        <>
          <div className="card">
            <h3 style={{ marginTop: 0 }}>Kiểm tra thông tin</h3>
            <div className="tw">
              <table>
                <thead>
                  <tr>
                    <th>Mục</th>
                    <th>Giá trị</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td>Tên bãi</td>
                    <td>{data.name}</td>
                  </tr>
                  <tr>
                    <td>Địa chỉ</td>
                    <td>{data.addr}</td>
                  </tr>
                  <tr>
                    <td>GPS</td>
                    <td>{data.gps}</td>
                  </tr>
                  <tr>
                    <td>Giờ hoạt động</td>
                    <td>
                      {data.op} – {data.cl2}
                    </td>
                  </tr>
                  <tr>
                    <td>Loại xe</td>
                    <td>
                      {[data.vc ? 'Ô tô' : '', data.vm ? 'Xe máy' : '']
                        .filter(Boolean)
                        .join(', ')}
                    </td>
                  </tr>
                  <tr>
                    <td>Sức chứa</td>
                    <td>{data.cap} chỗ</td>
                  </tr>
                  <tr>
                    <td>Giá</td>
                    <td>
                      {vnd(data.pc)} / giờ (ô tô), {vnd(data.pm)} / giờ (xe máy)
                    </td>
                  </tr>
                  <tr>
                    <td>Tiền cọc</td>
                    <td>{vnd(data.dep)}</td>
                  </tr>
                  <tr>
                    <td>Đặt trước</td>
                    <td>{data.hold > 0 ? `Giữ chỗ ${data.hold} phút` : 'Không'}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          {confirming && (
            <div className="ok" style={{ marginTop: '14px' }}>
              <b>Xác nhận gửi duyệt?</b>
              <p>Sau khi gửi, bạn không thể sửa cho tới khi có kết quả.</p>
              <div style={{ display: 'flex', gap: '8px' }}>
                <button className="btn p" onClick={handleConfirmSend}>
                  Xác nhận gửi
                </button>
                <button className="btn" onClick={() => setConfirming(false)}>
                  Quay lại
                </button>
              </div>
            </div>
          )}
        </>
      )}

      {/* Action buttons */}
      <div className="ph" style={{ marginTop: '16px' }}>
        {step === 0 ? (
          <Link href="/owner/parking-lots" className="btn">
            Hủy
          </Link>
        ) : (
          <button className="btn" onClick={handleBack}>
            Quay lại
          </button>
        )}

        <button className="btn p" onClick={handleNext}>
          {step === 2 ? 'Gửi duyệt' : 'Tiếp tục'}
        </button>
      </div>
    </>
  );
}
