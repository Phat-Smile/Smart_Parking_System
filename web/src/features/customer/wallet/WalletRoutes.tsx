"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import { useRouter } from "next/navigation";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { CustomerWallet } from "./CustomerWallet";
import { VnpayReturnStatus } from "./VnpayReturnStatus";
import { VnpayReturnIntro } from "./WalletPageCopy";
import { CustomerParkingFeeReturn } from "../CustomerParkingFeeReturn";

export function CustomerWalletScreen() {
    const router = useRouter();
    return (
        <CustomerWallet
            onRedirect={(url) => window.location.assign(url)}
            onOpenStatus={(topupId) =>
                router.push(
                    `/payment/vnpay-return?vnp_TxnRef=${encodeURIComponent(topupId)}`,
                )
            }
            newKey={() => crypto.randomUUID()}
        />
    );
}

/**
 * VNPay returns with a full page load. Let browser storage hydrate before deciding
 * whether the customer needs to sign in again.
 */
export function VnpayReturnScreen({ topupId }: { topupId: string | null }) {
    const router = useRouter();
    const [hydrated, setHydrated] = useState(false);
    const [parkingFeeReturn, setParkingFeeReturn] = useState<{
        sessionId: string;
        transactionId: string;
    } | null>(null);
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );

    useEffect(() => {
        const timer = window.setTimeout(() => {
            const saved = topupId
                ? window.sessionStorage.getItem(
                      `parkwise:parking-fee-return:${topupId}`,
                  )
                : null;
            if (saved) {
                try {
                    const parsed = JSON.parse(saved) as {
                        sessionId?: string;
                        transactionId?: string;
                    };
                    if (parsed.sessionId && parsed.transactionId)
                        setParkingFeeReturn({
                            sessionId: parsed.sessionId,
                            transactionId: parsed.transactionId,
                        });
                } catch {
                    /* Ignore invalid local mapping. */
                }
            }
            setHydrated(true);
        }, 0);
        return () => window.clearTimeout(timer);
    }, [topupId]);

    useEffect(() => {
        if (!hydrated || session) return;
        const back = `${window.location.pathname}${window.location.search}`;
        router.replace(`/login?redirect=${encodeURIComponent(back)}`);
    }, [hydrated, router, session]);

    if (!session) return <p>Redirecting to login...</p>;
    if (!hydrated) return <p>Checking payment...</p>;
    if (parkingFeeReturn)
        return (
            <CustomerParkingFeeReturn
                sessionId={parkingFeeReturn.sessionId}
                transactionId={parkingFeeReturn.transactionId}
            />
        );
    return (
        <>
            <VnpayReturnIntro />
            <VnpayReturnStatus topupId={topupId} />
        </>
    );
}
