"use client";

import { useState, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import {
    CarFront,
    ChevronRight,
    FileText,
    LogOut,
    Wallet,
    Bell,
    Languages,
} from "lucide-react";
import { logoutParkWiseAccount } from "@/features/auth/logoutParkWiseAccount";
import { useCustomerLanguage } from "../i18n/customerLanguage";
import { CustomerLanguageSwitcher } from "../i18n/CustomerLanguageSwitcher";
import { ParkWiseIconTile } from "@/features/parkwise/ParkWiseIconTile";

export function CustomerMobileAccount({
    name,
    phone,
    email,
    avatar,
    initial,
    loading,
    privacyControls,
}: {
    name: string;
    phone: string;
    email: string;
    avatar: string | null;
    initial: ReactNode;
    loading: boolean;
    privacyControls: ReactNode;
}) {
    const { copy } = useCustomerLanguage();
    const router = useRouter();
    const [signingOut, setSigningOut] = useState(false);
    async function signOut() {
        setSigningOut(true);
        try {
            await logoutParkWiseAccount();
        } catch {
            /* The existing logout helper clears the local session even if the server is unavailable. */
        } finally {
            router.replace("/login");
        }
    }
    return (
        <div className="pw-app-account">
            <Link
                href="/customer/complete-profile"
                className="pw-app-account-identity"
            >
                <span
                    className="pw-app-avatar parkwise-tone"
                    data-tone="account"
                    style={
                        avatar
                            ? { backgroundImage: `url(${avatar})` }
                            : undefined
                    }
                >
                    {avatar ? null : initial}
                </span>
                <div>
                    <h2>{loading ? copy("Loading…", "Đang tải…") : name}</h2>
                    <p>{phone}</p>
                </div>
                <ChevronRight size={22} aria-hidden="true" />
            </Link>
            <Link
                href="/customer/complete-profile"
                className="pw-app-profile-banner"
            >
                <ParkWiseIconTile tone="account">
                    <FileText size={30} aria-hidden="true" />
                </ParkWiseIconTile>
                <div>
                    <h2>
                        {copy(
                            "Your profile, up to date",
                            "Hồ sơ luôn sẵn sàng",
                        )}
                    </h2>
                    <p>
                        {copy(
                            "Review your details and saved documents.",
                            "Xem và cập nhật thông tin, giấy tờ đã lưu.",
                        )}
                    </p>
                </div>
                <ChevronRight size={19} aria-hidden="true" />
            </Link>
            <div className="pw-app-account-tiles">
                <Link
                    href="/customer/wallet"
                    className="parkwise-tone"
                    data-tone="wallet"
                >
                    <Wallet size={30} aria-hidden="true" />
                    <h2>{copy("My wallet", "Ví của tôi")}</h2>
                    <span>
                        {copy("Balance & transactions", "Số dư & giao dịch")}
                    </span>
                </Link>
                <Link
                    href="/customer/vehicles"
                    className="parkwise-tone"
                    data-tone="vehicles"
                >
                    <CarFront size={30} aria-hidden="true" />
                    <h2>{copy("My vehicles", "Xe của tôi")}</h2>
                    <span>
                        {copy("Manage your vehicles", "Quản lý phương tiện")}
                    </span>
                </Link>
            </div>
            <section className="pw-app-section">
                <h2 className="pw-app-section-title">
                    {copy("Settings & utilities", "Cài đặt & tiện ích")}
                </h2>
                <nav
                    className="pw-app-account-links"
                    aria-label={copy("Account settings", "Cài đặt tài khoản")}
                >
                    <Link href="/customer/notifications">
                        <Bell size={23} aria-hidden="true" />
                        <span>{copy("Notifications", "Thông báo")}</span>
                        <ChevronRight size={18} aria-hidden="true" />
                    </Link>
                    <div className="pw-app-language-setting">
                        <Languages size={23} aria-hidden="true" />
                        <span>{copy("Language", "Ngôn ngữ")}</span>
                        <CustomerLanguageSwitcher />
                    </div>
                </nav>
            </section>
            {privacyControls}
            <div className="pw-app-account-footer">
                <p>{email}</p>
                <button
                    type="button"
                    onClick={() => void signOut()}
                    disabled={signingOut}
                >
                    <LogOut size={18} aria-hidden="true" />
                    {signingOut
                        ? copy("Signing out…", "Đang đăng xuất…")
                        : copy("Sign out", "Đăng xuất")}
                </button>
                <div>
                    <Link href="/privacy-policy">
                        {copy("Privacy", "Bảo mật")}
                    </Link>
                    <Link href="/terms-and-conditions">
                        {copy("Terms", "Điều khoản")}
                    </Link>
                </div>
            </div>
        </div>
    );
}
