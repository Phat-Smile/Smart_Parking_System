"use client";

import React, { useState, useRef, useEffect, useMemo } from "react";
import { useRouter } from "next/navigation";
import { useOwnerStore } from "@/store/useOwnerStore";

export interface OwnerPageHeaderProps {
    breadcrumb?: string;
    title: string;
    subtitle?: string;
    actions?: React.ReactNode;
}

export function OwnerPageHeader({
    breadcrumb,
    title,
    subtitle,
    actions,
}: OwnerPageHeaderProps) {
    const router = useRouter();
    const { lots, reservations, spots } = useOwnerStore();

    const [searchQuery, setSearchQuery] = useState("");
    const [searchOpen, setSearchOpen] = useState(false);

    const searchRef = useRef<HTMLDivElement>(null);

    // Close search popup on click outside or Escape
    useEffect(() => {
        function handleClickOutside(e: MouseEvent) {
            if (
                searchRef.current &&
                !searchRef.current.contains(e.target as Node)
            ) {
                setSearchOpen(false);
            }
        }

        function handleKeyDown(e: KeyboardEvent) {
            if (e.key === "Escape") {
                setSearchOpen(false);
            }
        }

        document.addEventListener("mousedown", handleClickOutside);
        document.addEventListener("keydown", handleKeyDown);
        return () => {
            document.removeEventListener("mousedown", handleClickOutside);
            document.removeEventListener("keydown", handleKeyDown);
        };
    }, []);

    // Global search filtering
    const searchResults = useMemo(() => {
        const q = searchQuery.trim().toLowerCase();
        if (!q) return null;

        const matchedLots = lots.filter(
            (l) =>
                l.name.toLowerCase().includes(q) ||
                (l.addr && l.addr.toLowerCase().includes(q)),
        );

        const matchedReservations = reservations.filter(
            (r) =>
                r.id.toLowerCase().includes(q) ||
                r.plate.toLowerCase().includes(q) ||
                r.slot.toLowerCase().includes(q),
        );

        // From active sessions/spots
        const matchedSpots = spots.filter(
            (s) =>
                (s.p && s.p.toLowerCase().includes(q)) ||
                s.id.toLowerCase().includes(q),
        );

        return {
            lots: matchedLots.slice(0, 3),
            reservations: matchedReservations.slice(0, 3),
            spots: matchedSpots.slice(0, 3),
            total:
                matchedLots.length +
                matchedReservations.length +
                matchedSpots.length,
        };
    }, [searchQuery, lots, reservations, spots]);

    return (
        <header
            className="ov2-header"
            style={{
                position: "relative",
                marginBottom: 24,
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                flexWrap: "wrap",
                gap: 16,
            }}
        >
            <div className="ov2-header-left">
                <h1
                    style={{
                        fontSize: "clamp(20px, 4.5vw, 26px)",
                        fontWeight: 800,
                        margin: 0,
                        color: "#0f1b3d",
                        lineHeight: 1.25,
                    }}
                >
                    {title}
                </h1>
                {subtitle && (
                    <div
                        style={{
                            fontSize: 13.5,
                            color: "#6a7491",
                            marginTop: 4,
                        }}
                    >
                        {subtitle}
                    </div>
                )}
            </div>

            <div
                className="ov2-header-right"
                style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 16,
                    flexWrap: "wrap",
                }}
            >
                {actions && <div className="ov2-header-actions">{actions}</div>}

                {/* Global Search */}
                <div ref={searchRef} style={{ position: "relative", maxWidth: "100%" }}>
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            background: "#f3f6fb",
                            borderRadius: 999,
                            padding: "8px 16px",
                            width: "100%",
                            maxWidth: 280,
                            border: "1px solid #e3e8f2",
                        }}
                    >
                        <svg
                            width="16"
                            height="16"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="#6a7491"
                            strokeWidth="2"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                            style={{ marginRight: 8, flexShrink: 0 }}
                        >
                            <circle cx="11" cy="11" r="8" />
                            <line x1="21" y1="21" x2="16.65" y2="16.65" />
                        </svg>
                        <input
                            type="text"
                            placeholder="Tìm biển số, bãi, mã đặt chỗ"
                            value={searchQuery}
                            onChange={(e) => {
                                setSearchQuery(e.target.value);
                                setSearchOpen(true);
                            }}
                            onFocus={() => setSearchOpen(true)}
                            aria-label="Tìm kiếm toàn cục"
                            style={{
                                border: "none",
                                background: "transparent",
                                outline: "none",
                                width: "100%",
                                fontSize: 13,
                                color: "#0f1b3d",
                            }}
                        />
                    </div>

                    {/* Search Dropdown Results */}
                    {searchOpen && searchQuery.trim() !== "" && (
                        <div
                            style={{
                                position: "absolute",
                                top: "calc(100% + 8px)",
                                right: 0,
                                width: "min(360px, calc(100vw - 32px))",
                                background: "#fff",
                                borderRadius: 14,
                                boxShadow: "0 10px 25px rgba(15,27,61,0.12)",
                                border: "1px solid #e3e8f2",
                                zIndex: 100,
                                padding: "12px 0",
                                maxHeight: 380,
                                overflowY: "auto",
                            }}
                        >
                            {searchResults && searchResults.total > 0 ? (
                                <>
                                    {searchResults.lots.length > 0 && (
                                        <div style={{ marginBottom: 8 }}>
                                            <div
                                                style={{
                                                    fontSize: 11,
                                                    fontWeight: 700,
                                                    textTransform: "uppercase",
                                                    color: "#6a7491",
                                                    padding: "4px 16px",
                                                    letterSpacing: 0.5,
                                                }}
                                            >
                                                Bãi của tôi
                                            </div>
                                            {searchResults.lots.map((lot) => (
                                                <div
                                                    key={lot.id}
                                                    onClick={() => {
                                                        setSearchOpen(false);
                                                        router.push(
                                                            "/owner/parking-lots",
                                                        );
                                                    }}
                                                    style={{
                                                        padding: "8px 16px",
                                                        cursor: "pointer",
                                                        display: "flex",
                                                        alignItems: "center",
                                                        gap: 10,
                                                        fontSize: 13,
                                                        color: "#0f1b3d",
                                                    }}
                                                    onMouseEnter={(e) =>
                                                        (e.currentTarget.style.backgroundColor =
                                                            "#f3f6fb")
                                                    }
                                                    onMouseLeave={(e) =>
                                                        (e.currentTarget.style.backgroundColor =
                                                            "transparent")
                                                    }
                                                >
                                                    <span
                                                        style={{
                                                            fontWeight: 600,
                                                        }}
                                                    >
                                                        {lot.name}
                                                    </span>
                                                    <span
                                                        style={{
                                                            fontSize: 12,
                                                            color: "#6a7491",
                                                        }}
                                                    >
                                                        {lot.addr}
                                                    </span>
                                                </div>
                                            ))}
                                        </div>
                                    )}

                                    {searchResults.reservations.length > 0 && (
                                        <div style={{ marginBottom: 8 }}>
                                            <div
                                                style={{
                                                    fontSize: 11,
                                                    fontWeight: 700,
                                                    textTransform: "uppercase",
                                                    color: "#6a7491",
                                                    padding: "4px 16px",
                                                    letterSpacing: 0.5,
                                                }}
                                            >
                                                Đặt chỗ
                                            </div>
                                            {searchResults.reservations.map(
                                                (res) => (
                                                    <div
                                                        key={res.id}
                                                        onClick={() => {
                                                            setSearchOpen(
                                                                false,
                                                            );
                                                            router.push(
                                                                `/owner/reservations?q=${encodeURIComponent(res.id)}`,
                                                            );
                                                        }}
                                                        style={{
                                                            padding: "8px 16px",
                                                            cursor: "pointer",
                                                            display: "flex",
                                                            alignItems:
                                                                "center",
                                                            justifyContent:
                                                                "space-between",
                                                            fontSize: 13,
                                                            color: "#0f1b3d",
                                                        }}
                                                        onMouseEnter={(e) =>
                                                            (e.currentTarget.style.backgroundColor =
                                                                "#f3f6fb")
                                                        }
                                                        onMouseLeave={(e) =>
                                                            (e.currentTarget.style.backgroundColor =
                                                                "transparent")
                                                        }
                                                    >
                                                        <div>
                                                            <span
                                                                style={{
                                                                    fontWeight: 600,
                                                                }}
                                                            >
                                                                {res.id}
                                                            </span>
                                                            <span
                                                                style={{
                                                                    color: "#6a7491",
                                                                    marginLeft: 8,
                                                                }}
                                                            >
                                                                {res.plate} · Ô{" "}
                                                                {res.slot}
                                                            </span>
                                                        </div>
                                                        <span
                                                            style={{
                                                                fontSize: 12,
                                                                color: "#2563eb",
                                                            }}
                                                        >
                                                            {res.status}
                                                        </span>
                                                    </div>
                                                ),
                                            )}
                                        </div>
                                    )}

                                    {searchResults.spots.length > 0 && (
                                        <div>
                                            <div
                                                style={{
                                                    fontSize: 11,
                                                    fontWeight: 700,
                                                    textTransform: "uppercase",
                                                    color: "#6a7491",
                                                    padding: "4px 16px",
                                                    letterSpacing: 0.5,
                                                }}
                                            >
                                                Phiên đỗ xe
                                            </div>
                                            {searchResults.spots.map((s) => (
                                                <div
                                                    key={s.id}
                                                    onClick={() => {
                                                        setSearchOpen(false);
                                                        router.push(
                                                            `/owner/sessions?q=${encodeURIComponent(s.p || s.id)}`,
                                                        );
                                                    }}
                                                    style={{
                                                        padding: "8px 16px",
                                                        cursor: "pointer",
                                                        display: "flex",
                                                        alignItems: "center",
                                                        justifyContent:
                                                            "space-between",
                                                        fontSize: 13,
                                                        color: "#0f1b3d",
                                                    }}
                                                    onMouseEnter={(e) =>
                                                        (e.currentTarget.style.backgroundColor =
                                                            "#f3f6fb")
                                                    }
                                                    onMouseLeave={(e) =>
                                                        (e.currentTarget.style.backgroundColor =
                                                            "transparent")
                                                    }
                                                >
                                                    <div>
                                                        <span
                                                            style={{
                                                                fontWeight: 600,
                                                            }}
                                                        >
                                                            {s.p || s.id}
                                                        </span>
                                                        <span
                                                            style={{
                                                                color: "#6a7491",
                                                                marginLeft: 8,
                                                            }}
                                                        >
                                                            Ô {s.id} (Tầng {s.f}
                                                            )
                                                        </span>
                                                    </div>
                                                </div>
                                            ))}
                                        </div>
                                    )}
                                </>
                            ) : (
                                <div
                                    style={{
                                        padding: "16px",
                                        textAlign: "center",
                                        color: "#6a7491",
                                        fontSize: 13,
                                    }}
                                >
                                    Không tìm thấy kết quả
                                </div>
                            )}
                        </div>
                    )}
                </div>
            </div>
        </header>
    );
}
