"use client";

import { useEffect, useState } from "react";

let mswStarted = false;

export function MSWProvider({ children }: { children: React.ReactNode }) {
    const [mswReady, setMswReady] = useState(
        () => process.env.NEXT_PUBLIC_USE_MOCKS !== "true",
    );

    useEffect(() => {
        if (process.env.NEXT_PUBLIC_USE_MOCKS === "true") {
            const init = async () => {
                if (mswStarted) {
                    setMswReady(true);
                    return;
                }
                mswStarted = true;

                try {
                    const { worker } = await import("../../mocks/browser");
                    await worker.start({
                        onUnhandledRequest: "bypass",
                    });
                } catch {
                    // Ignore if it's already running
                }
                setMswReady(true);
            };

            init();
        }
    }, []);

    if (!mswReady) return null;

    return <>{children}</>;
}
