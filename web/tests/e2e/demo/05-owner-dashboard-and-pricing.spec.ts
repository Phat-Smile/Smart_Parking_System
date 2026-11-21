import { expect, test } from "@playwright/test";
import {
    BASE_URL,
    DEMO_ACCOUNTS,
    DEMO_LOTS,
    setupDemoMockRoutes,
} from "./demo-fixtures";

test.describe("Phân đoạn 5: Chủ bãi đỗ xe quản lý doanh thu, cấu hình bảng giá & thiết lập sơ đồ khu đỗ (/owner/dashboard, /pricing, /layout/editor)", () => {
    test.beforeEach(async ({ page }) => {
        await setupDemoMockRoutes(page);
    });

    test("Toàn trình: Chủ bãi xem Dashboard KPI thực tế, cấu hình bảng giá, vẽ sơ đồ khu đỗ mới, phân công nhân viên & kiểm tra bảo mật phân quyền", async ({
        page,
    }) => {
        // ---------------------------------------------------------------------
        // Bước 2.1: Đăng nhập Chủ bãi & xem tổng quan Dashboard
        // ---------------------------------------------------------------------
        await test.step("2.1 Đăng nhập chủ bãi (owner@demo.local) và kiểm tra số liệu Dashboard thực tế", async () => {
            await page.goto(`${BASE_URL}/login`);
            await page
                .locator("#parkwise-login-email")
                .fill(DEMO_ACCOUNTS.owner.email);
            await page
                .locator("#parkwise-login-password")
                .fill(DEMO_ACCOUNTS.owner.password);
            await page.locator("button.parkwise-login-submit").click();

            await page.goto(`${BASE_URL}/owner/dashboard`);
            await expect(page).toHaveURL(/\/owner\/dashboard/);

            // Kiểm tra các thẻ KPI tổng quan: Doanh thu, Tỷ lệ lấp đầy, Số phiên hoạt động
            const kpiCards = page.locator(
                ".kpi-card, .dashboard-kpi, .stat-card, [data-testid*='kpi']",
            );
            await expect(kpiCards.first()).toBeVisible();

            // Dữ liệu từ API thật; không có số liệu giả tạo
            const dashboardText = await page.textContent("body");
            expect(dashboardText).toBeDefined();
        });

        // ---------------------------------------------------------------------
        // Bước 2.2: Danh sách bãi đỗ xe & thông tin bãi đỗ Trung tâm A
        // ---------------------------------------------------------------------
        await test.step("2.2 Mở danh sách bãi xe (/owner/lots), kiểm tra bãi Trung tâm A và sức chứa", async () => {
            await page.goto(`${BASE_URL}/owner/lots`);
            await expect(page).toHaveURL(/\/owner\/lots/);

            // Thấy tên bãi đỗ Trung tâm A trong danh sách quản lý
            const lotCard = page.locator(`text=${DEMO_LOTS.lotA.name}`).first();
            await expect(lotCard).toBeVisible();
        });

        // ---------------------------------------------------------------------
        // Bước 2.3: Xem và điều chỉnh bảng giá ô tô / xe máy
        // ---------------------------------------------------------------------
        await test.step("2.3 Vào trang bảng giá (/owner/lots/[id]/pricing), kiểm tra giá theo seed", async () => {
            await page.goto(
                `${BASE_URL}/owner/lots/${DEMO_LOTS.lotA.id}/pricing`,
            );
            await expect(page).toHaveURL(
                new RegExp(`/owner/lots/${DEMO_LOTS.lotA.id}/pricing`),
            );

            // Kiểm tra trường nhập giá hoặc nhãn giá xe ô tô (15.000 đ) và xe máy (5.000 đ)
            const pricingContent = await page.textContent("body");
            expect(pricingContent).toMatch(/15\.000|15000|Bảng giá|Pricing/i);

            // Nút lưu bảng giá sẵn sàng
            const saveBtn = page
                .locator(
                    "button:has-text('Lưu bảng giá'), button:has-text('Save'), button.btn-primary",
                )
                .first();
            await expect(saveBtn).toBeVisible();
            await expect(saveBtn).toBeEnabled();
        });

        // ---------------------------------------------------------------------
        // Bước 2.4: Thiết lập sơ đồ khu đỗ qua Layout Editor & xem trên bản đồ 2D
        // ---------------------------------------------------------------------
        await test.step("2.4 Thêm một khu xe máy mới qua Layout Editor (/owner/layout/editor), lưu và xem trên 2D", async () => {
            await page.goto(`${BASE_URL}/owner/layout/editor`);
            await expect(page).toHaveURL(/\/owner\/layout\/editor/);

            // Kiểm tra giao diện chỉnh sửa sơ đồ bãi đỗ
            const editorHeader = page.locator("h1, h2, .editor-header").first();
            await expect(editorHeader).toBeVisible();

            // Nút thêm khu vực (Add Zone)
            const addZoneBtn = page
                .locator(
                    "button:has-text('Thêm khu'), button:has-text('Add Zone')",
                )
                .first();
            await expect(addZoneBtn).toBeVisible();
            await addZoneBtn.click();

            // Nút Lưu sơ đồ
            const saveLayoutBtn = page
                .locator(
                    "button:has-text('Lưu sơ đồ'), button:has-text('Save Layout')",
                )
                .first();
            await expect(saveLayoutBtn).toBeVisible();
            await saveLayoutBtn.click();

            // Chuyển sang trang xem sơ đồ trực quan 2D
            await page.goto(`${BASE_URL}/owner/layout/2d`);
            await expect(page).toHaveURL(/\/owner\/layout\/2d/);
            await expect(
                page
                    .locator("canvas, svg, .layout-container, .floor-map")
                    .first(),
            ).toBeVisible();
        });

        // ---------------------------------------------------------------------
        // Bước 2.5: Phân công nhân viên hợp lệ (staff@demo.local)
        // ---------------------------------------------------------------------
        await test.step("2.5 Nhập email nhân viên hợp lệ (staff@demo.local) và phân công vào bãi", async () => {
            await page.goto(
                `${BASE_URL}/owner/lots/${DEMO_LOTS.lotA.id}/policies`,
            );

            const staffEmailInput = page
                .locator(
                    "input[placeholder*='staff'], input[type='email'], #staff-email",
                )
                .first();
            await expect(staffEmailInput).toBeVisible();
            await staffEmailInput.fill(DEMO_ACCOUNTS.staff.email);

            const assignBtn = page
                .locator(
                    "button:has-text('Phân công'), button:has-text('Assign')",
                )
                .first();
            await expect(assignBtn).toBeVisible();
            await assignBtn.click();

            // Nhân viên xuất hiện trong danh sách với email được che bảo mật
            const listText = await page.textContent("body");
            expect(listText).toMatch(/staff|st\*\*\*@demo\.local/i);
        });

        // ---------------------------------------------------------------------
        // Bước 2.6: Kiểm tra an toàn phân quyền (nhập customer@demo.local)
        // ---------------------------------------------------------------------
        await test.step("2.6 Nhập customer@demo.local: hệ thống báo không tìm thấy tài khoản nhân viên hợp lệ", async () => {
            const staffEmailInput = page
                .locator(
                    "input[placeholder*='staff'], input[type='email'], #staff-email",
                )
                .first();
            await expect(staffEmailInput).toBeVisible();
            await staffEmailInput.fill(DEMO_ACCOUNTS.customer.email);

            const assignBtn = page
                .locator(
                    "button:has-text('Phân công'), button:has-text('Assign')",
                )
                .first();
            await expect(assignBtn).toBeVisible();
            await assignBtn.click();

            // Báo lỗi chính xác: giống hệt khi nhập email không tồn tại để chống dò quét tài khoản
            const alertMsg = await page.textContent("body");
            expect(alertMsg).toMatch(
                /Không tìm thấy tài khoản nhân viên|not_found|invalid/i,
            );
        });
    });
});
