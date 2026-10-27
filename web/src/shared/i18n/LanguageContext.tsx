"use client";

import React, {
    createContext,
    useCallback,
    useContext,
    useEffect,
    useMemo,
    useState,
    useSyncExternalStore,
} from "react";
import {
    translations,
    type SupportedLanguage,
    type TranslationDictionary,
} from "./translations";

interface LanguageContextValue {
    language: SupportedLanguage;
    setLanguage: (lang: SupportedLanguage) => void;
    toggleLanguage: () => void;
    t: (key: keyof TranslationDictionary, fallback?: string) => string;
}

const STORAGE_KEY = "smartpark_language";

const LanguageContext = createContext<LanguageContextValue | null>(null);

function getInitialLanguage(): SupportedLanguage {
    if (typeof window === "undefined") return "vi";
    try {
        const saved = localStorage.getItem(STORAGE_KEY) as SupportedLanguage | null;
        if (saved === "vi" || saved === "en") return saved;
    } catch {
        // Ignore
    }
    return "vi";
}

const emptySubscribe = () => () => {};

export function LanguageProvider({ children }: { children: React.ReactNode }) {
    const isClient = useSyncExternalStore(
        emptySubscribe,
        () => true,
        () => false,
    );

    const [languageState, setLanguageState] = useState<SupportedLanguage>(getInitialLanguage);

    // During SSR and initial hydration, language is always "vi" to match server render.
    // As soon as hydration completes, isClient becomes true and the stored preference takes effect.
    const language: SupportedLanguage = isClient ? languageState : "vi";

    // Sync document element lang and listen for cross-tab storage changes
    useEffect(() => {
        document.documentElement.lang = language;

        const handleStorageChange = (e: StorageEvent) => {
            if (e.key === STORAGE_KEY && (e.newValue === "vi" || e.newValue === "en")) {
                setLanguageState(e.newValue as SupportedLanguage);
                document.documentElement.lang = e.newValue;
            }
        };
        window.addEventListener("storage", handleStorageChange);
        return () => window.removeEventListener("storage", handleStorageChange);
    }, [language]);

    const setLanguage = useCallback((lang: SupportedLanguage) => {
        setLanguageState(lang);
        try {
            localStorage.setItem(STORAGE_KEY, lang);
            document.documentElement.lang = lang;
        } catch {
            // Ignore
        }
    }, []);

    const toggleLanguage = useCallback(() => {
        setLanguage(language === "vi" ? "en" : "vi");
    }, [language, setLanguage]);

    const t = useCallback(
        (key: keyof TranslationDictionary, fallback?: string): string => {
            const dict = translations[language] || translations.vi;
            return dict[key] ?? fallback ?? key;
        },
        [language],
    );

    const value = useMemo(
        () => ({
            language,
            setLanguage,
            toggleLanguage,
            t,
        }),
        [language, setLanguage, toggleLanguage, t],
    );

    return (
        <LanguageContext.Provider value={value}>
            {children}
        </LanguageContext.Provider>
    );
}

export function useLanguage() {
    const context = useContext(LanguageContext);
    if (!context) {
        // Fallback if rendered outside provider
        return {
            language: "vi" as SupportedLanguage,
            setLanguage: () => {},
            toggleLanguage: () => {},
            t: (key: keyof TranslationDictionary, fallback?: string) =>
                translations.vi[key] ?? fallback ?? key,
        };
    }
    return context;
}

/**
 * Nút chuyển đổi ngôn ngữ ở góc trái (Language Switcher Button)
 * Hỗ trợ chế độ inline (cho sidebar) hoặc floating (góc dưới bên trái màn hình)
 */
