"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import { LanguageProvider } from "@/shared/i18n/LanguageContext";
import { ToastNotificationHub } from "@/shared/ui/ToastNotificationHub";
// Suppress React 19 false-positive warning regarding script tags in Next.js Turbopack dev mode
if (typeof window !== "undefined") {
    const originalConsoleError = console.error;
    console.error = (...args: unknown[]) => {
        if (
            typeof args[0] === "string" &&
            args[0].includes("Encountered a script tag")
        ) {
            return;
        }
        originalConsoleError.apply(console, args);
    };
}

export function Providers({
    children,
}: Readonly<{ children: React.ReactNode }>) {
    const [queryClient] = useState(
        () =>
            new QueryClient({
                defaultOptions: {
                    queries: { retry: 1, refetchOnWindowFocus: false },
                },
            }),
    );

    return (
        <QueryClientProvider client={queryClient}>
            <LanguageProvider>
                {children}
                <ToastNotificationHub />
            </LanguageProvider>
        </QueryClientProvider>
    );
}
