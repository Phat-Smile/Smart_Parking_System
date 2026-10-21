'use client';

import React from 'react';
import { useOwnerProfileStore } from './useOwnerProfileStore';

export function ProfilePersonalInfo() {
  const draftProfile = useOwnerProfileStore((s) => s.draftProfile);
  const setDraftField = useOwnerProfileStore((s) => s.setDraftField);
  const errors = useOwnerProfileStore((s) => s.errors);

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
      {/* ── 1. Thông tin cá nhân ── */}
      <h3 style={{ fontSize: 16, fontWeight: 800, color: '#0f1b3d', margin: '0 0 16px' }}>
        Thông tin cá nhân
      </h3>

      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
          gap: 16,
          marginBottom: 28,
        }}
      >
        {/* Họ và tên */}
        <div>
          <label
            htmlFor="profile-fullName"
            style={{ display: 'block', fontSize: 13, fontWeight: 700, color: '#0f1b3d', marginBottom: 6 }}
          >
            Họ và tên <span style={{ color: '#e5484d' }}>*</span>
          </label>
          <input
            id="profile-fullName"
            type="text"
            value={draftProfile.fullName}
            onChange={(e) => setDraftField('fullName', e.target.value)}
            aria-invalid={Boolean(errors.fullName)}
            aria-describedby={errors.fullName ? 'error-fullName' : undefined}
            style={{
              width: '100%',
              padding: '10px 14px',
              borderRadius: 10,
              border: errors.fullName ? '1.5px solid #e5484d' : '1px solid #e3e8f2',
              background: '#f8faff',
              fontSize: 13.5,
              color: '#0f1b3d',
              outline: 'none',
              minHeight: 44,
            }}
          />
          {errors.fullName && (
            <div id="error-fullName" style={{ color: '#e5484d', fontSize: 12, marginTop: 4, fontWeight: 500 }}>
              {errors.fullName}
            </div>
          )}
        </div>

        {/* Số điện thoại */}
        <div>
          <label
            htmlFor="profile-phone"
            style={{ display: 'block', fontSize: 13, fontWeight: 700, color: '#0f1b3d', marginBottom: 6 }}
          >
            Số điện thoại <span style={{ color: '#e5484d' }}>*</span>
          </label>
          <input
            id="profile-phone"
            type="tel"
            value={draftProfile.phone}
            onChange={(e) => setDraftField('phone', e.target.value)}
            aria-invalid={Boolean(errors.phone)}
            aria-describedby={errors.phone ? 'error-phone' : undefined}
            style={{
              width: '100%',
              padding: '10px 14px',
              borderRadius: 10,
              border: errors.phone ? '1.5px solid #e5484d' : '1px solid #e3e8f2',
              background: '#f8faff',
              fontSize: 13.5,
              color: '#0f1b3d',
              outline: 'none',
              minHeight: 44,
            }}
          />
          {errors.phone && (
            <div id="error-phone" style={{ color: '#e5484d', fontSize: 12, marginTop: 4, fontWeight: 500 }}>
              {errors.phone}
            </div>
          )}
        </div>

        {/* Email */}
        <div>
          <label
            htmlFor="profile-email"
            style={{ display: 'block', fontSize: 13, fontWeight: 700, color: '#0f1b3d', marginBottom: 6 }}
          >
            Email <span style={{ color: '#e5484d' }}>*</span>
          </label>
          <input
            id="profile-email"
            type="email"
            value={draftProfile.email}
            onChange={(e) => setDraftField('email', e.target.value)}
            aria-invalid={Boolean(errors.email)}
            aria-describedby={errors.email ? 'error-email' : undefined}
            style={{
              width: '100%',
              padding: '10px 14px',
              borderRadius: 10,
              border: errors.email ? '1.5px solid #e5484d' : '1px solid #e3e8f2',
              background: '#f8faff',
              fontSize: 13.5,
              color: '#0f1b3d',
              outline: 'none',
              minHeight: 44,
            }}
          />
          {errors.email && (
            <div id="error-email" style={{ color: '#e5484d', fontSize: 12, marginTop: 4, fontWeight: 500 }}>
              {errors.email}
            </div>
          )}
        </div>

        {/* Địa chỉ liên hệ */}
        <div>
          <label
            htmlFor="profile-address"
            style={{ display: 'block', fontSize: 13, fontWeight: 700, color: '#0f1b3d', marginBottom: 6 }}
          >
            Địa chỉ liên hệ
          </label>
          <input
            id="profile-address"
            type="text"
            value={draftProfile.address}
            onChange={(e) => setDraftField('address', e.target.value)}
            style={{
              width: '100%',
              padding: '10px 14px',
              borderRadius: 10,
              border: '1px solid #e3e8f2',
              background: '#f8faff',
              fontSize: 13.5,
              color: '#0f1b3d',
              outline: 'none',
              minHeight: 44,
            }}
          />
        </div>
      </div>

      {/* ── 2. Thông tin doanh nghiệp ── */}
      <h3 style={{ fontSize: 16, fontWeight: 800, color: '#0f1b3d', margin: '0 0 16px' }}>
        Thông tin doanh nghiệp
      </h3>

      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
          gap: 16,
        }}
      >
        {/* Tên doanh nghiệp/đơn vị */}
        <div>
          <label
            htmlFor="profile-businessName"
            style={{ display: 'block', fontSize: 13, fontWeight: 700, color: '#0f1b3d', marginBottom: 6 }}
          >
            Tên doanh nghiệp / đơn vị <span style={{ color: '#e5484d' }}>*</span>
          </label>
          <input
            id="profile-businessName"
            type="text"
            value={draftProfile.businessName}
            onChange={(e) => setDraftField('businessName', e.target.value)}
            aria-invalid={Boolean(errors.businessName)}
            aria-describedby={errors.businessName ? 'error-businessName' : undefined}
            style={{
              width: '100%',
              padding: '10px 14px',
              borderRadius: 10,
              border: errors.businessName ? '1.5px solid #e5484d' : '1px solid #e3e8f2',
              background: '#f8faff',
              fontSize: 13.5,
              color: '#0f1b3d',
              outline: 'none',
              minHeight: 44,
            }}
          />
          {errors.businessName && (
            <div id="error-businessName" style={{ color: '#e5484d', fontSize: 12, marginTop: 4, fontWeight: 500 }}>
              {errors.businessName}
            </div>
          )}
        </div>

        {/* Mã số thuế */}
        <div>
          <label
            htmlFor="profile-taxCode"
            style={{ display: 'block', fontSize: 13, fontWeight: 700, color: '#0f1b3d', marginBottom: 6 }}
          >
            Mã số thuế (MST)
          </label>
          <input
            id="profile-taxCode"
            type="text"
            placeholder="Ví dụ: 0312345678"
            value={draftProfile.taxCode}
            onChange={(e) => setDraftField('taxCode', e.target.value)}
            aria-invalid={Boolean(errors.taxCode)}
            aria-describedby={errors.taxCode ? 'error-taxCode' : undefined}
            style={{
              width: '100%',
              padding: '10px 14px',
              borderRadius: 10,
              border: errors.taxCode ? '1.5px solid #e5484d' : '1px solid #e3e8f2',
              background: '#f8faff',
              fontSize: 13.5,
              color: '#0f1b3d',
              outline: 'none',
              minHeight: 44,
            }}
          />
          {errors.taxCode && (
            <div id="error-taxCode" style={{ color: '#e5484d', fontSize: 12, marginTop: 4, fontWeight: 500 }}>
              {errors.taxCode}
            </div>
          )}
        </div>

        {/* Địa chỉ đăng ký kinh doanh */}
        <div style={{ gridColumn: '1 / -1' }}>
          <label
            htmlFor="profile-businessAddress"
            style={{ display: 'block', fontSize: 13, fontWeight: 700, color: '#0f1b3d', marginBottom: 6 }}
          >
            Địa chỉ đăng ký kinh doanh
          </label>
          <input
            id="profile-businessAddress"
            type="text"
            value={draftProfile.businessAddress}
            onChange={(e) => setDraftField('businessAddress', e.target.value)}
            style={{
              width: '100%',
              padding: '10px 14px',
              borderRadius: 10,
              border: '1px solid #e3e8f2',
              background: '#f8faff',
              fontSize: 13.5,
              color: '#0f1b3d',
              outline: 'none',
              minHeight: 44,
            }}
          />
        </div>
      </div>
    </div>
  );
}
