"use client";

import React, { useEffect, useRef, useState } from "react";

export interface ExcelExportButtonProps {
    filename?: string;
    sheetName?: string;
    data: (string | number)[][];
    title?: string;
    lotName?: string;
    supportCsv?: boolean;
}

/**
 * Xuất dữ liệu ra file CSV chuẩn UTF-8 kèm Byte Order Mark (BOM) để Microsoft Excel hiển thị tiếng Việt không bị lỗi font.
 */
export function exportToCsv(
    data: (string | number)[][],
    filename: string = "bao-cao-doanh-thu.csv",
) {
    const csvContent =
        "\uFEFF" +
        data
            .map((row) =>
                row
                    .map((cell) => {
                        const val =
                            cell === null || cell === undefined
                                ? ""
                                : String(cell);
                        if (
                            val.includes(",") ||
                            val.includes('"') ||
                            val.includes("\n") ||
                            val.includes("\r")
                        ) {
                            return `"${val.replace(/"/g, '""')}"`;
                        }
                        return val;
                    })
                    .join(","),
            )
            .join("\r\n");

    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    const cleanFilename = filename.replace(/\.xlsx$/i, ".csv");
    link.setAttribute("download", cleanFilename);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
}

/**
 * Xuất dữ liệu ra file bảng tính Microsoft Excel (.xlsx).
 */
export async function exportToExcel(
    data: (string | number)[][],
    filename: string = "bao-cao-doanh-thu.xlsx",
    sheetName: string = "Báo cáo",
) {
    const XLSX = await import("xlsx");
    const ws = XLSX.utils.aoa_to_sheet(data);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, sheetName);
    const cleanFilename = filename.endsWith(".xlsx")
        ? filename
        : `${filename.replace(/\.csv$/i, "")}.xlsx`;
    XLSX.writeFile(wb, cleanFilename);
}

