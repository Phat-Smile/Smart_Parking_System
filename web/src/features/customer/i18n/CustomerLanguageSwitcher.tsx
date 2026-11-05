"use client";

import { setCustomerLanguage, useCustomerLanguage } from "./customerLanguage";

export function CustomerLanguageSwitcher({ mobile = false }: { mobile?: boolean }) {
    const { language } = useCustomerLanguage();

    return (
        <div className={`customer-language-switcher${mobile ? " is-mobile" : ""}`} role="group" aria-label="Language / Ngôn ngữ">
            <button type="button" lang="en" aria-label="English" aria-pressed={language === "en"} onClick={() => setCustomerLanguage("en")}>EN</button>
            <button type="button" lang="vi" aria-label="Tiếng Việt" aria-pressed={language === "vi"} onClick={() => setCustomerLanguage("vi")}>VI</button>
        </div>
    );
}
