import { test, expect } from '@playwright/test';

test.describe('Owner Portal Smoke Tests', () => {
  test('Dashboard loads KPIs, revenue chart, donut, and floor bars', async ({ page }) => {
    await page.goto('/owner/dashboard');

    // Verify brand
    await expect(page.locator('.brand')).toContainText('Smart Parking System');

    // Verify 7 KPI cards are rendered
    const kpis = page.locator('.kpis .kp');
    await expect(kpis).toHaveCount(7);
    await expect(kpis.first()).toContainText('Doanh thu hôm nay');

    // Verify revenue section
    await expect(page.locator('#revb .big')).toBeVisible();
    await expect(page.locator('#revb svg')).toBeVisible();

    // Verify donut chart and floor bars
    await expect(page.locator('#dn')).toBeVisible();
    await expect(page.locator('#flr .fcol')).toHaveCount(5);
  });

  test('Parking Lots page and New Lot Wizard submission', async ({ page }) => {
    await page.goto('/owner/parking-lots');

    // Verify existing lots
    await expect(page.locator('text=Bãi đỗ xe Vincom Đồng Khởi')).toBeVisible();
    await expect(page.locator('text=+ Thêm bãi mới')).toBeVisible();

    // Navigate to wizard
    await page.click('text=+ Thêm bãi mới');
    await expect(page).toHaveURL(/.*\/parking-lots\/new/);

    // Step 1: Fill configuration
    await page.locator('label:has-text("Tên bãi") input').fill('Bãi xe Landmark 81');
    await page.locator('label:has-text("Địa chỉ") input').fill('720A Điện Biên Phủ, Bình Thạnh');
    await page.locator('label:has-text("Tọa độ GPS") input').fill('10.7950, 106.7218');

    // Click Next to Step 2
    await page.click('button:has-text("Tiếp tục")');

    // Step 2: 2D/3D Preview
    await expect(page.locator('.steps .on')).toContainText('2. Xem trước');
    await page.click('button:has-text("Tiếp tục")');

    // Step 3: Confirmation
    await expect(page.locator('.steps .on')).toContainText('3. Gửi duyệt');
    await page.click('button:has-text("Gửi duyệt")');

    // Modal confirmation
    await expect(page.locator('text=Xác nhận gửi duyệt?')).toBeVisible();
    await page.click('button:has-text("Xác nhận gửi")');

    // Success state
    await expect(page.locator('text=Đã gửi duyệt')).toBeVisible();
    await page.click('text=Về danh sách bãi');

    // Should return to parking lots list with new pending lot
    await expect(page).toHaveURL(/.*\/parking-lots/);
    await expect(page.locator('text=Bãi xe Landmark 81')).toBeVisible();
  });

  test('Reports date change and view mode toggling', async ({ page }) => {
    await page.goto('/owner/reports');

    // Default date is 2025-10-20
    const dateInput = page.locator('#dt');
    await expect(dateInput).toHaveValue('2025-10-20');

    // Period KPI cards
    await expect(page.locator('.kpis .kp span:has-text("Ngày 20/10/2025")')).toBeVisible();
    await expect(page.locator('.kpis .kp span:has-text("Tháng 10/2025")')).toBeVisible();
    await expect(page.locator('.kpis .kp span:has-text("Năm 2025")')).toBeVisible();

    // Switch to month view
    await page.click('.segs button:has-text("Tháng")');
    await expect(page.locator('.card.c8 svg')).toBeVisible();

    // Switch to year view
    await page.click('.segs button:has-text("Năm")');
    await expect(page.locator('.card.c8 svg')).toBeVisible();

    // Change date
    await dateInput.fill('2025-11-15');
    await expect(dateInput).toHaveValue('2025-11-15');
  });
});
