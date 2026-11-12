"use client";

import React, { useEffect, useSyncExternalStore } from "react";
import { OwnerPageHeader } from "@/components/layout/OwnerPageHeader";
import { ProfileLeftCard } from "@/features/owner/profile/ProfileLeftCard";
import { ProfilePersonalInfo } from "@/features/owner/profile/ProfilePersonalInfo";
import { ProfileNotificationSettings } from "@/features/owner/profile/ProfileNotificationSettings";
import { ProfilePasswordChange } from "@/features/owner/profile/ProfilePasswordChange";
import { ProfileStickyBar } from "@/features/owner/profile/ProfileStickyBar";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { getMyOwnerApplication } from "@/features/owner/api/ownerLegalApi";
import { useOwnerProfileStore } from "@/features/owner/profile/useOwnerProfileStore";

export default function OwnerProfilePage() {
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );

    useEffect(() => {
        let active = true;
        getMyOwnerApplication()
            .then((app) => {
                if (!active || !app) return;
                const setDraftField =
                    useOwnerProfileStore.getState().setDraftField;
                const saveProfile = useOwnerProfileStore.getState().saveProfile;

                const name = app.fullName || session?.displayName || "";
                if (name) setDraftField("fullName", name);

                const mail = app.email || session?.account?.email || "";
                if (mail) setDraftField("email", mail);

                if (app.phoneNumber) setDraftField("phone", app.phoneNumber);
                if (app.businessName)
                    setDraftField("businessName", app.businessName);
                if (app.taxCode) setDraftField("taxCode", app.taxCode);
                if (app.permanentAddress)
                    setDraftField("address", app.permanentAddress);
                if (app.parkingAddress)
                    setDraftField("businessAddress", app.parkingAddress);
                if (app.reviewedAtUtc) {
                    setDraftField(
                        "joinedDate",
                        new Date(app.reviewedAtUtc).toLocaleDateString(
                            "vi-VN",
                            {
                                month: "long",
                                year: "numeric",
                            },
                        ),
                    );
                }
                saveProfile();
            })
            .catch(() => undefined);
        return () => {
            active = false;
        };
    }, [session]);

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
