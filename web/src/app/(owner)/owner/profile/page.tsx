"use client";

import React from "react";
import { OwnerPageHeader } from "@/components/layout/OwnerPageHeader";
import { ProfileLeftCard } from "@/features/owner/profile/ProfileLeftCard";
import { ProfilePersonalInfo } from "@/features/owner/profile/ProfilePersonalInfo";
import { ProfileNotificationSettings } from "@/features/owner/profile/ProfileNotificationSettings";
import { ProfilePasswordChange } from "@/features/owner/profile/ProfilePasswordChange";
import { ProfileStickyBar } from "@/features/owner/profile/ProfileStickyBar";

export default function OwnerProfilePage() {
    return (
        <div style={{ maxWidth: 1400, margin: "0 auto", paddingBottom: 64 }}>
            <OwnerPageHeader
                title="Hồ sơ chủ bãi"
                subtitle="Cập nhật thông tin cá nhân, doanh nghiệp và thông báo"
            />

            {/* 2-Column Responsive Layout */}
            <div
                className="ov2-profile-layout"
                style={{
                    display: "grid",
                    gridTemplateColumns: "280px minmax(0, 1fr)",
                    gap: 24,
                    alignItems: "start",
                }}
            >
                {/* Cột trái: Thẻ tóm tắt thông tin & ảnh đại diện */}
                <aside>
                    <ProfileLeftCard />
                </aside>

                {/* Cột phải: Thông tin cá nhân, Doanh nghiệp, Cài đặt thông báo, Đổi mật khẩu & Thanh lưu */}
                <main>
                    <ProfilePersonalInfo />
                    <ProfileNotificationSettings />
                    <ProfilePasswordChange />
                    <ProfileStickyBar />
                </main>
            </div>

            <style jsx>{`
                @media (max-width: 900px) {
                    .ov2-profile-layout {
                        grid-template-columns: 1fr !important;
                    }
                }
            `}</style>
        </div>
    );
}
