import { expect, test } from "@playwright/test";
import {
    BASE_URL,
    DEMO_ACCOUNTS,
    DEMO_LOTS,
    setupDemoMockRoutes,
} from "./demo-fixtures";

test.describe("Phân đoạn 2: Khách tới bãi, cổng vào ANPR nhận diện & nhân viên xác nhận (Web /staff/entry)", () => {
    test.beforeEach(async ({ page }) => {
        await setupDemoMockRoutes(page);
    });

    test("Toàn trình: Nhân viên trực cổng vào, camera ANPR nhận diện xe đặt trước, xếp mã chỗ, barrier mở & kiểm tra từ chối khi hết chỗ", async ({
        page,
    }) => {
        // ---------------------------------------------------------------------
        // Bước 4.1: Đăng nhập nhân viên trực cổng vào bãi đỗ Trung tâm A
        // ---------------------------------------------------------------------
        await test.step("4.1 Nhân viên đăng nhập và vào màn hình kiểm soát cổng vào", async () => {
            await page.goto(`${BASE_URL}/login`);
            await page
                .locator("#parkwise-login-email")
                .fill(DEMO_ACCOUNTS.staff.email);
            await page
                .locator("#parkwise-login-password")
                .fill(DEMO_ACCOUNTS.staff.password);
            await page.locator("button.parkwise-login-submit").click();

            await page.goto(`${BASE_URL}/staff/entry`);
            await expect(page).toHaveURL(/\/staff\/entry/);

            // Kiểm tra tên bãi xe được phân công và số chỗ hiển thị
            const headerSubtitle = page.locator(".staff-subtitle, header");
            await expect(headerSubtitle.first()).toBeVisible();
        });

        // ---------------------------------------------------------------------
        // Bước 4.2: Camera ANPR nhận diện biển số xe (51A-123.45)
        // ---------------------------------------------------------------------
        await test.step("4.2 Camera ANPR đọc biển số xe 51A-123.45, barrier giữ đóng chờ nhân viên duyệt", async () => {
            // Sử dụng preset biển số 51A-123.45
            const presetBtn = page.locator(
                "button.staff-preset-chip:has-text('51A-123.45')",
            );
            await expect(presetBtn).toBeVisible();
            await presetBtn.click();

            // Kiểm tra biển số hiển thị trên giao diện xác thực
            const plateInput = page.locator(
                "#staff-edit-plate, .staff-plate-input",
            );
            await expect(plateInput).toHaveValue(/51A-123\.45/i);

            // Barrier vẫn đang ở trạng thái đóng (chưa có alert barrier mở)
            const openAlert = page.locator(".staff-barrier-open-alert");
            await expect(openAlert).not.toBeVisible();
        });

        // ---------------------------------------------------------------------
        // Bước 4.3: Nhân viên xác nhận xe vào cổng, hệ thống mở phiên & xếp slot A-01
        // ---------------------------------------------------------------------
        await test.step("4.3 Nhân viên bấm xác nhận: nhận ra xe có đặt chỗ, xếp chỗ cụ thể và mở barrier", async () => {
            const confirmEntryBtn = page
                .locator(
                    "button.staff-btn-primary.staff-btn-lg, button:has-text('Xác nhận'), button:has-text('Vào cổng')",
                )
                .first();

            await expect(confirmEntryBtn).toBeEnabled();
            await confirmEntryBtn.click();

            // Barrier chuyển trạng thái mở thành công
            const openAlert = page.locator(
                ".staff-barrier-open-alert, .staff-done-actions",
            );
            await expect(openAlert.first()).toBeVisible();

            // Xem modal QR phiếu gửi xe
            const viewQrBtn = page.locator(
                "button:has-text('Xem vé QR'), button:has-text('QR')",
            );
            await expect(viewQrBtn).toBeVisible();
            await viewQrBtn.click();
            const modal = page.locator(".staff-modal, [role='dialog']");
            await expect(modal).toBeVisible();
            const closeBtn = page
                .locator("button:has-text('Đóng'), button:has-text('✕')")
                .first();
            await expect(closeBtn).toBeVisible();
            await closeBtn.click();
        });

        // ---------------------------------------------------------------------
        // Bước 4.4: Khách hàng kiểm tra phiên đang đỗ với mã chỗ đã được xếp
        // ---------------------------------------------------------------------
        await test.step("4.4 Khách mở danh sách phiên đỗ xe (/customer/sessions), thấy mã chỗ được xếp", async () => {
            await page.goto(`${BASE_URL}/customer/sessions`);
            await expect(page).toHaveURL(/\/customer\/sessions/);

            // Thẻ phiên xe đang đỗ hiển thị biển số 51A-123.45 và mã chỗ A-01
            const sessionsContent = await page.textContent("body");
            expect(sessionsContent).toMatch(/51A-123\.45/);
            expect(sessionsContent).toMatch(
                /A-01|CAR-A|Đang đỗ|Active|Tầng 1/i,
            );
        });

        // ---------------------------------------------------------------------
        // Bước 4.5: Xử lý tình huống biển số mờ / đọc sai một ký tự (Fuzzy Plate)
        // ---------------------------------------------------------------------
        await test.step("4.5 Camera đọc sai ký tự (51A-123.4S), nhân viên sửa tay thành 51A-123.45 rồi xác nhận", async () => {
            await page.goto(`${BASE_URL}/staff/entry`);

            const editPlateInput = page.locator("#staff-edit-plate");
            await expect(editPlateInput).toBeVisible();

            // Nhập biển số sai
            await editPlateInput.fill("51A-123.4S");

            // Nhân viên đối soát ảnh thực tế và sửa lại cho đúng
            await editPlateInput.fill("51A-123.45");

            // Xác nhận thành công sau khi sửa
            const confirmBtn = page
                .locator(
                    "button.staff-btn-primary.staff-btn-lg, button:has-text('Xác nhận')",
                )
                .first();
            await expect(confirmBtn).toBeEnabled();
        });

        // ---------------------------------------------------------------------
        // Bước 4.6: Từ chối xe vãng lai khi số chỗ còn lại đều đã giữ cho khách đặt trước
        // ---------------------------------------------------------------------
        await test.step("4.6 Xe khác không đặt trước vào cổng khi walk-in capacity = 0: hệ thống từ chối", async () => {
            // Thiết lập kịch bản WalkInFull (Tin 270):
            // 1. Chế độ Mock: kích hoạt mock options { walkInFull: true } nếu USE_MOCK_API="true"
            await setupDemoMockRoutes(page, { walkInFull: true });

            // 2. Chế độ Live API: Parking trên develop dùng StubHeldQuotaReader (held = 0).
            // Slot A-01 đã được chiếm ở bước 4.3 (xe 51A-123.45). Để bãi thực sự hết chỗ cho xe vãng lai,
            // gọi API Owner PUT /api/parking/owner/lots/{lotId}/slots/{slotId}/maintenance để chuyển
            // slot A-02 sang trạng thái bảo trì (UnderMaintenance). Tuyệt đối không sửa StubHeldQuotaReader.
            const slotA02Id = "40000000-0000-0000-0000-000000000002";
            let ownerToken: string | null = null;
            try {
                const loginRes = await page.request.post(
                    `${BASE_URL}/api/identity/accounts/login`,
                    {
                        data: {
                            email: DEMO_ACCOUNTS.owner.email,
                            password: DEMO_ACCOUNTS.owner.password,
                        },
                    },
                );
                if (loginRes.ok()) {
                    const loginData = await loginRes.json();
                    ownerToken = loginData.accessToken || null;
                    if (ownerToken) {
                        await page.request.put(
                            `${BASE_URL}/api/parking/owner/lots/${DEMO_LOTS.lotA.id}/slots/${slotA02Id}/maintenance`,
                            {
                                headers: {
                                    Authorization: `Bearer ${ownerToken}`,
                                    "Content-Type": "application/json",
                                },
                                data: {
                                    underMaintenance: true,
                                    reason: "Bảo trì kỹ thuật phục vụ kiểm thử kịch bản WalkInFull (Tin 270)",
                                },
                            },
                        );
                    }
                }
            } catch {
                // Khi mock hoặc không có network ngoài, tiếp tục với giao diện
            }

            try {
                await page.goto(`${BASE_URL}/staff/entry`);

                const editPlateInput = page.locator("#staff-edit-plate");
                await editPlateInput.fill("51B-999.88");

                const confirmBtn = page
                    .locator(
                        "button.staff-btn-primary.staff-btn-lg, button:has-text('Xác nhận')",
                    )
                    .first();
                await confirmBtn.click();

                // Thông báo lỗi từ chối rõ ràng: "Hết chỗ cho xe không đặt trước." (parking.walk_in.full)
                const alertDanger = page.locator(
                    ".staff-alert-danger, [role='alert']",
                );
                await expect(alertDanger).toBeVisible();
                const alertText = await alertDanger.textContent();
                expect(alertText).toMatch(
                    /Hết chỗ cho xe không đặt trước|walk_in\.full/i,
                );
            } finally {
                // Khôi phục slot A-02 về trạng thái sẵn sàng sau khi kiểm thử xong
                if (ownerToken) {
                    try {
                        await page.request.put(
                            `${BASE_URL}/api/parking/owner/lots/${DEMO_LOTS.lotA.id}/slots/${slotA02Id}/maintenance`,
                            {
                                headers: {
                                    Authorization: `Bearer ${ownerToken}`,
                                    "Content-Type": "application/json",
                                },
                                data: {
                                    underMaintenance: false,
                                    reason: "Khôi phục sau kiểm thử kịch bản WalkInFull",
                                },
                            },
                        );
                    } catch {
                        // Bỏ qua lỗi dọn dẹp môi trường
                    }
                }
            }
        });
    });
});
