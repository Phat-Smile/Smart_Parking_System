import type { Metadata } from "next";
import { Plus_Jakarta_Sans, JetBrains_Mono } from "next/font/google";
import { Providers } from "./providers";
import "./globals.css";

const plusJakartaSans = Plus_Jakarta_Sans({
    subsets: ["latin", "vietnamese"],
    variable: "--font-sans",
    display: "swap",
});

const jetbrainsMono = JetBrains_Mono({
    subsets: ["latin", "vietnamese"],
    variable: "--font-mono",
    display: "swap",
});

export const metadata: Metadata = {
    title: "ParkWise - Tìm bãi đỗ xe trước khi khởi hành",
    description: "Hệ thống quản lý và đặt chỗ bãi đỗ xe thông minh ParkWise",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default function RootLayout({
    children,
}: Readonly<{ children: React.ReactNode }>) {
    return (
        <html
            lang="vi"
            data-theme="dark"
            data-scroll-behavior="smooth"
            className={`dark ${plusJakartaSans.variable} ${jetbrainsMono.variable}`}
            suppressHydrationWarning
        >
            <body>
                <Providers>{children}</Providers>
            </body>
        </html>
    );
}
