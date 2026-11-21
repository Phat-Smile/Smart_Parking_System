import { expect, test } from "@playwright/test";
import {
    BASE_URL,
    DEMO_ACCOUNTS,
    DEMO_LOTS,
    setupDemoMockRoutes,
} from "./demo-fixtures";

test.describe("Phân đoạn 4: Xe ra bãi, so khớp biển số ANPR, quét mã QR phiên & mở barrier (/staff/exit)", () => {
    test.beforeEach(async ({ page }) => {
        await setupDemoMockRoutes(page);
    });

    test("Toàn trình: Quét QR tại cổng ra, kiểm tra chống gian lận lệch biển số, thanh toán, gia hạn cửa sổ ra bãi, mở barrier & cấp lại vé mất", async ({
        page,
    }) => {
        // ---------------------------------------------------------------------
        // Bước 5.2: Nhân viên mở màn hình cổng ra, quét mã QR của khách
        // ---------------------------------------------------------------------
        await test.step("5.2 Nhân viên trực cổng ra quét mã QR của phiên, ô biển số camera giữ trống (không tự điền)", async () => {
            await page.goto(`${BASE_URL}/staff/exit`);
            await expect(page).toHaveURL(/\/staff\/exit/);

            // Kiểm tra ô nhập mã QR
            const qrInput = page.locator("#staff-qr-input");
            await expect(qrInput).toBeVisible();

            // Nhập mã QR phiên đỗ xe
            await qrInput.fill("QR-PARKING-SESSION-999888");

            const verifyQrBtn = page
                .locator(
                    "button:has-text('Xác thực QR'), button:has-text('Verify')",
                )
                .first();
            await expect(verifyQrBtn).toBeEnabled();
            await verifyQrBtn.click();

            // Ô biển số đọc ở cổng ra (#staff-exit-plate) không được tự động điền từ phiên (luật BR-11 chống gian lận)
            const exitPlateInput = page.locator("#staff-exit-plate");
            await expect(exitPlateInput).toBeVisible();
            expect(await exitPlateInput.inputValue()).toBe("");
        });

        // ---------------------------------------------------------------------
        // Bước 5.3: Thử nhập biển số khác (51A-999.99) rồi cho xe ra -> Bị từ chối
        // ---------------------------------------------------------------------
        await test.step("5.3 Nhập biển số sai lệch (51A-999.99), checklist báo lỗi và barrier giữ khóa", async () => {
            const exitPlateInput = page.locator("#staff-exit-plate");
            await exitPlateInput.fill("51A-999.99");

            // Nút cho xe qua barrier phải bị vô hiệu hóa khi biển số không khớp
            const authorizeBtn = page
                .locator(
                    "button.staff-btn-primary.staff-btn-lg, button:has-text('Cho xe qua barrier'), button:has-text('Authorize')",
                )
                .first();

            await expect(authorizeBtn).toBeDisabled();

            // Mục kiểm tra biển số trong checklist báo dấu ✕
            const checklist = page.locator(".staff-checklist, .staff-card");
            await expect(checklist.first()).toBeVisible();
            const checklistText = await checklist.textContent();
            expect(checklistText).toMatch(/✕|Từ chối|Denied|Không khớp/i);
        });

        // ---------------------------------------------------------------------
        // Bước 5.4: Nhập đúng biển số (51A-123.45) & đối soát trạng thái thanh toán
        // ---------------------------------------------------------------------
        await test.step("5.4 Nhập đúng biển số 51A-123.45, xác nhận thanh toán đủ tiền", async () => {
            const exitPlateInput = page.locator("#staff-exit-plate");
            await exitPlateInput.fill("51A-123.45");

            // Checklist biển số chuyển sang trạng thái Hợp lệ (✓)
            const checklistText = await page
                .locator(".staff-checklist")
                .textContent();
            expect(checklistText).toMatch(/✓|Khớp|Passed/i);

            // Nếu còn thiếu tiền: có tùy chọn thu tiền mặt hoặc thanh toán VNPay
            // Nút thu tiền mặt chỉ xuất hiện khi xe còn nợ phí (amountDue > 0); do xe 51A-123.45 đã thanh toán qua ví ở bước 5.3 nên nút này là tùy chọn.
            const cashBtn = page.locator(
                "button:has-text('Thu tiền mặt'), button:has-text('Cash')",
            );
            if (await cashBtn.isVisible()) {
                await cashBtn.click();
                const confirmCashBtn = page
                    .locator("button:has-text('Xác nhận đã thu')")
                    .first();
                await expect(confirmCashBtn).toBeVisible();
                await confirmCashBtn.click();
            }
        });

        // ---------------------------------------------------------------------
        // Bước 5.5: Gia hạn cửa sổ ra cổng (Exit Window Extension)
        // ---------------------------------------------------------------------
        await test.step("5.5 Bấm gia hạn 15 phút do kẹt cổng: lần một được duyệt, lần hai bị từ chối", async () => {
            const extendBtn = page.locator(
                "button:has-text('Gia hạn'), button:has-text('Extend')",
            );
            await expect(extendBtn).toBeVisible();
            // Lần 1: gia hạn thành công thêm 15 phút
            await extendBtn.click();
            const toast = page.locator(".toast, [role='status']");
            await expect(toast.first()).toBeVisible();

            // Lần 2: hệ thống từ chối theo quy định (không gia hạn liên tiếp quá số lần cho phép)
            await expect(extendBtn).toBeVisible();
            // Thành phần tùy chọn hành vi UI: Sau khi gia hạn lần 1, giao diện có thể vô hiệu hóa nút (disabled) hoặc cho bấm tiếp và hiển thị thông báo lỗi từ server
            if (await extendBtn.isEnabled()) {
                await extendBtn.click();
                const alertMsg = await page.textContent("body");
                expect(alertMsg).toMatch(
                    /cannot_extend|không thể gia hạn|quá số lần/i,
                );
            }
        });

        // ---------------------------------------------------------------------
        // Bước 5.6: Cho xe ra bãi & mở barrier khi đủ điều kiện
        // ---------------------------------------------------------------------
        await test.step("5.6 Cho xe ra: hệ thống kiểm đủ 3 điều kiện (biển khớp + QR đúng + đã trả tiền), mở barrier", async () => {
            const authorizeBtn = page
                .locator(
                    "button.staff-btn-primary.staff-btn-lg, button:has-text('Cho xe qua barrier'), button:has-text('Authorize')",
                )
                .first();

            // Đảm bảo nút được kích hoạt khi đủ 3 điều kiện và không bỏ qua bước
            await expect(authorizeBtn).toBeEnabled();
            await authorizeBtn.click();

            // Barrier mở thành công
            const openAlert = page.locator(
                ".staff-barrier-open-alert, .barrier-icon",
            );
            await expect(openAlert.first()).toBeVisible();
        });

        // ---------------------------------------------------------------------
        // Bước 5.7: Mở biên lai điện tử quyết toán của phiên đỗ
        // ---------------------------------------------------------------------
        await test.step("5.7 Mở biên lai điện tử tại /customer/receipts/[sessionId]", async () => {
            await page.goto(`${BASE_URL}/customer/receipts/sess-8888-0001`);

            // Trang biên lai hiển thị giờ vào, giờ ra, tổng tiền
            const receiptText = await page.textContent("body");
            expect(receiptText).toMatch(
                /Biên lai|Receipt|51A-123\.45|30\.000/i,
            );
        });

        // ---------------------------------------------------------------------
        // Bước 5.8: Quy trình xử lý khách báo mất vé (Cấp lại QR phiên)
        // ---------------------------------------------------------------------
        await test.step("5.8 Khách báo mất vé: nhân viên tra cứu biển số và cấp lại mã QR phiên mới", async () => {
            await page.goto(`${BASE_URL}/staff/exit`);

            // Nhập biển số để tra cứu mất vé
            const exitPlateInput = page.locator("#staff-exit-plate");
            await exitPlateInput.fill("51A-123.45");

            const lookupBtn = page
                .locator(
                    "button:has-text('Tra cứu mất vé'), button:has-text('🔍')",
                )
                .first();
            await expect(lookupBtn).toBeVisible();
            await expect(lookupBtn).toBeEnabled();
            await lookupBtn.click();

            // Nút cấp lại vé QR xuất hiện
            const reissueBtn = page
                .locator(
                    "button:has-text('Cấp lại vé QR'), button:has-text('🔄')",
                )
                .first();
            await expect(reissueBtn).toBeVisible();
            await reissueBtn.click();
            const modal = page.locator(".staff-modal, [role='dialog']");
            await expect(modal).toBeVisible();
            const reasonInput = modal.locator("input, textarea").first();
            await expect(reasonInput).toBeVisible();
            await reasonInput.fill("Khách đánh rơi vé giấy tại sảnh");
            const submitReissue = modal
                .locator("button:has-text('Xác nhận cấp lại')")
                .first();
            await expect(submitReissue).toBeVisible();
            await submitReissue.click();
        });
    });
});
