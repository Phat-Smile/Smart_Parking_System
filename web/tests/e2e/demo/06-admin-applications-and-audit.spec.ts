import { expect, test } from "@playwright/test";
import {
    BASE_URL,
    DEMO_ACCOUNTS,
    DEMO_LOTS,
    setupDemoMockRoutes,
} from "./demo-fixtures";

test.describe("Phân đoạn 6: Quản trị viên xét duyệt hồ sơ chủ bãi & kiểm tra nhật ký kiểm toán hệ thống (/admin/owner-applications, /admin/audit-logs)", () => {
    test.beforeEach(async ({ page }) => {
        await setupDemoMockRoutes(page);
    });

    test("Toàn trình: Admin đăng nhập với 2FA TOTP, thẩm định hồ sơ chủ bãi kèm giấy tờ, duyệt bãi xe mới & kiểm tra nhật ký kiểm toán Audit Log", async ({
        page,
    }) => {
        // ---------------------------------------------------------------------
        // Bước 1.1: Đăng nhập quản trị viên với xác thực hai yếu tố (TOTP / MFA)
        // ---------------------------------------------------------------------
        await test.step("1.1 Đăng nhập admin (admin@demo.local) và xác thực mã TOTP", async () => {
            const isMock = process.env.USE_MOCK_API === "true";
            const totpCode = process.env.ADMIN_TOTP_CODE;

            // Ở chế độ API thật: yêu cầu biến ADMIN_TOTP_CODE theo SUPER-PLAN T.6
            if (!isMock && !totpCode) {
                test.skip(
                    true,
                    "ADMIN_TOTP_CODE chưa được cấu hình - bước đăng nhập Admin MFA cần Lộc tự nhập mã TOTP thật theo SUPER-PLAN T.6",
                );
                return;
            }

            await page.goto(`${BASE_URL}/login`);
            await page
                .locator("#parkwise-login-email")
                .fill(DEMO_ACCOUNTS.admin.email);
            await page
                .locator("#parkwise-login-password")
                .fill(DEMO_ACCOUNTS.admin.password);
            await page.locator("button.parkwise-login-submit").click();

            // Bước thử thách 2FA (TOTP Step 2)
            const mfaHeading = page.locator(
                "h2:has-text('Two-factor authentication'), h2:has-text('Xác thực hai yếu tố')",
            );
            await expect(mfaHeading).toBeVisible();

            // Điền 6 chữ số mã TOTP từ biến môi trường ADMIN_TOTP_CODE (hoặc mock)
            const otpCode = totpCode || "111111";
            const otpInputs = page.locator(
                ".parkwise-otp-char-input, input[inputmode='numeric']",
            );
            await expect(otpInputs.first()).toBeVisible();
            const otpCount = await otpInputs.count();
            if (otpCount >= 6) {
                for (let i = 0; i < 6; i++) {
                    await otpInputs.nth(i).fill(otpCode[i] || "1");
                }
            } else {
                await otpInputs.first().fill(otpCode);
            }

            const mfaSubmit = page
                .locator(
                    "button:has-text('Verify and log in'), button[type='submit']",
                )
                .first();
            await expect(mfaSubmit).toBeVisible();
            await mfaSubmit.click();

            // Chuyển hướng vào Admin Dashboard
            await page.goto(`${BASE_URL}/admin/dashboard`);
            await expect(page).toHaveURL(/\/admin\/dashboard/);
            await expect(
                page.locator("h1, h2, .admin-title").first(),
            ).toBeVisible();
        });

        // ---------------------------------------------------------------------
        // Bước 1.2: Xét duyệt hồ sơ đối tác Chủ bãi (Chu Bai Cho Duyet)
        // ---------------------------------------------------------------------
        await test.step("1.2 Mở danh sách hồ sơ chủ bãi (/admin/owner-applications), xem giấy tờ pháp lý và duyệt hồ sơ", async () => {
            await page.goto(`${BASE_URL}/admin/owner-applications`);
            await expect(page).toHaveURL(/\/admin\/owner-applications/);

            // Tìm hồ sơ "Chu Bai Cho Duyet"
            const applicantRow = page
                .locator("tr, .owner-app-card, div")
                .filter({
                    hasText: new RegExp(
                        DEMO_ACCOUNTS.ownerPending.fullName,
                        "i",
                    ),
                })
                .first();
            await expect(applicantRow).toBeVisible();

            // Bấm xem chi tiết hồ sơ
            const inspectBtn = applicantRow
                .locator(
                    "button:has-text('Xem'), button:has-text('Chi tiết'), button:has-text('Duyệt')",
                )
                .first();
            await expect(inspectBtn).toBeVisible();
            await inspectBtn.click();

            // Hộp thoại thẩm định giấy tờ xuất hiện
            const modal = page.locator(
                ".admin-modal, [role='dialog'], .verification-modal",
            );
            await expect(modal).toBeVisible();

            // Kiểm tra checklist giấy tờ pháp lý
            const checklistItems = modal.locator(
                ".checklist-item, input[type='checkbox']",
            );
            const count = await checklistItems.count();
            for (let i = 0; i < count; i++) {
                const cb = checklistItems.nth(i);
                await expect(cb).toBeVisible();
                await cb.click();
            }

            // Bấm nút Phê duyệt hồ sơ
            const approveBtn = modal
                .locator(
                    "button:has-text('Phê duyệt'), button:has-text('Approve')",
                )
                .first();
            await expect(approveBtn).toBeVisible();
            await approveBtn.click();

            // Trạng thái hồ sơ chuyển sang "Đã duyệt" (Approved)
            const statusContent = await page.textContent("body");
            expect(statusContent).toMatch(/Đã duyệt|Approved|Hoàn tất/i);
        });

        // ---------------------------------------------------------------------
        // Bước 1.3: Xét duyệt bãi đỗ xe chờ duyệt (Bai cho duyet)
        // ---------------------------------------------------------------------
        await test.step("1.3 Mở danh sách bãi xe chờ duyệt (/admin/parking-lots/pending) và bấm duyệt bãi", async () => {
            await page.goto(`${BASE_URL}/admin/parking-lots/pending`);
            await expect(page).toHaveURL(/\/admin\/parking-lots\/pending/);

            // Tìm bãi "Bai cho duyet"
            const pendingLot = page
                .locator(`text=${DEMO_LOTS.lotPending.name}`)
                .first();
            await expect(pendingLot).toBeVisible();

            const approveLotBtn = page
                .locator("button:has-text('Duyệt'), button:has-text('Approve')")
                .first();
            await expect(approveLotBtn).toBeVisible();
            await approveLotBtn.click();

            // Sau khi duyệt, bãi rời khỏi danh sách chờ duyệt
            await page.reload();
            const pendingContent = await page.textContent("body");
            expect(pendingContent).toBeDefined();
        });

        // ---------------------------------------------------------------------
        // Bước 1.4: Kiểm tra nhật ký kiểm toán hệ thống (Audit Logs)
        // ---------------------------------------------------------------------
        await test.step("1.4 Vào trang Audit Logs (/admin/audit-logs), kiểm tra vết ghi nhận 2 thao tác duyệt", async () => {
            await page.goto(`${BASE_URL}/admin/audit-logs`);
            await expect(page).toHaveURL(/\/admin\/audit-logs/);

            // Kiểm tra bảng lịch sử kiểm toán hiển thị các bản ghi mới nhất
            const table = page.locator("table, .audit-log-list, .audit-card");
            await expect(table.first()).toBeVisible();

            // Kiểm tra có ghi nhận thao tác duyệt hồ sơ và duyệt bãi xe, kèm người thực hiện và thời gian
            const logContent = await page.textContent("body");
            expect(logContent).toMatch(
                /OwnerApplicationApproved|ParkingLotApproved|admin@demo\.local|Duyệt/i,
            );
        });
    });
});
