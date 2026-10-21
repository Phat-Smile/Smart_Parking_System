'use client';

import React, { useState } from 'react';
import { useOwnerProfileStore } from './useOwnerProfileStore';

export function ProfileStickyBar() {
  const { draftProfile, isDirty, setErrors, clearErrors, saveProfile, resetDraft } = useOwnerProfileStore();
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const validateAndSave = () => {
    clearErrors();
    const errs: Record<string, string> = {};

    // Họ tên tối thiểu 2 ký tự
    const name = draftProfile.fullName.trim();
    if (!name || name.length < 2) {
      errs.fullName = 'Họ và tên tối thiểu 2 ký tự';
    }

    // Số điện thoại ^(0|\+84)\d{9}$ sau khi bỏ khoảng trắng, dấu chấm, gạch ngang
    const cleanPhone = draftProfile.phone.replace(/[\s.-]/g, '');
    const phoneRegex = /^(0|\+84)\d{9}$/;
    if (!cleanPhone || !phoneRegex.test(cleanPhone)) {
      errs.phone = 'Số điện thoại không hợp lệ (ví dụ: 0912345678 hoặc +84912345678)';
    }

    // Email hợp lệ
    const cleanEmail = draftProfile.email.trim();
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!cleanEmail || !emailRegex.test(cleanEmail)) {
      errs.email = 'Email không hợp lệ';
    }

    // Mã số thuế: để trống được, nếu có phải 10 chữ số hoặc 13 chữ số (cho phép gạch ngang trước 3 số cuối)
    const rawTax = draftProfile.taxCode.trim();
    if (rawTax) {
      const cleanTax = rawTax.replace(/-/g, '');
      if (!/^\d{10}$/.test(cleanTax) && !/^\d{13}$/.test(cleanTax)) {
        errs.taxCode = 'Mã số thuế phải gồm 10 hoặc 13 chữ số';
      }
    }

    // Tên doanh nghiệp bắt buộc
    if (!draftProfile.businessName.trim()) {
      errs.businessName = 'Tên doanh nghiệp là bắt buộc';
    }

    if (Object.keys(errs).length > 0) {
      setErrors(errs);
      return;
    }

    // Lưu thành công
    saveProfile();
    setToastMessage('Đã lưu hồ sơ');
    setTimeout(() => setToastMessage(null), 3000);
  };

  return (
    <>
      <div
        className="ov2-profile-sticky-bar"
        style={{
          position: 'sticky',
          bottom: 16,
          zIndex: 50,
          background: 'rgba(255, 255, 255, 0.95)',
          backdropFilter: 'blur(12px)',
          border: '1.5px solid #e3e8f2',
          borderRadius: 18,
          padding: '14px 24px',
          boxShadow: '0 12px 32px rgba(15, 27, 61, 0.14)',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: 12,
          marginTop: 24,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <span
            style={{
              width: 9,
              height: 9,
              borderRadius: '50%',
              backgroundColor: isDirty ? '#f59e0b' : '#10b981',
            }}
          />
          <span style={{ fontSize: 13.5, fontWeight: 600, color: '#0f1b3d' }}>
            {isDirty ? 'Bạn có thay đổi chưa lưu' : 'Chưa có thay đổi'}
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <button
            type="button"
            onClick={resetDraft}
            disabled={!isDirty}
            style={{
              background: '#f3f6fb',
              color: isDirty ? '#0f1b3d' : '#94a3b8',
              border: '1px solid #e3e8f2',
              borderRadius: 10,
              padding: '10px 18px',
              fontSize: 13,
              fontWeight: 600,
              cursor: isDirty ? 'pointer' : 'not-allowed',
              opacity: isDirty ? 1 : 0.6,
              minHeight: 44,
              transition: 'all 0.15s ease',
            }}
          >
            Hoàn tác
          </button>

          <button
            type="button"
            onClick={validateAndSave}
            disabled={!isDirty}
            style={{
              background: isDirty ? '#2563EB' : '#cbd5e1',
              color: '#ffffff',
              border: 'none',
              borderRadius: 10,
              padding: '10px 22px',
              fontSize: 13,
              fontWeight: 700,
              cursor: isDirty ? 'pointer' : 'not-allowed',
              minHeight: 44,
              boxShadow: isDirty ? '0 4px 12px rgba(37,99,235,0.25)' : 'none',
              transition: 'all 0.15s ease',
            }}
          >
            Lưu thay đổi
          </button>
        </div>
      </div>

      {/* Floating Toast */}
      {toastMessage && (
        <div
          role="status"
          aria-live="polite"
          style={{
            position: 'fixed',
            bottom: 84,
            right: 32,
            background: '#0f1b3d',
            color: '#fff',
            padding: '12px 24px',
            borderRadius: 12,
            fontSize: 13.5,
            fontWeight: 700,
            boxShadow: '0 12px 32px rgba(15,27,61,0.25)',
            zIndex: 9999,
          }}
        >
          {toastMessage}
        </div>
      )}
    </>
  );
}
