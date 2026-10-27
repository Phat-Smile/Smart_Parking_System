import type { Metadata } from "next";
import {
    Plus_Jakarta_Sans,
    Space_Grotesk,
    JetBrains_Mono,
} from "next/font/google";
import { Providers } from "./providers";
import "./globals.css";

const plusJakartaSans = Plus_Jakarta_Sans({
    subsets: ["latin"],
    variable: "--font-sans",
    display: "swap",
});

const spaceGrotesk = Space_Grotesk({
    subsets: ["latin"],
    variable: "--font-heading",
    display: "swap",
});

const jetbrainsMono = JetBrains_Mono({
    subsets: ["latin"],
    variable: "--font-mono",
    display: "swap",
});

export const metadata: Metadata = {
    title: "ParkWise - Find parking before you arrive",
    description: "ParkWise web application",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default function RootLayout({
    children,
}: Readonly<{ children: React.ReactNode }>) {
    return (
        <html
            lang="en"
            data-theme="dark"
            data-scroll-behavior="smooth"
            className={`dark ${plusJakartaSans.variable} ${spaceGrotesk.variable} ${jetbrainsMono.variable}`}
            suppressHydrationWarning
        >
            <body>
                <Providers>{children}</Providers>
            </body>
        </html>
    );
}
