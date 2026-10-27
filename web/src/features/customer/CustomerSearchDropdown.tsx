"use client";

import { useEffect, useId, useRef, useState, type KeyboardEvent } from "react";
import { CustomerIcon, type CustomerIconName } from "./CustomerShell";

export type SearchDropdownOption = {
    value: string;
    label: string;
    icon?: CustomerIconName;
};

export function CustomerSearchDropdown({
    label,
    value,
    options,
    onChange,
    variant = "filter",
    align = "left",
}: {
    label: string;
    value: string;
    options: SearchDropdownOption[];
    onChange: (value: string) => void;
    variant?: "filter" | "field";
    align?: "left" | "right";
}) {
    const [open, setOpen] = useState(false);
    const rootRef = useRef<HTMLDivElement>(null);
    const triggerRef = useRef<HTMLButtonElement>(null);
    const optionRefs = useRef<Array<HTMLButtonElement | null>>([]);
    const menuId = useId();
    const selected = options.find((option) => option.value === value);
    const isActive = value !== options[0]?.value;

    useEffect(() => {
        function handleOutsideClick(event: PointerEvent) {
            if (!rootRef.current?.contains(event.target as Node))
                setOpen(false);
        }
        document.addEventListener("pointerdown", handleOutsideClick);
        return () =>
            document.removeEventListener("pointerdown", handleOutsideClick);
    }, []);

    function focusOption(index: number) {
        requestAnimationFrame(() => optionRefs.current[index]?.focus());
    }

    function handleTriggerKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
        if (event.key === "Escape") {
            setOpen(false);
        } else if (event.key === "ArrowDown" || event.key === "ArrowUp") {
            event.preventDefault();
            setOpen(true);
            focusOption(event.key === "ArrowDown" ? 0 : options.length - 1);
        }
    }

    function handleOptionKeyDown(
        event: KeyboardEvent<HTMLButtonElement>,
        index: number,
    ) {
        if (event.key === "Escape") {
            setOpen(false);
            triggerRef.current?.focus();
        } else if (event.key === "Tab") {
            setOpen(false);
        } else if (event.key === "ArrowDown" || event.key === "ArrowUp") {
            event.preventDefault();
            const step = event.key === "ArrowDown" ? 1 : -1;
            optionRefs.current[
                (index + step + options.length) % options.length
            ]?.focus();
        }
    }

    return (
        <div
            className={`customer-search-dropdown customer-search-dropdown--${variant}${isActive ? " is-active" : ""}`}
            ref={rootRef}
        >
            <button
                ref={triggerRef}
                type="button"
                className="customer-search-dropdown-trigger"
                aria-label={`${label}: ${selected?.label ?? value}`}
                aria-haspopup="menu"
                aria-expanded={open}
                aria-controls={open ? menuId : undefined}
                onClick={() => setOpen((current) => !current)}
                onKeyDown={handleTriggerKeyDown}
            >
                <span className="customer-search-dropdown-copy">
                    <span className="customer-search-dropdown-label">
                        {label}
                    </span>
                    <strong>{selected?.label ?? value}</strong>
                </span>
                <CustomerIcon name="chevron" size={17} />
            </button>
            {open && (
                <div
                    className={`customer-search-dropdown-menu${align === "right" ? " align-right" : ""}`}
                    id={menuId}
                    role="menu"
                    aria-label={label}
                >
                    {options.map((option, index) => (
                        <button
                            key={option.value}
                            ref={(element) => {
                                optionRefs.current[index] = element;
                            }}
                            type="button"
                            role="menuitemradio"
                            aria-checked={option.value === value}
                            className={option.value === value ? "selected" : ""}
                            onClick={() => {
                                onChange(option.value);
                                setOpen(false);
                                triggerRef.current?.focus();
                            }}
                            onKeyDown={(event) =>
                                handleOptionKeyDown(event, index)
                            }
                        >
                            <span className="customer-search-dropdown-option-label">
                                {option.icon && (
                                    <CustomerIcon
                                        name={option.icon}
                                        size={17}
                                    />
                                )}
                                {option.label}
                            </span>
                            {option.value === value && (
                                <CustomerIcon name="check" size={16} />
                            )}
                        </button>
                    ))}
                </div>
            )}
        </div>
    );
}
