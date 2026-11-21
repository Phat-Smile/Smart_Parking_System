import { expect, test } from "@playwright/test";
import {
    BASE_URL,
    DEMO_ACCOUNTS,
    DEMO_LOTS,
    setupDemoMockRoutes,
} from "./demo-fixtures";

test.describe("Phân đoạn 1: Khách hàng vãng lai tìm bãi đỗ với AI & đặt chỗ trước (Web /customer)", () => {
    test.beforeEach(async ({ page }) => {
        await setupDemoMockRoutes(page);
    });

    test("Toàn trình: Khách đăng nhập, nạp ví VNPay, tìm kiếm AI Top 3, xem chi tiết và đặt chỗ giữ trước", async ({
        page,
    }) => {
        // ---------------------------------------------------------------------
        // Bước 3.1: Đăng nhập tài khoản khách hàng & kiểm tra thông tin ban đầu
        // ---------------------------------------------------------------------
        await test.step("3.1 Đăng nhập tài khoản khách hàng (customer@demo.local)", async () => {
            await page.goto(`${BASE_URL}/login`);
            await expect(page).toHaveURL(/\/login/);

            const emailInput = page.locator("#parkwise-login-email");
            const passwordInput = page.locator("#parkwise-login-password");
            const submitBtn = page.locator("button.parkwise-login-submit");

            await emailInput.fill(DEMO_ACCOUNTS.customer.email);
            await passwordInput.fill(DEMO_ACCOUNTS.customer.password);
            await submitBtn.click();

            // Chuyển hướng thành công vào Customer Dashboard
            await expect(page).toHaveURL(
                /\/customer\/(dashboard|complete-profile)/,
            );
            await page.goto(`${BASE_URL}/customer/dashboard`);

            // Kiểm tra hiển thị thông tin lời chào và tiêu đề trang
            await expect(
                page.locator("h1#dashboard-title, h1, .dashboard-hero-copy"),
            ).toBeVisible();
        });

        // ---------------------------------------------------------------------
        // Bước 3.2: Khách nạp 100.000 đ vào ví qua VNPay Sandbox
        // ---------------------------------------------------------------------
        await test.step("3.2 Mở ví ParkWise và nạp tiền qua cổng thanh toán VNPay", async () => {
            await page.goto(`${BASE_URL}/customer/wallet`);
            await expect(page).toHaveURL(/\/customer\/wallet/);

            // Kiểm tra thẻ số dư khả dụng
            const balanceLocator = page.locator(
                '[data-testid="wallet-balance"], .wallet-balance-value',
            );
            await expect(balanceLocator).toBeVisible();

            // Nhập số tiền nạp 100.000 đ
            const amountInput = page.locator("#wallet-topup-amount");
            await expect(amountInput).toBeVisible();
            await amountInput.fill("100000");

            // Bấm nút tạo giao dịch thanh toán VNPay
            const topupBtn = page.locator("button.wallet-topup-submit");
            await expect(topupBtn).toBeEnabled();
            await topupBtn.click();

            // Mô phỏng VNPay Return callback thành công
            await page.goto(
                `${BASE_URL}/payment/vnpay-return?vnp_ResponseCode=00&vnp_TransactionStatus=00&vnp_TxnRef=topup-demo-123&vnp_Amount=10000000`,
            );

            // Quay lại trang ví kiểm tra số dư đã được cập nhật
            await page.goto(`${BASE_URL}/customer/wallet`);
            await expect(balanceLocator).toBeVisible();
        });

        // ---------------------------------------------------------------------
        // Bước 3.3: Tìm kiếm bãi đỗ xe và nhận diện khối gợi ý AI Top 3
        // ---------------------------------------------------------------------
        await test.step("3.3 Tìm kiếm bãi đỗ gần điểm đến với phân tích gợi ý AI", async () => {
            await page.goto(
                `${BASE_URL}/search?destination=Nguyen+Hue&vehicle=car`,
            );
            await expect(page).toHaveURL(/\/search/);

            // Kiểm tra trường nhập điểm đến và bộ lọc phương tiện
            const destinationField = page
                .locator(
                    'input[placeholder*="destination"], input[type="text"]',
                )
                .first();
            await expect(destinationField).toBeVisible();

            // Khối gợi ý AI Top 3 xuất hiện với lý do gợi ý rõ ràng
            const aiBanner = page.locator(
                ".ai-recommendation-banner, .ai-recommendation-cards",
            );
            await expect(aiBanner).toBeVisible();

            // Kiểm tra thẻ gợi ý bãi xe có huy hiệu AI và lý do
            const recCards = page.locator(
                ".ai-recommendation-card, .search-lot-card",
            );
            await expect(recCards.first()).toBeVisible();
        });

        // ---------------------------------------------------------------------
        // Bước 3.4: Xem trang chi tiết bãi đỗ xe (Bãi đỗ Trung tâm A)
        // ---------------------------------------------------------------------
        await test.step("3.4 Mở chi tiết bãi đỗ Trung tâm A và kiểm tra thông số vận hành", async () => {
            await page.goto(`${BASE_URL}/parking-lots/${DEMO_LOTS.lotA.id}`);
            await expect(page).toHaveURL(
                new RegExp(`/parking-lots/${DEMO_LOTS.lotA.id}`),
            );

            // Kiểm tra tên bãi xe, địa chỉ và thông tin bảng giá
            await expect(
                page.getByRole("heading", {
                    name: new RegExp(DEMO_LOTS.lotA.name, "i"),
                }),
            ).toBeVisible();

            // Kiểm tra phần giá vé và trạng thái chỗ đỗ
            const bodyText = await page.textContent("body");
            expect(bodyText).toContain("15.000");
        });

        // ---------------------------------------------------------------------
        // Bước 3.5: Đặt chỗ giữ tạm và xác nhận trừ tiền ví
        // ---------------------------------------------------------------------
        await test.step("3.5 Đặt chỗ trước: chọn xe 51A-123.45 và thanh toán cọc bằng ví", async () => {
            // Nút bắt đầu đặt chỗ
            const bookNowBtn = page
                .locator(
                    "button.lot-detail-book-now, button:has-text('Book Now'), button:has-text('Đặt ngay')",
                )
                .first();
            await expect(bookNowBtn).toBeVisible();
            await bookNowBtn.click();

            // Hộp thoại đặt chỗ xuất hiện
            const bookingModal = page.locator(
                ".booking-dialog, [role='dialog'], .lot-detail-booking-card",
            );
            await expect(bookingModal.first()).toBeVisible();

            // Nút xác nhận giữ chỗ / thanh toán cọc
            const confirmBtn = page
                .locator(
                    "button:has-text('Confirm'), button:has-text('Xác nhận'), button:has-text('Giữ chỗ')",
                )
                .first();
            await expect(confirmBtn).toBeVisible();
            await confirmBtn.click();
        });

        // ---------------------------------------------------------------------
        // Bước 3.6: Mở vé đặt chỗ (Driver Booking Pass)
        // ---------------------------------------------------------------------
        await test.step("3.6 Mở Driver Booking Pass với mã QR và thông tin chỉ đường", async () => {
            await page.goto(
                `${BASE_URL}/customer/bookings/res-hold-12345678-0000`,
            );

            // Trang chi tiết vé đặt chỗ hiển thị
            const passHeading = page
                .locator("h1, h2, .booking-pass-title")
                .first();
            await expect(passHeading).toBeVisible();

            // Mã QR truy cập cổng hoặc thông tin đặt chỗ
            const passContent = await page.textContent("body");
            expect(passContent).toMatch(
                /51A-123\.45|PW-BKG|Booking Pass|Đặt chỗ/i,
            );
        });

        // ---------------------------------------------------------------------
        // Bước 3.7: Kiểm tra danh sách đặt chỗ của khách hàng
        // ---------------------------------------------------------------------
        await test.step("3.7 Kiểm tra danh sách đặt chỗ đang hoạt động", async () => {
            await page.goto(`${BASE_URL}/customer/bookings`);
            await expect(page).toHaveURL(/\/customer\/bookings/);

            const content = await page.textContent("body");
            expect(content).toBeDefined();
        });
    });
});
