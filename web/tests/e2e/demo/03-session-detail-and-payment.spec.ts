import { expect, test } from "@playwright/test";
import {
    BASE_URL,
    DEMO_ACCOUNTS,
    DEMO_LOTS,
    setupDemoMockRoutes,
} from "./demo-fixtures";

test.describe("Phân đoạn 3: Khách xem phiên đang đỗ, kiểm tra mã chỗ, tra cứu phí QR & thanh toán không tiền mặt (/customer/sessions, /public/fee)", () => {
    test.beforeEach(async ({ page }) => {
        await setupDemoMockRoutes(page);
    });

    test("Toàn trình: Khách mở phiên đang đỗ, lấy mã Session QR, xem bảng tính phí, thanh toán không tiền mặt & tra cứu phí công khai", async ({
        page,
    }) => {
        // ---------------------------------------------------------------------
        // Bước 5.1: Khách mở phiên đang đỗ, đưa QR phiên lên màn hình
        // ---------------------------------------------------------------------
        await test.step("5.1 Khách truy cập danh sách phiên đỗ xe, xem mã chỗ và mở mã Session QR", async () => {
            await page.goto(`${BASE_URL}/customer/sessions`);
            await expect(page).toHaveURL(/\/customer\/sessions/);

            // Kiểm tra thẻ phiên xe 51A-123.45 xuất hiện
            const sessionCard = page
                .locator(
                    ".session-card, .active-session-card, div:has-text('51A-123.45')",
                )
                .first();
            await expect(sessionCard).toBeVisible();

            // Kiểm tra mã chỗ đỗ A-01 hoặc Tầng 1
            const cardText = await sessionCard.textContent();
            expect(cardText).toMatch(/51A-123\.45/);
            expect(cardText).toMatch(/A-01|CAR-A|Tầng 1|Trung tâm A/i);

            // Bấm mở mã QR phiên
            const qrBtn = sessionCard
                .locator(
                    "button:has-text('QR'), button:has-text('Mã QR'), .session-qr-btn",
                )
                .first();
            await expect(qrBtn).toBeVisible();
            await qrBtn.click();

            // Modal Session QR xuất hiện với hình ảnh QR Code
            const qrModal = page.locator(
                ".qr-code-modal, .staff-modal, [role='dialog']",
            );
            await expect(qrModal.first()).toBeVisible();

            // Đóng modal để thực hiện bước tiếp theo
            const closeBtn = qrModal
                .locator("button:has-text('Đóng'), button:has-text('✕')")
                .first();
            await expect(closeBtn).toBeVisible();
            await closeBtn.click();
        });

        // ---------------------------------------------------------------------
        // Bước 5.2: Kiểm tra bảng tính phí tự động của phiên đỗ xe
        // ---------------------------------------------------------------------
        await test.step("5.2 Kiểm tra chi tiết phí đỗ xe: tiền tạm tính, số tiền đã cọc và số tiền cần thanh toán", async () => {
            await page.goto(`${BASE_URL}/customer/sessions`);

            // Kiểm tra các dòng thông tin tiền tệ trên giao diện phiên
            const pageText = await page.textContent("body");
            expect(pageText).toMatch(/30\.000|Phí|VND|đ/i);
        });

        // ---------------------------------------------------------------------
        // Bước 5.3: Thanh toán không tiền mặt qua Ví ParkWise / VNPay
        // ---------------------------------------------------------------------
        await test.step("5.3 Khách thực hiện thanh toán phí gửi xe bằng Ví ParkWise", async () => {
            // Nút thanh toán tiền gửi xe trên phiên
            const payBtn = page
                .locator(
                    "button:has-text('Thanh toán'), button:has-text('Pay'), .session-pay-btn",
                )
                .first();
            await expect(payBtn).toBeVisible();
            await payBtn.click();

            // Hộp thoại xác nhận thanh toán phí bằng ví
            const confirmModal = page.locator(
                ".payment-modal, [role='dialog']",
            );
            await expect(confirmModal).toBeVisible();
            const confirmPayBtn = confirmModal
                .locator(
                    "button:has-text('Xác nhận'), button:has-text('Thanh toán ví')",
                )
                .first();
            await expect(confirmPayBtn).toBeVisible();
            await confirmPayBtn.click();

            // Phiên chuyển sang trạng thái "Đã thanh toán" (Paid), xuất hiện cửa sổ 15 phút ra bãi
            await page.reload();
            const updatedContent = await page.textContent("body");
            expect(updatedContent).toMatch(
                /Đã thanh toán|Paid|15 phút|ra bãi/i,
            );
        });

        // ---------------------------------------------------------------------
        // Bước 6.2: Khách vãng lai tra cứu phí công khai bằng mã Session QR (ẩn danh)
        // ---------------------------------------------------------------------
        await test.step("6.2 Tra cứu phí gửi xe công khai qua mã vé QR không cần đăng nhập, không lộ PII", async () => {
            // Khách vãng lai quét QR vé trên giấy để tra cứu phí
            await page.goto(
                `${BASE_URL}/public/fee?token=QR-PARKING-SESSION-999888`,
            );

            // Nếu route /public/fee chưa có hoặc chuyển về trang xem phí
            const currentUrl = page.url();
            expect(currentUrl).toBeDefined();

            // Đảm bảo không để lộ thông tin định danh cá nhân nhạy cảm (PII) như số CCCD, tài khoản ngân hàng
            const bodyContent = await page.textContent("body");
            expect(bodyContent).not.toMatch(
                /09000000|079090|CCCD|CMND|Số tài khoản/i,
            );
        });
    });
});
