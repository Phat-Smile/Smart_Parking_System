import { fireEvent, render, screen } from "@testing-library/react";
import React from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ExcelExportButton, exportToCsv } from "./ExcelExportButton";

describe("ExcelExportButton Component", () => {
    const mockData: (string | number)[][] = [
        ["Tiêu đề báo cáo"],
        ["STT", "Mã", "Số tiền"],
        [1, "TX-01", 50000],
        [2, "TX-02", 120000],
    ];

    beforeEach(() => {
        vi.clearAllMocks();
    });

    it("renders export button and dropdown toggle", () => {
        render(<ExcelExportButton data={mockData} />);
        expect(screen.getByTestId("export-excel-btn")).toBeDefined();
        expect(screen.getByTestId("export-dropdown-toggle")).toBeDefined();
    });

    it("toggles dropdown when supportCsv is true", () => {
        render(<ExcelExportButton data={mockData} supportCsv={true} />);
        const toggleBtn = screen.getByTestId("export-dropdown-toggle");
        fireEvent.click(toggleBtn);

        // Menu items should be visible
        expect(screen.getByTestId("export-option-xlsx")).toBeDefined();
        expect(screen.getByTestId("export-option-csv")).toBeDefined();

        // Click outside closes menu
        fireEvent.mouseDown(document.body);
        expect(screen.queryByTestId("export-option-xlsx")).toBeNull();
    });

    it("triggers CSV download on CSV option click", () => {
        const createObjectURLSpy = vi.fn().mockReturnValue("blob:mock-url");
        const revokeObjectURLSpy = vi.fn();
        global.URL.createObjectURL = createObjectURLSpy;
        global.URL.revokeObjectURL = revokeObjectURLSpy;

        const appendChildSpy = vi.spyOn(document.body, "appendChild");
        const removeChildSpy = vi.spyOn(document.body, "removeChild");

        render(
            <ExcelExportButton
                data={mockData}
                filename="test-report.xlsx"
                supportCsv={true}
            />,
        );
        const toggleBtn = screen.getByTestId("export-dropdown-toggle");
        fireEvent.click(toggleBtn);

        const csvOption = screen.getByTestId("export-option-csv");
        fireEvent.click(csvOption);

        expect(createObjectURLSpy).toHaveBeenCalled();
        expect(appendChildSpy).toHaveBeenCalled();
        expect(removeChildSpy).toHaveBeenCalled();
    });

    it("exportToCsv creates BOM and escaped CSV content", () => {
        const createObjectURLSpy = vi.fn().mockReturnValue("blob:mock-url");
        global.URL.createObjectURL = createObjectURLSpy;
        global.URL.revokeObjectURL = vi.fn();

        const testData = [
            ["Tên", "Ghi chú, có phẩy", 'Có dấu "ngoặc kép"'],
            ["Nguyễn Văn An", "Không phẩy", "Bình thường"],
        ];

        exportToCsv(testData, "test.csv");
        expect(createObjectURLSpy).toHaveBeenCalled();
    });
});
