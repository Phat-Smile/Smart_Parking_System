'use client';

import React, { useState } from 'react';
import { useRouter } from 'next/navigation';
import { changePassword } from '@/features/auth/changePassword';
import { logoutAllParkWiseAccount } from '@/features/auth/logoutParkWiseAccount';
import { ApiError } from '@/shared/errors/api-error';

export function ProfilePasswordChange() {
  const router = useRouter();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  const [errors, setErrors] = useState<{
    currentPassword?: string;
    newPassword?: string;
    confirmPassword?: string;
  }>({});
  const [apiError, setApiError] = useState<string | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isLoggingOutAll, setIsLoggingOutAll] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setApiError(null);
    const newErrors: typeof errors = {};

    if (!currentPassword) {
      newErrors.currentPassword = 'Vui lòng nhập mật khẩu hiện tại';
    }

    if (!newPassword) {
      newErrors.newPassword = 'Vui lòng nhập mật khẩu mới';
    } else {
      const hasLetter = /[a-zA-Z]/.test(newPassword);
      const hasNumber = /[0-9]/.test(newPassword);
      if (newPassword.length < 8 || !hasLetter || !hasNumber) {
        newErrors.newPassword = 'Mật khẩu mới tối thiểu 8 ký tự, gồm cả chữ và số';
      } else if (newPassword === currentPassword) {
        newErrors.newPassword = 'Mật khẩu mới phải khác mật khẩu hiện tại';
      }
    }

    if (!confirmPassword) {
      newErrors.confirmPassword = 'Vui lòng xác nhận mật khẩu mới';
    } else if (confirmPassword !== newPassword) {
      newErrors.confirmPassword = 'Mật khẩu xác nhận không khớp';
    }

    setErrors(newErrors);
    if (Object.keys(newErrors).length > 0) return;

    setIsSubmitting(true);
    try {
      await changePassword(currentPassword, newPassword);
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      setErrors({});
      setToastMessage('Đã đổi mật khẩu thành công!');
      setTimeout(() => setToastMessage(null), 3000);
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setApiError(err.message || 'Mật khẩu hiện tại không chính xác hoặc vi phạm chính sách bảo mật.');
      } else {
        setApiError('Đã có lỗi xảy ra khi đổi mật khẩu.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleLogoutAll = async () => {
    if (!window.confirm('Bạn có chắc chắn muốn đăng xuất khỏi tất cả các thiết bị?')) {
      return;
    }
    setIsLoggingOutAll(true);
    try {
      await logoutAllParkWiseAccount();
      router.push('/login');
    } catch {
      router.push('/login');
    }
  };

  return (
    <div
      className="ov2-card"
      style={{
        background: '#fff',
        borderRadius: 20,
        border: '1px solid #e3e8f2',
        padding: 24,
        marginBottom: 20,
        position: 'relative',
      }}
    >
      <h3 style={{ fontSize: 16, fontWeight: 800, color: '#0f1b3d', margin: '0 0 16px' }}>
        Đổi mật khẩu
      </h3>

      <form onSubmit={handleSubmit} noValidate>
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
            gap: 16,
            marginBottom: 20,
          }}
        >
          {/* Mật khẩu hiện tại */}
          <div>
            <label
              htmlFor="current-password"
              style={{ display: 'block', fontSize: 13, fontWeight: 700, color: '#0f1b3d', marginBottom: 6 }}
            >
              Mật khẩu hiện tại <span style={{ color: '#e5484d' }}>*</span>
            </label>
            <input
              id="current-password"
              type="password"
              autoComplete="current-password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              aria-invalid={Boolean(errors.currentPassword)}
              aria-describedby={errors.currentPassword ? 'err-curr-pass' : undefined}
              style={{
                width: '100%',
                padding: '10px 14px',
                borderRadius: 10,
                border: errors.currentPassword ? '1.5px solid #e5484d' : '1px solid #e3e8f2',
                background: '#f8faff',
                fontSize: 13.5,
                color: '#0f1b3d',
                outline: 'none',
                minHeight: 44,
              }}
            />
            {errors.currentPassword && (
              <div id="err-curr-pass" style={{ color: '#e5484d', fontSize: 12, marginTop: 4, fontWeight: 500 }}>
                {errors.currentPassword}
              </div>
            )}
          </div>

          {/* Mật khẩu mới */}
          <div>
            <label
              htmlFor="new-password"
              style={{ display: 'block', fontSize: 13, fontWeight: 700, color: '#0f1b3d', marginBottom: 6 }}
            >
              Mật khẩu mới <span style={{ color: '#e5484d' }}>*</span>
            </label>
            <input
              id="new-password"
              type="password"
              autoComplete="new-password"
              placeholder="Tối thiểu 8 ký tự, gồm chữ và số"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              aria-invalid={Boolean(errors.newPassword)}
              aria-describedby={errors.newPassword ? 'err-new-pass' : undefined}
              style={{
                width: '100%',
                padding: '10px 14px',
                borderRadius: 10,
                border: errors.newPassword ? '1.5px solid #e5484d' : '1px solid #e3e8f2',
                background: '#f8faff',
                fontSize: 13.5,
                color: '#0f1b3d',
                outline: 'none',
                minHeight: 44,
              }}
            />
            {errors.newPassword && (
              <div id="err-new-pass" style={{ color: '#e5484d', fontSize: 12, marginTop: 4, fontWeight: 500 }}>
                {errors.newPassword}
              </div>
            )}
          </div>

          {/* Nhập lại mật khẩu mới */}
          <div>
            <label
              htmlFor="confirm-password"
              style={{ display: 'block', fontSize: 13, fontWeight: 700, color: '#0f1b3d', marginBottom: 6 }}
            >
              Nhập lại mật khẩu mới <span style={{ color: '#e5484d' }}>*</span>
            </label>
            <input
              id="confirm-password"
              type="password"
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              aria-invalid={Boolean(errors.confirmPassword)}
              aria-describedby={errors.confirmPassword ? 'err-conf-pass' : undefined}
              style={{
                width: '100%',
                padding: '10px 14px',
                borderRadius: 10,
                border: errors.confirmPassword ? '1.5px solid #e5484d' : '1px solid #e3e8f2',
                background: '#f8faff',
                fontSize: 13.5,
                color: '#0f1b3d',
                outline: 'none',
                minHeight: 44,
              }}
            />
            {errors.confirmPassword && (
              <div id="err-conf-pass" style={{ color: '#e5484d', fontSize: 12, marginTop: 4, fontWeight: 500 }}>
                {errors.confirmPassword}
              </div>
            )}
          </div>
        </div>

        {apiError && (
          <div
            role="alert"
            style={{
              padding: '10px 14px',
              borderRadius: 10,
              background: '#fde8e8',
              border: '1px solid #fbd5d5',
              color: '#9b1c1c',
              fontSize: 13,
              fontWeight: 600,
              marginBottom: 16,
            }}
          >
            ⚠️ {apiError}
          </div>
        )}

        <button
          type="submit"
          disabled={isSubmitting}
          style={{
            background: '#0f1b3d',
            color: '#fff',
            border: 'none',
            borderRadius: 10,
            padding: '10px 20px',
            fontSize: 13,
            fontWeight: 700,
            cursor: isSubmitting ? 'not-allowed' : 'pointer',
            opacity: isSubmitting ? 0.7 : 1,
            minHeight: 44,
          }}
        >
          {isSubmitting ? 'Đang cập nhật...' : 'Cập nhật mật khẩu'}
        </button>
      </form>

      {/* Toast Notification */}
      {toastMessage && (
        <div
          role="status"
          aria-live="polite"
          style={{
            position: 'absolute',
            bottom: 20,
            right: 24,
            background: '#0f1b3d',
            color: '#fff',
            padding: '10px 18px',
            borderRadius: 10,
            fontSize: 13,
            fontWeight: 700,
            boxShadow: '0 8px 24px rgba(15,27,61,0.2)',
          }}
        >
          {toastMessage}
        </div>
      )}

      {/* Session Security Card - US-AUTH-005 */}
      <div
        style={{
          marginTop: 24,
          paddingTop: 20,
          borderTop: '1px solid #e3e8f2',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: 16,
        }}
      >
        <div>
          <div style={{ fontSize: 14, fontWeight: 700, color: '#0f1b3d' }}>
            Phiên đăng nhập & Thiết bị
          </div>
          <div style={{ fontSize: 12.5, color: '#6a7491', marginTop: 2 }}>
            Đăng xuất khỏi tất cả thiết bị khác đang lưu tài khoản của bạn
          </div>
        </div>
        <button
          type="button"
          onClick={handleLogoutAll}
          disabled={isLoggingOutAll}
          style={{
            background: '#fff',
            color: '#e5484d',
            border: '1.5px solid #fbd5d5',
            borderRadius: 10,
            padding: '8px 16px',
            fontSize: 12.5,
            fontWeight: 700,
            cursor: isLoggingOutAll ? 'not-allowed' : 'pointer',
            opacity: isLoggingOutAll ? 0.6 : 1,
            transition: 'all 0.15s ease',
          }}
        >
          {isLoggingOutAll ? 'Đang xử lý...' : '🚪 Đăng xuất tất cả thiết bị'}
        </button>
      </div>
    </div>
  );
}

