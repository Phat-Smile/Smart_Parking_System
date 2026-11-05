"use client";

import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { useCustomerLanguage } from "../i18n/customerLanguage";

export function WalletPageIntro() {
    const { copy } = useCustomerLanguage();
    return (
        <div className="wallet-page-intro">
            <div>
                <span className="wallet-eyebrow">{copy("YOUR ACCOUNT", "TÀI KHOẢN CỦA BẠN")}</span>
                <h1>{copy("My Wallet", "Ví của tôi")}</h1>
                <p>{copy("Manage your balance and parking payments in one place.", "Quản lý số dư và thanh toán đỗ xe tại một nơi.")}</p>
            </div>
            <span className="wallet-page-badge">ParkWise Wallet</span>
        </div>
    );
}

export function VnpayReturnIntro() {
    const { copy } = useCustomerLanguage();
    return (
        <>
            <div className="wallet-return-topline">
                <Link href="/customer/wallet" className="wallet-return-back">
                    <ArrowLeft size={18} aria-hidden="true" />
                    {copy("My Wallet", "Ví của tôi")}
                </Link>
                <span className="wallet-return-kicker">{copy("VNPAY PAYMENT", "THANH TOÁN QUA VNPAY")}</span>
            </div>
            <div className="wallet-return-intro">
                <h1>{copy("Top-up result", "Kết quả nạp tiền")}</h1>
                <p>{copy("Check your payment and ParkWise wallet balance.", "Kiểm tra giao dịch và số dư Ví ParkWise của bạn.")}</p>
            </div>
        </>
    );
}