export function LanguageSwitcher({
    variant = "sidebar",
    className = "",
}: {
    variant?: "sidebar" | "floating" | "compact";
    className?: string;
}) {
    const { language, setLanguage } = useLanguage();

    if (variant === "compact") {
        return (
            <div
                className={`language-switcher compact ${className}`}
                style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "4px",
                    background: "rgba(255, 255, 255, 0.08)",
                    padding: "3px 6px",
                    borderRadius: "6px",
                    border: "1px solid rgba(255, 255, 255, 0.12)",
                }}
            >
                <button
                    type="button"
                    onClick={() => setLanguage("vi")}
                    style={{
                        background: language === "vi" ? "var(--admin-lime, #a3e635)" : "transparent",
                        color: language === "vi" ? "#000" : "inherit",
                        fontWeight: language === "vi" ? 700 : 500,
                        border: "none",
                        borderRadius: "4px",
                        padding: "2px 6px",
                        fontSize: "11px",
                        cursor: "pointer",
                        transition: "all 0.15s ease",
                    }}
                    title="Tiếng Việt"
                >
                    🇻🇳 VI
                </button>
                <span style={{ opacity: 0.3, fontSize: "11px" }}>|</span>
                <button
                    type="button"
                    onClick={() => setLanguage("en")}
                    style={{
                        background: language === "en" ? "var(--admin-lime, #a3e635)" : "transparent",
                        color: language === "en" ? "#000" : "inherit",
                        fontWeight: language === "en" ? 700 : 500,
                        border: "none",
                        borderRadius: "4px",
                        padding: "2px 6px",
                        fontSize: "11px",
                        cursor: "pointer",
                        transition: "all 0.15s ease",
                    }}
                    title="English"
                >
                    🇬🇧 EN
                </button>
            </div>
        );
    }

    if (variant === "floating") {
        return (
            <aside
                aria-label="Language selection"
                className={`language-switcher-floating ${className}`}
                style={{
                    position: "fixed",
                    bottom: "20px",
                    left: "20px",
                    zIndex: 9999,
                    display: "flex",
                    alignItems: "center",
                    gap: "6px",
                    padding: "6px 10px",
                    borderRadius: "24px",
                    background: "rgba(18, 22, 28, 0.92)",
                    backdropFilter: "blur(12px)",
                    border: "1px solid rgba(255, 255, 255, 0.18)",
                    boxShadow: "0 8px 24px rgba(0, 0, 0, 0.4)",
                }}
            >
                <span style={{ fontSize: "14px" }}>🌐</span>
                <button
                    type="button"
                    onClick={() => setLanguage("vi")}
                    style={{
                        background: language === "vi" ? "var(--admin-lime, #a3e635)" : "transparent",
                        color: language === "vi" ? "#090d16" : "#cbd5e1",
                        fontWeight: language === "vi" ? 700 : 500,
                        border: "none",
                        borderRadius: "16px",
                        padding: "4px 8px",
                        fontSize: "12px",
                        cursor: "pointer",
                    }}
                >
                    🇻🇳 Tiếng Việt
                </button>
                <button
                    type="button"
                    onClick={() => setLanguage("en")}
                    style={{
                        background: language === "en" ? "var(--admin-lime, #a3e635)" : "transparent",
                        color: language === "en" ? "#090d16" : "#cbd5e1",
                        fontWeight: language === "en" ? 700 : 500,
                        border: "none",
                        borderRadius: "16px",
                        padding: "4px 8px",
                        fontSize: "12px",
                        cursor: "pointer",
                    }}
                >
                    🇬🇧 English
                </button>
            </aside>
        );
    }

    // Default sidebar variant
    return (
        <div
            className={`language-switcher-sidebar ${className}`}
            style={{
                display: "flex",
                flexDirection: "column",
                gap: "6px",
                padding: "8px 10px",
                background: "rgba(255, 255, 255, 0.03)",
                border: "1px solid rgba(255, 255, 255, 0.08)",
                borderRadius: "8px",
                fontSize: "12px",
            }}
        >
            <div
                style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    color: "var(--staff-text-muted, #94a3b8)",
                    fontSize: "11px",
                    fontWeight: 600,
                    textTransform: "uppercase",
                    letterSpacing: "0.5px",
                }}
            >
                <span>🌐 {language === "vi" ? "Ngôn ngữ" : "Language"}</span>
                <span style={{ opacity: 0.7 }}>{language.toUpperCase()}</span>
            </div>

            <div
                style={{
                    display: "grid",
                    gridTemplateColumns: "1fr 1fr",
                    gap: "4px",
                    background: "rgba(0, 0, 0, 0.25)",
                    padding: "2px",
                    borderRadius: "6px",
                }}
            >
                <button
                    type="button"
                    onClick={() => setLanguage("vi")}
                    style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        gap: "4px",
                        padding: "5px 8px",
                        borderRadius: "4px",
                        border: "none",
                        fontSize: "11.5px",
                        fontWeight: language === "vi" ? 700 : 500,
                        background: language === "vi" ? "var(--admin-lime, #a3e635)" : "transparent",
                        color: language === "vi" ? "#090d16" : "rgba(255, 255, 255, 0.75)",
                        cursor: "pointer",
                        transition: "all 0.15s ease",
                    }}
                >
                    <span>🇻🇳</span>
                    <span>Tiếng Việt</span>
                </button>
                <button
                    type="button"
                    onClick={() => setLanguage("en")}
                    style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        gap: "4px",
                        padding: "5px 8px",
                        borderRadius: "4px",
                        border: "none",
                        fontSize: "11.5px",
                        fontWeight: language === "en" ? 700 : 500,
                        background: language === "en" ? "var(--admin-lime, #a3e635)" : "transparent",
                        color: language === "en" ? "#090d16" : "rgba(255, 255, 255, 0.75)",
                        cursor: "pointer",
                        transition: "all 0.15s ease",
                    }}
                >
                    <span>🇬🇧</span>
                    <span>English</span>
                </button>
            </div>
        </div>
    );
}
