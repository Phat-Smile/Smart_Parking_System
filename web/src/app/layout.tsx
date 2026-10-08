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

const initializeTheme = `
    let theme = "dark";
    try {
        if (localStorage.getItem("smart-parking-theme") === "light") theme = "light";
    } catch {}
    document.documentElement.classList.toggle("dark", theme === "dark");
    document.documentElement.dataset.theme = theme;
`;

export const metadata: Metadata = {
    title: "ParkWise - Find parking before you arrive",
    description: "ParkWise web application",
};

export default function RootLayout({
    children,
}: Readonly<{ children: React.ReactNode }>) {
    return (
        <html
            lang="en"
            data-scroll-behavior="smooth"
            className={`${plusJakartaSans.variable} ${spaceGrotesk.variable} ${jetbrainsMono.variable}`}
            suppressHydrationWarning
        >
            <head>
                <script dangerouslySetInnerHTML={{ __html: initializeTheme }} />
            </head>
            <body>
                <Providers>{children}</Providers>
            </body>
        </html>
    );
}
