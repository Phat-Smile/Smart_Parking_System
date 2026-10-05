"use client";

import React, {
    createContext,
    useContext,
    useEffect,
    useState,
    useSyncExternalStore,
} from "react";

type Theme = "dark" | "light";

interface ThemeContextType {
    theme: Theme;
    isDark: boolean;
    setTheme: (theme: Theme) => void;
    toggleTheme: () => void;
}

const ThemeContext = createContext<ThemeContextType | undefined>(undefined);

const THEME_STORAGE_KEY = "smart-parking-theme";

function getStoredTheme(): Theme {
    if (typeof window === "undefined") {
        return "dark";
    }
    try {
        const stored = localStorage.getItem(THEME_STORAGE_KEY);
        if (stored === "light" || stored === "dark") {
            return stored;
        }
    } catch {
        // Ignore localStorage error in restricted environments
    }
    return "dark";
}

function applyThemeClass(newTheme: Theme) {
    const root = document.documentElement;
    if (newTheme === "dark") {
        root.classList.add("dark");
        root.setAttribute("data-theme", "dark");
    } else {
        root.classList.remove("dark");
        root.setAttribute("data-theme", "light");
    }
}

const emptySubscribe = () => () => {};

export function ThemeProvider({
    children,
    defaultTheme = "dark",
}: {
    children: React.ReactNode;
    defaultTheme?: Theme;
}) {
    const isClient = useSyncExternalStore(
        emptySubscribe,
        () => true,
        () => false,
    );

    const [theme, setThemeState] = useState<Theme>(getStoredTheme);

    useEffect(() => {
        applyThemeClass(theme);
    }, [theme]);

    const setTheme = (newTheme: Theme) => {
        setThemeState(newTheme);
        try {
            localStorage.setItem(THEME_STORAGE_KEY, newTheme);
        } catch {
            // Ignore
        }
        applyThemeClass(newTheme);
    };

    const toggleTheme = () => {
        const next = theme === "dark" ? "light" : "dark";
        setTheme(next);
    };

    const currentTheme = isClient ? theme : defaultTheme;
    const isDark = currentTheme === "dark";

    return (
        <ThemeContext.Provider
            value={{
                theme: currentTheme,
                isDark,
                setTheme,
                toggleTheme,
            }}
        >
            {children}
        </ThemeContext.Provider>
    );
}

export function useTheme(): ThemeContextType {
    const context = useContext(ThemeContext);
    if (!context) {
        throw new Error("useTheme must be used within a ThemeProvider");
    }
    return context;
}
