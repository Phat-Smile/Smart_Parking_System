"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
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
    Loader2,
} from "lucide-react";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import {
    deleteCustomerMedia,
    deleteMyProfileIdentity,
    getCustomerMediaBlob,
    getMyProfile,
    saveMyProfile,
    type CustomerProfile,
} from "./customerProfileApi";

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

    const [profile, setProfile] = useState<CustomerProfile | null>(null);
    const [loading, setLoading] = useState(true);
    const [avatarObjectUrl, setAvatarObjectUrl] = useState<string | null>(null);
    const [privacyBusy, setPrivacyBusy] = useState(false);
    const [privacyMessage, setPrivacyMessage] = useState("");

    useEffect(() => {
        let active = true;
        async function fetchProfile() {
            try {
                const data = await getMyProfile();
                if (active) setProfile(data);
            } catch {
                // If profile is not found or fails, fallback to session data
            } finally {
                if (active) setLoading(false);
            }
        }
        void fetchProfile();
        return () => {
            active = false;
        };
    }, []);

    useEffect(() => {
        const mediaUrl = profile?.avatarUrl;
        if (!mediaUrl?.startsWith("/api/identity/accounts/me/media/")) return;
        let active = true;
        let objectUrl: string | null = null;
        void getCustomerMediaBlob(mediaUrl)
            .then((blob) => {
                objectUrl = URL.createObjectURL(blob);
                if (active) setAvatarObjectUrl(objectUrl);
                else URL.revokeObjectURL(objectUrl);
            })
            .catch(() => {
                if (active) setAvatarObjectUrl(null);
            });
        return () => {
            active = false;
            if (objectUrl) URL.revokeObjectURL(objectUrl);
        };
    }, [profile?.avatarUrl]);

    async function removeIdentity() {
        if (
            !window.confirm(
                copy(
                    "Delete your saved identity details and documents?",
                    "Xóa thông tin và giấy tờ định danh đã lưu?",
                ),
            )
        )
            return;
        setPrivacyBusy(true);
        setPrivacyMessage("");
        try {
            await deleteMyProfileIdentity();
            setProfile(await getMyProfile());
            setPrivacyMessage(
                copy(
                    "Identity details deleted.",
                    "Đã xóa thông tin định danh.",
                ),
            );
        } catch {
            setPrivacyMessage(
                copy(
                    "Could not delete identity details.",
                    "Không thể xóa thông tin định danh.",
                ),
            );
        } finally {
            setPrivacyBusy(false);
        }
    }

    async function removeAvatar() {
        if (
            !profile?.avatarUrl ||
            !window.confirm(
                copy("Remove your profile photo?", "Xóa ảnh đại diện?"),
            )
        )
            return;
        const mediaId = profile.avatarUrl.split("/").pop();
        if (
            !mediaId ||
            !profile.avatarUrl.startsWith("/api/identity/accounts/me/media/")
        )
            return;
        setPrivacyBusy(true);
        setPrivacyMessage("");
        try {
            await saveMyProfile({ ...profile, avatarUrl: null });
            await deleteCustomerMedia(mediaId);
            setProfile(await getMyProfile());
            setAvatarObjectUrl(null);
            setPrivacyMessage(
                copy("Profile photo removed.", "Đã xóa ảnh đại diện."),
            );
        } catch {
            setPrivacyMessage(
                copy(
                    "Could not remove profile photo. Refresh your profile and retry.",
                    "Không thể xóa ảnh. Hãy tải lại hồ sơ và thử lại.",
                ),
            );
        } finally {
            setPrivacyBusy(false);
        }
    }

    const name =
        profile?.fullName?.trim() ||
        session?.displayName?.trim() ||
        copy("Not provided", "Chưa cung cấp");

    const email =
        session?.account.email?.trim() || copy("Not provided", "Chưa cung cấp");

    const phone =
        profile?.phoneNumber?.trim() ||
        session?.account.phoneNumber?.trim() ||
        copy("Not provided", "Chưa cung cấp");

    const address =
        profile?.address?.trim() || copy("Not provided", "Chưa cung cấp");

    const dob = profile?.dateOfBirth
        ? new Date(profile.dateOfBirth).toLocaleDateString("vi-VN", {
              day: "2-digit",
              month: "2-digit",
              year: "numeric",
          })
        : copy("Not provided", "Chưa cung cấp");

    const initial =
        !name || name === copy("Not provided", "Chưa cung cấp") ? (
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
                <div
                    className="customer-profile-avatar"
                    aria-hidden="true"
                    style={
                        avatarObjectUrl
                            ? {
                                  backgroundImage: `url(${avatarObjectUrl})`,
                                  backgroundSize: "cover",
                                  backgroundPosition: "center",
                              }
                            : undefined
                    }
                >
                    {avatarObjectUrl ? null : initial}
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

            {profile && (
                <div className="customer-account-card" style={{ padding: 20 }}>
                    <h2>{copy("Privacy controls", "Quyền riêng tư")}</h2>
                    {profile.avatarUrl?.startsWith(
                        "/api/identity/accounts/me/media/",
                    ) && (
                        <button
                            type="button"
                            disabled={privacyBusy}
                            onClick={() => void removeAvatar()}
                        >
                            {copy("Remove profile photo", "Xóa ảnh đại diện")}
                        </button>
                    )}
                    {(profile.citizenIdNumber ||
                        profile.driverLicenseNumber ||
                        profile.identityFrontMediaId ||
                        profile.identityBackMediaId) && (
                        <button
                            type="button"
                            disabled={privacyBusy}
                            onClick={() => void removeIdentity()}
                            style={{ marginLeft: 16 }}
                        >
                            {copy(
                                "Delete identity details and documents",
                                "Xóa thông tin và giấy tờ định danh",
                            )}
                        </button>
                    )}
                    {privacyMessage && <p role="status">{privacyMessage}</p>}
                </div>
            )}

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
                    {loading ? (
                        <div className="flex items-center justify-center p-8 text-[#8790a3] gap-2">
                            <Loader2
                                size={20}
                                className="animate-spin text-[#6246ea]"
                            />
                            <span>
                                {copy(
                                    "Loading profile details...",
                                    "Đang tải chi tiết hồ sơ...",
                                )}
                            </span>
                        </div>
                    ) : (
                        <div className="customer-profile-fields">
                            <ProfileField
                                icon={UserRound}
                                label={copy("Full name", "Họ và tên")}
                                value={name}
                            />
                            <ProfileField
                                icon={Mail}
                                label="Email"
                                value={email}
                            />
                            <ProfileField
                                icon={Phone}
                                label={copy("Phone number", "Số điện thoại")}
                                value={phone}
                            />
                            <ProfileField
                                icon={CalendarDays}
                                label={copy("Date of birth", "Ngày sinh")}
                                value={dob}
                            />
                            <ProfileField
                                icon={MapPin}
                                label={copy("Address", "Địa chỉ")}
                                value={address}
                            />
                        </div>
                    )}
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
                                "Your profile is linked directly to your ParkWise Identity. Updating your profile ensures accurate booking and parking session records.",
                                "Hồ sơ của bạn được liên kết trực tiếp với Định danh ParkWise. Cập nhật hồ sơ giúp thông tin đặt chỗ và phiên đỗ luôn chính xác.",
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
