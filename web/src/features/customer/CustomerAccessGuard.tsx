"use client";

import { useEffect, useSyncExternalStore, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { CUSTOMER_UI_PREVIEW } from "./customerUiPreview";

export function CustomerAccessGuard({ children }: { children: ReactNode }) {
    const router = useRouter();
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );
    const ready = useSyncExternalStore(
        () => () => {},
        () => true,
        () => false,
    );

    useEffect(() => {
        if (CUSTOMER_UI_PREVIEW || !ready) return;
        if (!session) {
            router.replace("/login");
        } else if (session.account.role !== "Customer") {
            router.replace("/403");
        }
    }, [ready, router, session]);

    if (
        !CUSTOMER_UI_PREVIEW &&
        (!ready || session?.account.role !== "Customer")
    ) {
        return <main aria-live="polite">Checking customer access…</main>;
    }

    return <>{children}</>;
}
