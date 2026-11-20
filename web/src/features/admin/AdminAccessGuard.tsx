"use client";

import { useEffect, useSyncExternalStore, type ReactNode } from "react";
import { usePathname, useRouter } from "next/navigation";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";

export function AdminAccessGuard({ children }: { children: ReactNode }) {
    const router = useRouter();
    const pathname = usePathname();

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

    const isSystemAdmin = session?.account?.role === "SystemAdmin";

    useEffect(() => {
        if (!ready) return;

        if (!session) {
            const returnUrl = pathname
                ? `?returnUrl=${encodeURIComponent(pathname)}`
                : "";
            router.replace(`/login${returnUrl}`);
            return;
        }

        if (!isSystemAdmin) {
            router.replace("/403");
        }
    }, [ready, router, session, isSystemAdmin, pathname]);

    if (!ready || !session || !isSystemAdmin) {
        return (
            <div
                className="admin-guard-loading"
                role="status"
                aria-live="polite"
                style={{
                    minHeight: "100vh",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    backgroundColor: "#0b0f17",
                    color: "#9ca3af",
                    fontSize: "0.875rem",
                    fontFamily: "inherit",
                }}
            >
                <div
                    style={{
                        display: "flex",
                        alignItems: "center",
                        gap: "10px",
                    }}
                >
                    <div
                        style={{
                            width: 20,
                            height: 20,
                            border: "2px solid #374151",
                            borderTopColor: "#bbf445",
                            borderRadius: "50%",
                            animation: "admin-guard-spin 0.8s linear infinite",
                        }}
                    />
                    <span>
                        {!session
                            ? "Đang chuyển hướng đến trang đăng nhập..."
                            : "Kiểm tra quyền truy cập Quản trị viên..."}
                    </span>
                </div>
                <style>{`
                    @keyframes admin-guard-spin {
                        to { transform: rotate(360deg); }
                    }
                `}</style>
            </div>
        );
    }

    return <>{children}</>;
}
