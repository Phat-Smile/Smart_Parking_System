'use client';

import React, { useRef, useState } from 'react';
import { useOwnerProfileStore } from './useOwnerProfileStore';
import { useAllLots } from '@/features/owner/lots/useOwnerMapStore';

export function ProfileLeftCard() {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [fileError, setFileError] = useState<string | null>(null);

  const draftProfile = useOwnerProfileStore((s) => s.draftProfile);
  const setAvatar = useOwnerProfileStore((s) => s.setAvatar);
  const lots = useAllLots();

  const totalLots = lots.length;
  const activeLots = lots.filter((l) => l.ap === 'approved').length;

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFileError(null);
    const file = e.target.files?.[0];
    if (!file) return;

    // Kiểm tra định dạng (PNG, JPG, WebP)
    const validTypes = ['image/png', 'image/jpeg', 'image/webp'];
    if (!validTypes.includes(file.type)) {
      setFileError('Chỉ nhận ảnh PNG, JPG hoặc WebP');
      e.target.value = '';
      return;
    }

    // Kiểm tra kích thước tối đa 2 MB
    if (file.size > 2 * 1024 * 1024) {
      setFileError('Ảnh tối đa 2 MB');
      e.target.value = '';
      return;
    }

    // Đọc preview bằng FileReader ở client
    const reader = new FileReader();
    reader.onload = (event) => {
      if (typeof event.target?.result === 'string') {
        setAvatar(event.target.result);
      }
    };
    reader.readAsDataURL(file);
    e.target.value = '';
  };

  const handleRemoveAvatar = () => {
    setFileError(null);
    setAvatar(null);
  };

  const initial = draftProfile.fullName.trim()
    ? draftProfile.fullName.trim().charAt(0).toUpperCase()
    : 'M';

  return (
    <div
      className="ov2-card"
      style={{
        background: '#fff',
        borderRadius: 20,
        border: '1px solid #e3e8f2',
        padding: 24,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
      }}
    >
      {/* Avatar Container */}
      <div style={{ position: 'relative', marginBottom: 16 }}>
        <div
          style={{
            width: 100,
            height: 100,
            borderRadius: '50%',
            backgroundColor: '#e3f28d',
            color: '#1a3300',
            fontWeight: 800,
            fontSize: 38,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            boxShadow: '0 4px 14px rgba(15,27,61,0.08)',
            border: '3px solid #fff',
            overflow: 'hidden',
          }}
        >
          {draftProfile.avatarUrl ? (
            /* eslint-disable-next-line @next/next/no-img-element */
            <img
              src={draftProfile.avatarUrl}
              alt="Ảnh đại diện"
              style={{ width: '100%', height: '100%', objectFit: 'cover' }}
            />
          ) : (
            initial
          )}
        </div>
      </div>

      {/* Tên & vai trò */}
      <h2 style={{ fontSize: 18, fontWeight: 800, color: '#0f1b3d', margin: '0 0 4px' }}>
        {draftProfile.fullName || 'Chủ bãi'}
      </h2>
      <div style={{ fontSize: 13, color: '#6a7491', marginBottom: 12 }}>Chủ bãi</div>

      {/* Huy hiệu xác minh */}
      <div
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          gap: 6,
          background: '#def7ec',
          color: '#0e9f6e',
          padding: '4px 12px',
          borderRadius: 999,
          fontSize: 12,
          fontWeight: 700,
          marginBottom: 16,
        }}
      >
        <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
          <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z" />
        </svg>
        Tài khoản đã xác minh
      </div>

      {/* Nút Đổi ảnh & Xóa ảnh */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 8, width: '100%' }}>
        <input
          type="file"
          ref={fileInputRef}
          onChange={handleFileChange}
          accept="image/png, image/jpeg, image/webp"
          style={{ display: 'none' }}
          aria-label="Tải lên ảnh đại diện mới"
        />
        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          style={{
            flex: 1,
            background: '#2563EB',
            color: '#fff',
            border: 'none',
            borderRadius: 10,
            padding: '8px 12px',
            fontSize: 12.5,
            fontWeight: 700,
            cursor: 'pointer',
            minHeight: 38,
          }}
        >
          Đổi ảnh
        </button>

        {draftProfile.avatarUrl && (
          <button
            type="button"
            onClick={handleRemoveAvatar}
            style={{
              background: '#fff',
              color: '#e5484d',
              border: '1px solid #fecdd3',
              borderRadius: 10,
              padding: '8px 12px',
              fontSize: 12.5,
              fontWeight: 600,
              cursor: 'pointer',
              minHeight: 38,
            }}
          >
            Xóa ảnh
          </button>
        )}
      </div>

      {fileError && (
        <div style={{ color: '#e5484d', fontSize: 12, fontWeight: 600, marginBottom: 12 }}>
          {fileError}
        </div>
      )}

      {/* Bảng số liệu thống kê */}
      <div
        style={{
          width: '100%',
          borderTop: '1px solid #f1f3f7',
          marginTop: 16,
          paddingTop: 16,
          display: 'flex',
          flexDirection: 'column',
          gap: 10,
          textAlign: 'left',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13 }}>
          <span style={{ color: '#6a7491' }}>Số bãi xe:</span>
          <strong style={{ color: '#0f1b3d' }}>{totalLots}</strong>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13 }}>
          <span style={{ color: '#6a7491' }}>Đang hoạt động:</span>
          <strong style={{ color: '#0e9f6e' }}>{activeLots}</strong>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13 }}>
          <span style={{ color: '#6a7491' }}>Tham gia từ:</span>
          <strong style={{ color: '#0f1b3d' }}>{draftProfile.joinedDate}</strong>
        </div>
      </div>
    </div>
  );
}
