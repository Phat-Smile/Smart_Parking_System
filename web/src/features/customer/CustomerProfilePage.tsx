"use client";

import { useSyncExternalStore } from "react";
import { useCustomerLanguage } from "./i18n/customerLanguage";
import Link from "next/link";
import {
    ArrowUpRight,
    Bell,
    CalendarDays,
    Mail,
    MapPin,
    Pencil,
    Phone,
    ShieldCheck,
    UserRound,
    Wallet,
} from "lucide-react";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";

function ProfileField({
    icon: Icon,
    label,
    value,
}: {
    icon: typeof UserRound;
    label: string;
    value: string;
}) {
    return (
        <div className="customer-profile-field">
            <span className="customer-profile-field-icon" aria-hidden="true">
                <Icon size={20} strokeWidth={1.8} />
            </span>
            <div>
                <span>{label}</span>
                <strong>{value}</strong>
            </div>
        </div>
    );
}

export function CustomerProfilePage() {
    const { copy } = useCustomerLanguage();
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );
    const name =
        session?.displayName?.trim() || copy("Not provided", "Chưa cung cấp");
    const email =
        session?.account.email?.trim() || copy("Not provided", "Chưa cung cấp");
    const initial = !session?.displayName?.trim() ? (
        <UserRound size={36} />
    ) : (
        name[0].toUpperCase()
    );

    return (
        <div className="customer-account-content">
            <div className="customer-account-heading">
                <div>
                    <span className="customer-account-eyebrow">
                        {copy("PARKWISE ACCOUNT", "TÀI KHOẢN PARKWISE")}
                    </span>
                    <h1>{copy("My Profile", "Hồ sơ của tôi")}</h1>
                    <p>
                        {copy(
                            "Review your personal details and continue updating your profile.",
                            "Xem thông tin cá nhân và tiếp tục cập nhật hồ sơ.",
                        )}
                    </p>
                </div>
                <Link
                    className="customer-account-primary"
                    href="/customer/complete-profile"
                >
                    <Pencil size={18} aria-hidden="true" />
                    {copy("Edit profile", "Chỉnh sửa hồ sơ")}
                </Link>
            </div>

            <section
                className="customer-profile-hero"
                aria-label={copy("Profile overview", "Tổng quan hồ sơ")}
            >
                <div className="customer-profile-avatar" aria-hidden="true">
                    {initial}
                </div>
                <div className="customer-profile-hero-copy">
                    <span className="customer-profile-kicker">
                        {copy("YOUR INFORMATION", "THÔNG TIN CỦA BẠN")}
                    </span>
                    <h2>{name}</h2>
                    <p>{email}</p>
                </div>
                <span className="customer-profile-status">
                    {session
                        ? copy("Customer account", "Tài khoản khách hàng")
                        : copy("Preview", "Xem trước")}
                </span>
            </section>

            <div className="customer-profile-grid">
                <section
                    className="customer-account-card"
                    aria-labelledby="profile-details-title"
                >
                    <div className="customer-account-card-heading">
                        <div>
                            <span className="customer-account-eyebrow">
                                {copy("DETAILS", "CHI TIẾT")}
                            </span>
                            <h2 id="profile-details-title">
                                {copy(
                                    "Personal information",
                                    "Thông tin cá nhân",
                                )}
                            </h2>
                        </div>
                        <Link
                            href="/customer/complete-profile"
                            aria-label={copy(
                                "Edit personal information",
                                "Chỉnh sửa thông tin cá nhân",
                            )}
                        >
                            <Pencil size={18} />
                        </Link>
                    </div>
                    <div className="customer-profile-fields">
                        <ProfileField
                            icon={UserRound}
                            label={copy("Full name", "Họ và tên")}
                            value={name}
                        />
                        <ProfileField icon={Mail} label="Email" value={email} />
                        <ProfileField
                            icon={Phone}
                            label={copy("Phone number", "Số điện thoại")}
                            value={
                                session?.account.phoneNumber?.trim() ||
                                copy("Not provided", "Chưa cung cấp")
                            }
                        />
                        <ProfileField
                            icon={CalendarDays}
                            label={copy("Date of birth", "Ngày sinh")}
                            value={copy("Not provided", "Chưa cung cấp")}
                        />
                        <ProfileField
                            icon={MapPin}
                            label={copy("Address", "Địa chỉ")}
                            value={copy("Not provided", "Chưa cung cấp")}
                        />
                    </div>
                </section>

                <aside className="customer-profile-side">
                    <section className="customer-account-card customer-profile-note">
                        <span className="customer-profile-note-icon">
                            <ShieldCheck size={22} />
                        </span>
                        <h2>
                            {copy("Profile information", "Thông tin hồ sơ")}
                        </h2>
                        <p>
                            {copy(
                                "This page shows your current account details. More profile fields will appear when profile syncing is available.",
                                "Trang này hiển thị thông tin tài khoản hiện có. Các trường khác sẽ xuất hiện khi chức năng đồng bộ hồ sơ sẵn sàng.",
                            )}
                        </p>
                    </section>
                    <section
                        className="customer-account-card customer-profile-shortcuts"
                        aria-label={copy(
                            "Account shortcuts",
                            "Lối tắt tài khoản",
                        )}
                    >
                        <h2>{copy("Account settings", "Cài đặt tài khoản")}</h2>
                        <Link href="/settings/security">
                            <ShieldCheck size={19} />{" "}
                            {copy("Security", "Bảo mật")}{" "}
                            <ArrowUpRight size={17} />
                        </Link>
                        <Link href="/customer/notifications">
                            <Bell size={19} />{" "}
                            {copy("Notifications", "Thông báo")}{" "}
                            <ArrowUpRight size={17} />
                        </Link>
                        <Link href="/customer/wallet">
                            <Wallet size={19} />{" "}
                            {copy("My Wallet", "Ví của tôi")}{" "}
                            <ArrowUpRight size={17} />
                        </Link>
                    </section>
                </aside>
            </div>
        </div>
    );
}
