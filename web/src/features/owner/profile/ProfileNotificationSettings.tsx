'use client';

import React from 'react';
import { useNotificationStore } from '@/features/owner/notifications/useNotificationStore';
import { NotificationCategory } from '@/features/owner/notifications/notificationTypes';

interface CategorySettingItem {
  key: NotificationCategory;
  label: string;
  description: string;
}

const CATEGORY_ITEMS: CategorySettingItem[] = [
  {
    key: 'lots',
    label: 'Bãi của tôi',
    description: 'Thông báo về phê duyệt bãi xe, cập nhật trạng thái hoạt động và cấu hình',
  },
  {
    key: 'reservations',
    label: 'Đặt chỗ',
    description: 'Thông báo khách đặt chỗ mới, hoàn tất thanh toán hoặc hủy lượt giữ chỗ',
  },
  {
    key: 'sessions',
    label: 'Phiên đỗ xe',
    description: 'Thông báo xe vào bãi, xe rời bãi và cảnh báo đỗ quá giờ',
  },
  {
    key: 'layout',
    label: 'Sơ đồ',
    description: 'Thông báo cập nhật sơ đồ 2D/3D và cảnh báo ô đỗ hỏng/bảo trì',
  },
  {
    key: 'reports',
    label: 'Báo cáo',
    description: 'Báo cáo doanh thu định kỳ, tổng kết ngày/tháng và đối soát',
  },
  {
    key: 'system',
    label: 'Hệ thống',
    description: 'Cập nhật bảo trì hệ thống ParkWise và các chính sách vận hành mới',
  },
];

export function ProfileNotificationSettings() {
  const { categoryPreferences, setCategoryPreference } = useNotificationStore();

  return (
    <div
      className="ov2-card"
      style={{
        background: '#fff',
        borderRadius: 20,
        border: '1px solid #e3e8f2',
        padding: 24,
        marginBottom: 20,
      }}
    >
      <div style={{ marginBottom: 18 }}>
        <h3 style={{ fontSize: 16, fontWeight: 800, color: '#0f1b3d', margin: '0 0 4px' }}>
          Nhận thông báo theo mục
        </h3>
        <p style={{ fontSize: 13, color: '#6a7491', margin: 0 }}>
          Tùy chỉnh các loại thông báo bạn muốn nhận tại chuông thông báo. Tùy chọn có hiệu lực ngay lập tức.
        </p>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
        {CATEGORY_ITEMS.map((item) => {
          const isEnabled = categoryPreferences ? categoryPreferences[item.key] !== false : true;

          return (
            <div
              key={item.key}
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                padding: '12px 16px',
                borderRadius: 12,
                background: '#f8faff',
                border: '1px solid #edf2f7',
                gap: 16,
              }}
            >
              <div>
                <div style={{ fontSize: 14, fontWeight: 700, color: '#0f1b3d', marginBottom: 2 }}>
                  {item.label}
                </div>
                <div style={{ fontSize: 12.5, color: '#6a7491', lineHeight: 1.4 }}>
                  {item.description}
                </div>
              </div>

              {/* Switch Toggle */}
              <button
                type="button"
                role="switch"
                aria-checked={isEnabled}
                aria-label={`Nhận thông báo mục ${item.label}`}
                onClick={() => setCategoryPreference(item.key, !isEnabled)}
                style={{
                  position: 'relative',
                  width: 46,
                  height: 26,
                  borderRadius: 999,
                  background: isEnabled ? '#2563EB' : '#cbd5e1',
                  border: 'none',
                  cursor: 'pointer',
                  flexShrink: 0,
                  transition: 'background-color 0.2s ease',
                  padding: 0,
                  outline: 'none',
                }}
              >
                <span
                  style={{
                    position: 'absolute',
                    top: 3,
                    left: isEnabled ? 23 : 3,
                    width: 20,
                    height: 20,
                    borderRadius: '50%',
                    background: '#ffffff',
                    boxShadow: '0 2px 4px rgba(0,0,0,0.2)',
                    transition: 'left 0.2s ease',
                  }}
                />
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
}
