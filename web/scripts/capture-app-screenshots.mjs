import { chromium } from '@playwright/test';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

async function main() {
  const outDir = path.resolve(__dirname, '../app-screenshots');
  if (!fs.existsSync(outDir)) {
    fs.mkdirSync(outDir, { recursive: true });
  }

  const baseUrl = 'http://127.0.0.1:3000';
  console.log('Connecting to Next.js app at:', baseUrl);

  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });

  // 1. Dashboard
  await page.goto(`${baseUrl}/owner/dashboard`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(1000);
  await page.screenshot({ path: path.join(outDir, '01-dashboard.png'), fullPage: true });
  console.log('Saved 01-dashboard.png');

  // 2. Parking Lots
  await page.goto(`${baseUrl}/owner/parking-lots`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '02-parking-lots.png'), fullPage: true });
  console.log('Saved 02-parking-lots.png');

  // 3. Wizard Step 1
  await page.goto(`${baseUrl}/owner/parking-lots/new`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '03-wiz-step1.png'), fullPage: true });
  console.log('Saved 03-wiz-step1.png');

  // Fill data
  await page.locator('label:has-text("Tên bãi") input').fill('Bãi xe Thảo Cầm Viên');
  await page.locator('label:has-text("Địa chỉ") input').fill('2 Nguyễn Bỉnh Khiêm, Bến Nghé, Q.1');
  await page.locator('label:has-text("Tọa độ GPS") input').fill('10.7875, 106.7053');
  await page.click('button:has-text("Tiếp tục")');
  await page.waitForTimeout(1500);

  // Wizard Step 2 (3D)
  await page.screenshot({ path: path.join(outDir, '03-wiz-step2-3d.png'), fullPage: true });
  console.log('Saved 03-wiz-step2-3d.png');

  // Wizard Step 2 (2D)
  await page.click('.segs button:has-text("2D")');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '03-wiz-step2-2d.png'), fullPage: true });
  console.log('Saved 03-wiz-step2-2d.png');

  // Wizard Step 3 Review
  await page.click('button:has-text("Tiếp tục")');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '03-wiz-step3-review.png'), fullPage: true });
  console.log('Saved 03-wiz-step3-review.png');

  // Wizard Step 3 Confirm
  await page.click('button:has-text("Gửi duyệt")');
  await page.waitForTimeout(300);
  await page.screenshot({ path: path.join(outDir, '03-wiz-step3-confirm.png'), fullPage: true });
  console.log('Saved 03-wiz-step3-confirm.png');

  // 4. Lot 1 Detail
  await page.goto(`${baseUrl}/owner/parking-lots/1`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '04-lot-detail-info.png'), fullPage: true });
  console.log('Saved 04-lot-detail-info.png');

  const tabs = [
    { label: 'Khu vực', name: '04-lot-detail-zones.png' },
    { label: 'Loại xe & sức chứa', name: '04-lot-detail-types.png' },
    { label: 'Giá', name: '04-lot-detail-price.png' },
    { label: 'Đặt chỗ', name: '04-lot-detail-policy.png' },
    { label: 'Duyệt', name: '04-lot-detail-appr.png' },
  ];
  for (const t of tabs) {
    await page.click(`.segs button:has-text("${t.label}")`);
    await page.waitForTimeout(300);
    await page.screenshot({ path: path.join(outDir, t.name), fullPage: true });
    console.log(`Saved ${t.name}`);
  }

  // Lot 3 Detail (Rejected)
  await page.goto(`${baseUrl}/owner/parking-lots/3`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(500);
  await page.click('.segs button:has-text("Duyệt")');
  await page.waitForTimeout(300);
  await page.screenshot({ path: path.join(outDir, '04-lot-detail-rejected-appr.png'), fullPage: true });
  console.log('Saved 04-lot-detail-rejected-appr.png');

  // 5. Layout (Sơ đồ 2D / 3D)
  await page.goto(`${baseUrl}/owner/layout`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(1500);
  await page.screenshot({ path: path.join(outDir, '05-layout-both.png'), fullPage: true });
  console.log('Saved 05-layout-both.png');

  await page.click('.segs button:has-text("3D")');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '05-layout-3d.png'), fullPage: true });
  console.log('Saved 05-layout-3d.png');

  await page.click('.segs button:has-text("2D")');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '05-layout-2d.png'), fullPage: true });
  console.log('Saved 05-layout-2d.png');

  // 6. Reservations
  await page.goto(`${baseUrl}/owner/reservations`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '06-reservations.png'), fullPage: true });
  console.log('Saved 06-reservations.png');

  // 7. Sessions
  await page.goto(`${baseUrl}/owner/sessions`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '07-sessions.png'), fullPage: true });
  console.log('Saved 07-sessions.png');

  // 8. Reports
  await page.goto(`${baseUrl}/owner/reports`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '08-reports-day.png'), fullPage: true });
  console.log('Saved 08-reports-day.png');

  await page.click('.segs button:has-text("Tháng")');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '08-reports-month.png'), fullPage: true });
  console.log('Saved 08-reports-month.png');

  await page.click('.segs button:has-text("Năm")');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '08-reports-year.png'), fullPage: true });
  console.log('Saved 08-reports-year.png');

  await browser.close();
  console.log('All app screenshots captured successfully!');
}

main().catch(err => {
  console.error('Error:', err);
  process.exit(1);
});
