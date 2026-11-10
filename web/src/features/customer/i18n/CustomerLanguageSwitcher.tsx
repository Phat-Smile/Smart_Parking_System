"use client";

import { useEffect, useRef, useState } from "react";
import { Check } from "lucide-react";
import { setCustomerLanguage, useCustomerLanguage } from "./customerLanguage";

export function CustomerLanguageSwitcher({ mobile = false }: { mobile?: boolean }) {
    const { language } = useCustomerLanguage();
    const [open, setOpen] = useState(false);
    const rootRef = useRef<HTMLDivElement>(null);
    const triggerRef = useRef<HTMLButtonElement>(null);

    useEffect(() => {
        if (!open) return;
        function handlePointerDown(event: PointerEvent) {
            if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
        }
        function handleKeyDown(event: KeyboardEvent) {
            if (event.key === "Escape") {
                setOpen(false);
                triggerRef.current?.focus();
            }
        }
        document.addEventListener("pointerdown", handlePointerDown);
        document.addEventListener("keydown", handleKeyDown);
        return () => {
            document.removeEventListener("pointerdown", handlePointerDown);
            document.removeEventListener("keydown", handleKeyDown);
        };
    }, [open]);

    function choose(language: "en" | "vi") {
        setCustomerLanguage(language);
        setOpen(false);
        triggerRef.current?.focus();
    }

    return (
        <div
            className={`customer-language-switcher${mobile ? " is-mobile" : ""}${open ? " is-open" : ""}`}
            ref={rootRef}
        >
            <button
                ref={triggerRef}
                type="button"
                className="customer-language-trigger"
                aria-label={language === "en" ? "Language: English" : "Ngôn ngữ: Tiếng Việt"}
                aria-haspopup="listbox"
                aria-expanded={open}
                onClick={() => setOpen((current) => !current)}
            >
                <svg
                    xmlns="http://www.w3.org/2000/svg"
                    viewBox="0 0 512 512"
                    width="18"
                    height="18"
                    aria-hidden="true"
                    focusable="false"
                >
                    <g
                        fill="none"
                        stroke="rgb(116, 192, 252)"
                        strokeWidth="32"
                    >
                        <circle cx="256" cy="256" r="222" />
                        <ellipse cx="256" cy="256" rx="102" ry="222" />
                        <path d="M45 174c122 43 300 43 422 0M34 256h444M45 338c122-43 300-43 422 0" />
                    </g>
                </svg>
                <span aria-hidden="true">{language === "en" ? "EN" : "VI"}</span>
            </button>
            {open && (
                <div className="customer-language-options" role="listbox" aria-label="Language / Ngôn ngữ">
                    <button
                        type="button"
                        role="option"
                        aria-selected={language === "en"}
                        lang="en"
                        onClick={() => choose("en")}
                    >
                        <span>English</span>
                        {language === "en" && <Check size={16} aria-hidden="true" />}
                    </button>
                    <button
                        type="button"
                        role="option"
                        aria-selected={language === "vi"}
                        lang="vi"
                        onClick={() => choose("vi")}
                    >
                        <span>Tiếng Việt</span>
                        {language === "vi" && <Check size={16} aria-hidden="true" />}
                    </button>
                </div>
            )}
        </div>
    );
}
