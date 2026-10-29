"use client";

import {
    useEffect,
    useRef,
    useState,
    type KeyboardEvent,
    type ReactNode,
} from "react";

export type VehicleOption = { value: string; label: string };

type Props = {
    id: string;
    value: string;
    options: VehicleOption[];
    placeholder: string;
    onChange: (value: string) => void;
    searchable?: boolean;
    otherLabel?: string;
    emptyMessage?: string;
    leadingIcon?: ReactNode;
    required?: boolean;
    invalid?: boolean;
    describedBy?: string;
};

export function VehicleDropdown({
    id,
    value,
    options,
    placeholder,
    onChange,
    searchable = false,
    otherLabel,
    emptyMessage = "No matching options",
    leadingIcon,
    required = false,
    invalid = false,
    describedBy,
}: Props) {
    const [open, setOpen] = useState(false);
    const [filtering, setFiltering] = useState(false);
    const [custom, setCustom] = useState(false);
    const [opensUp, setOpensUp] = useState(false);
    const [activeIndex, setActiveIndex] = useState(0);
    const rootRef = useRef<HTMLDivElement>(null);
    const inputRef = useRef<HTMLInputElement>(null);
    const listId = `${id}-listbox`;
    const query = value.trim().toLocaleLowerCase();
    const visibleOptions = filtering
        ? options.filter((option) =>
              option.label.toLocaleLowerCase().includes(query),
          )
        : options;
    const itemCount = visibleOptions.length + (otherLabel ? 1 : 0);
    const displayValue =
        searchable || !value
            ? value
            : (options.find((option) => option.value === value)?.label ??
              value);

    useEffect(() => {
        function closeOnOutsideClick(event: PointerEvent) {
            if (!rootRef.current?.contains(event.target as Node)) {
                setOpen(false);
                setFiltering(false);
            }
        }
        document.addEventListener("pointerdown", closeOnOutsideClick);
        return () =>
            document.removeEventListener("pointerdown", closeOnOutsideClick);
    }, []);

    function updatePlacement() {
        const field = rootRef.current?.getBoundingClientRect();
        const dialog = rootRef.current
            ?.closest("dialog")
            ?.getBoundingClientRect();
        if (!field) return;
        const spaceBelow =
            Math.min(window.innerHeight, dialog?.bottom ?? Infinity) -
            field.bottom;
        const spaceAbove = field.top - Math.max(0, dialog?.top ?? 0);
        setOpensUp(spaceBelow < 230 && spaceAbove > spaceBelow);
    }

    function choose(option: VehicleOption) {
        onChange(option.value);
        setCustom(false);
        setFiltering(false);
        setOpen(false);
        inputRef.current?.focus();
    }

    function chooseOther() {
        if (options.some((option) => option.value === value)) onChange("");
        setCustom(true);
        setFiltering(false);
        setOpen(false);
        inputRef.current?.focus();
    }

    function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
        if (event.key === "Escape") {
            setOpen(false);
            setFiltering(false);
            return;
        }
        if (event.key === "Tab") {
            setOpen(false);
            return;
        }
        if (event.key === "ArrowDown" || event.key === "ArrowUp") {
            event.preventDefault();
            updatePlacement();
            setOpen(true);
            if (!itemCount) return;
            setActiveIndex((index) =>
                event.key === "ArrowDown"
                    ? (index + 1) % itemCount
                    : (index - 1 + itemCount) % itemCount,
            );
        }
        if (event.key === "Enter" && open && itemCount) {
            event.preventDefault();
            if (activeIndex < visibleOptions.length) {
                choose(visibleOptions[activeIndex]);
            } else {
                chooseOther();
            }
        }
    }

    return (
        <div
            ref={rootRef}
            className={`vehicles-dropdown${open ? " is-open" : ""}${opensUp ? " opens-up" : ""}${leadingIcon && value ? " has-icon" : ""}`}
        >
            {leadingIcon && value && (
                <span className="vehicles-dropdown-icon" aria-hidden="true">
                    {leadingIcon}
                </span>
            )}
            <input
                ref={inputRef}
                id={id}
                className="vehicles-dropdown-input"
                role="combobox"
                aria-autocomplete={searchable ? "list" : "none"}
                aria-expanded={open}
                aria-controls={listId}
                aria-required={required}
                aria-invalid={invalid}
                aria-describedby={describedBy}
                aria-activedescendant={
                    open && itemCount
                        ? `${listId}-option-${activeIndex}`
                        : undefined
                }
                value={displayValue}
                readOnly={!searchable}
                placeholder={custom ? "Enter manually" : placeholder}
                autoComplete="off"
                onClick={() => {
                    updatePlacement();
                    setOpen((current) => !current);
                    setActiveIndex(0);
                }}
                onChange={(event) => {
                    updatePlacement();
                    onChange(event.target.value);
                    setFiltering(true);
                    setOpen(true);
                    setActiveIndex(0);
                }}
                onKeyDown={handleKeyDown}
            />
            <button
                type="button"
                className="vehicles-dropdown-toggle"
                aria-label={`Show ${placeholder.toLowerCase()} options`}
                aria-expanded={open}
                aria-controls={listId}
                onClick={() => {
                    updatePlacement();
                    setOpen((current) => !current);
                    setFiltering(false);
                    setActiveIndex(0);
                    inputRef.current?.focus();
                }}
            >
                <svg
                    width="18"
                    height="18"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    aria-hidden="true"
                >
                    <path d="m6 9 6 6 6-6" />
                </svg>
            </button>
            {open && (
                <div
                    id={listId}
                    className="vehicles-dropdown-menu"
                    role="listbox"
                >
                    {visibleOptions.length === 0 && (
                        <p className="vehicles-dropdown-empty">
                            {emptyMessage}
                        </p>
                    )}
                    {visibleOptions.map((option, index) => (
                        <button
                            key={option.value}
                            id={`${listId}-option-${index}`}
                            type="button"
                            role="option"
                            aria-selected={value === option.value}
                            className={`vehicles-dropdown-option${activeIndex === index ? " is-active" : ""}`}
                            onMouseDown={(event) => event.preventDefault()}
                            onMouseEnter={() => setActiveIndex(index)}
                            onClick={() => choose(option)}
                        >
                            {option.label}
                        </button>
                    ))}
                    {otherLabel && (
                        <button
                            id={`${listId}-option-${visibleOptions.length}`}
                            type="button"
                            role="option"
                            aria-selected={custom}
                            className={`vehicles-dropdown-option vehicles-dropdown-other${activeIndex === visibleOptions.length ? " is-active" : ""}`}
                            onMouseDown={(event) => event.preventDefault()}
                            onMouseEnter={() =>
                                setActiveIndex(visibleOptions.length)
                            }
                            onClick={chooseOther}
                        >
                            {otherLabel}
                        </button>
                    )}
                </div>
            )}
        </div>
    );
}
