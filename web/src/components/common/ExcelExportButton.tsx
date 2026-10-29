"use client";

import React, { useState } from "react";

export interface ExcelExportButtonProps {
    filename?: string;
    sheetName?: string;
    data: (string | number)[][];
    title?: string;
}

export function ExcelExportButton({
    filename = "bao-cao-doanh-thu.xlsx",
    sheetName = "Báo cáo",
    data,
    title = "Xuất file Excel",
}: ExcelExportButtonProps) {
    const [exporting, setExporting] = useState(false);

    const handleExport = async () => {
        try {
            setExporting(true);
            const XLSX = await import("xlsx");
            const ws = XLSX.utils.aoa_to_sheet(data);
            const wb = XLSX.utils.book_new();
            XLSX.utils.book_append_sheet(wb, ws, sheetName);
            XLSX.writeFile(wb, filename);
        } catch (err) {
            console.error("Export Excel error:", err);
            alert("Không thể xuất file Excel. Vui lòng thử lại sau.");
        } finally {
            setExporting(false);
        }
    };

    return (
        <button
            type="button"
            onClick={handleExport}
            disabled={exporting}
            aria-label={title}
            style={{
                display: "inline-flex",
                alignItems: "center",
                gap: 8,
                background: "#fff",
                border: "1px solid #e3e8f2",
                borderRadius: 10,
                padding: "8px 16px",
                fontSize: 13,
                fontWeight: 600,
                color: "#0f1b3d",
                cursor: exporting ? "not-allowed" : "pointer",
                boxShadow: "0 1px 2px rgba(15,27,61,0.04)",
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
                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                <polyline points="7 10 12 15 17 10" />
                <line x1="12" y1="15" x2="12" y2="3" />
            </svg>
            <span>{exporting ? "Đang xuất..." : title}</span>
        </button>
    );
}
