"use client";

import { useEffect, useSyncExternalStore } from "react";
import { useRouter } from "next/navigation";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { CustomerWallet } from "./CustomerWallet";
import { VnpayReturnStatus } from "./VnpayReturnStatus";

export function CustomerWalletScreen() {
    return (
        <CustomerWallet
            onRedirect={(url) => window.location.assign(url)}
            newKey={() => crypto.randomUUID()}
        />
    );
}

/**
 * VNPay returns with a full page load, and the sign-in lives in memory only, so the customer may have to
 * sign in again; the login page then brings them back to this transaction.
 */
export function VnpayReturnScreen({ topupId }: { topupId: string | null }) {
    const router = useRouter();
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );

    useEffect(() => {
        if (session) return;
        const back = `${window.location.pathname}${window.location.search}`;
        router.replace(`/login?redirect=${encodeURIComponent(back)}`);
    }, [router, session]);

    if (!session) return <p>Đang chuyển đến trang đăng nhập...</p>;
    return <VnpayReturnStatus topupId={topupId} />;
}