export function ExcelExportButton({
    filename = "bao-cao-doanh-thu.xlsx",
    sheetName = "Báo cáo",
    data,
    title = "Xuất file",
    supportCsv = true,
}: ExcelExportButtonProps) {
    const [exporting, setExporting] = useState(false);
    const [menuOpen, setMenuOpen] = useState(false);
    const dropdownRef = useRef<HTMLDivElement>(null);

    // Đóng dropdown khi click ra ngoài
    useEffect(() => {
        function handleClickOutside(event: MouseEvent) {
            if (
                dropdownRef.current &&
                !dropdownRef.current.contains(event.target as Node)
            ) {
                setMenuOpen(false);
            }
        }
        if (menuOpen) {
            document.addEventListener("mousedown", handleClickOutside);
        }
        return () => {
            document.removeEventListener("mousedown", handleClickOutside);
        };
    }, [menuOpen]);

    const handleExport = async (format: "xlsx" | "csv") => {
        setMenuOpen(false);
        try {
            setExporting(true);
            if (format === "csv") {
                exportToCsv(data, filename);
            } else {
                await exportToExcel(data, filename, sheetName);
            }
        } catch (err) {
            console.error("Export error:", err);
            alert("Không thể xuất file báo cáo. Vui lòng thử lại sau.");
        } finally {
            setExporting(false);
        }
    };

    return (
        <div
            ref={dropdownRef}
            style={{ position: "relative", display: "inline-flex" }}
        >
            <div
                style={{
                    display: "inline-flex",
                    alignItems: "stretch",
                    background: "#fff",
                    border: "1px solid #cbd5e1",
                    borderRadius: 10,
                    boxShadow: "0 1px 2px rgba(15,27,61,0.04)",
                    overflow: "hidden",
                }}
            >
                {/* Nút hành động chính (Xuất Excel) */}
                <button
                    type="button"
                    onClick={() => handleExport("xlsx")}
                    disabled={exporting}
                    data-testid="export-excel-btn"
                    aria-label="Xuất file Excel (.xlsx)"
                    style={{
                        display: "inline-flex",
                        alignItems: "center",
                        gap: 8,
                        background: "transparent",
                        border: "none",
                        padding: "8px 14px",
                        fontSize: 13,
                        fontWeight: 650,
                        color: "#0f172a",
                        cursor: exporting ? "not-allowed" : "pointer",
                    }}
                >
                    <svg
                        width="16"
                        height="16"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="#16a36a"
                        strokeWidth="2.2"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                    >
                        <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                        <polyline points="7 10 12 15 17 10" />
                        <line x1="12" y1="15" x2="12" y2="3" />
                    </svg>
                    <span>
                        {exporting
                            ? "Đang xuất..."
                            : title === "Xuất file"
                              ? "Xuất Excel"
                              : title}
                    </span>
                </button>

                {/* Nút mũi tên mở dropdown chọn Excel hoặc CSV */}
                {supportCsv && (
                    <button
                        type="button"
                        onClick={() => setMenuOpen((prev) => !prev)}
                        disabled={exporting}
                        data-testid="export-dropdown-toggle"
                        aria-label="Tùy chọn định dạng xuất file"
                        aria-expanded={menuOpen}
                        style={{
                            display: "inline-flex",
                            alignItems: "center",
                            justifyContent: "center",
                            background: "#f8fafc",
                            border: "none",
                            borderLeft: "1px solid #e2e8f0",
                            padding: "0 10px",
                            cursor: exporting ? "not-allowed" : "pointer",
                            color: "#475569",
                        }}
                    >
                        <svg
                            width="14"
                            height="14"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2.2"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                        >
                            <polyline points="6 9 12 15 18 9" />
                        </svg>
                    </button>
                )}
            </div>

            {/* Menu thả xuống lựa chọn định dạng */}
            {menuOpen && (
                <div
                    role="menu"
                    style={{
                        position: "absolute",
                        top: "calc(100% + 6px)",
                        right: 0,
                        background: "#fff",
                        border: "1px solid #e2e8f0",
                        borderRadius: 12,
                        boxShadow:
                            "0 10px 25px -5px rgba(0,0,0,0.1), 0 8px 10px -6px rgba(0,0,0,0.1)",
                        zIndex: 50,
                        minWidth: 220,
                        padding: "6px",
                    }}
                >
                    <button
                        type="button"
                        role="menuitem"
                        data-testid="export-option-xlsx"
                        onClick={() => handleExport("xlsx")}
                        style={{
                            width: "100%",
                            display: "flex",
                            alignItems: "center",
                            gap: 10,
                            padding: "9px 12px",
                            borderRadius: 8,
                            border: "none",
                            background: "transparent",
                            fontSize: 13,
                            fontWeight: 600,
                            color: "#0f172a",
                            cursor: "pointer",
                            textAlign: "left",
                        }}
                        onMouseEnter={(e) => {
                            e.currentTarget.style.background = "#f1f5f9";
                        }}
                        onMouseLeave={(e) => {
                            e.currentTarget.style.background = "transparent";
                        }}
                    >
                        <svg
                            width="16"
                            height="16"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="#16a36a"
                            strokeWidth="2"
                        >
                            <rect
                                x="3"
                                y="3"
                                width="18"
                                height="18"
                                rx="2"
                                ry="2"
                            />
                            <line x1="3" y1="9" x2="21" y2="9" />
                            <line x1="9" y1="21" x2="9" y2="9" />
                        </svg>
                        <div>
                            <div>Bảng tính Excel (.xlsx)</div>
                            <div style={{ fontSize: 11, color: "#64748b" }}>
                                Định dạng chuẩn Microsoft Excel
                            </div>
                        </div>
                    </button>

                    <button
                        type="button"
                        role="menuitem"
                        data-testid="export-option-csv"
                        onClick={() => handleExport("csv")}
                        style={{
                            width: "100%",
                            display: "flex",
                            alignItems: "center",
                            gap: 10,
                            padding: "9px 12px",
                            borderRadius: 8,
                            border: "none",
                            background: "transparent",
                            fontSize: 13,
                            fontWeight: 600,
                            color: "#0f172a",
                            cursor: "pointer",
                            textAlign: "left",
                        }}
                        onMouseEnter={(e) => {
                            e.currentTarget.style.background = "#f1f5f9";
                        }}
                        onMouseLeave={(e) => {
                            e.currentTarget.style.background = "transparent";
                        }}
                    >
                        <svg
                            width="16"
                            height="16"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="#0284c7"
                            strokeWidth="2"
                        >
                            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                            <polyline points="14 2 14 8 20 8" />
                            <line x1="16" y1="13" x2="8" y2="13" />
                            <line x1="16" y1="17" x2="8" y2="17" />
                            <polyline points="10 9 9 9 8 9" />
                        </svg>
                        <div>
                            <div>Tệp văn bản CSV (.csv)</div>
                            <div style={{ fontSize: 11, color: "#64748b" }}>
                                Hỗ trợ UTF-8, Google Sheets, Power BI
                            </div>
                        </div>
                    </button>
                </div>
            )}
        </div>
    );
}
