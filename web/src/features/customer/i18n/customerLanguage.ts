"use client";

import { useCallback, useSyncExternalStore } from "react";

export type CustomerLanguage = "en" | "vi";

const STORAGE_KEY = "parkwise_customer_language";
const CHANGE_EVENT = "parkwise-customer-language-change";
let fallbackLanguage: CustomerLanguage = "en";

function getSnapshot(): CustomerLanguage {
    if (typeof window === "undefined") return "en";
    try {
        const saved = window.localStorage.getItem(STORAGE_KEY);
        return saved === "vi" || saved === "en" ? saved : fallbackLanguage;
    } catch {
        return fallbackLanguage;
    }
}

function subscribe(onChange: () => void) {
    window.addEventListener(CHANGE_EVENT, onChange);
    window.addEventListener("storage", onChange);
    return () => {
        window.removeEventListener(CHANGE_EVENT, onChange);
        window.removeEventListener("storage", onChange);
    };
}

export function setCustomerLanguage(language: CustomerLanguage) {
    fallbackLanguage = language;
    try {
        window.localStorage.setItem(STORAGE_KEY, language);
    } catch {
        // The current tab still updates when storage is unavailable.
    }
    window.dispatchEvent(new Event(CHANGE_EVENT));
}

export function useCustomerLanguage() {
    const language = useSyncExternalStore<CustomerLanguage>(
        subscribe,
        getSnapshot,
        () => "en",
    );
    const copy = useCallback(
        (english: string, vietnamese: string) =>
            language === "vi" ? vietnamese : english,
        [language],
    );
    return {
        language,
        isVietnamese: language === "vi",
        copy,
    };
}
