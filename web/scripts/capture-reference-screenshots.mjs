import { chromium } from '@playwright/test';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

async function main() {
  const outDir = path.resolve(__dirname, '../reference-screenshots');
  if (!fs.existsSync(outDir)) {
    fs.mkdirSync(outDir, { recursive: true });
  }

  const protoPath = 'file:///' + path.resolve(__dirname, '../reference/smart-parking-prototype.html').replace(/\\/g, '/');
  console.log('Opening prototype at:', protoPath);

  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });

  await page.goto(protoPath, { waitUntil: 'networkidle' });
  await page.waitForTimeout(1000);

  // 1. Dashboard (Tổng quan)
  await page.screenshot({ path: path.join(outDir, '01-dashboard.png'), fullPage: true });
  console.log('Saved 01-dashboard.png');

  // 2. Parking Lots (Bãi của tôi)
  await page.click('button[data-go="lots"]');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '02-parking-lots.png'), fullPage: true });
  console.log('Saved 02-parking-lots.png');

  // 3. Wizard Step 1 (Đăng ký bãi mới - Cấu hình)
  await page.click('button[data-go="wiz"]');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '03-wiz-step1.png'), fullPage: true });
  console.log('Saved 03-wiz-step1.png');

  // Wizard Step 2 (Xem trước)
  // Fill valid data first
  await page.fill('input[data-b="wz:name"]', 'Bãi xe Thảo Cầm Viên');
  await page.fill('input[data-b="wz:addr"]', '2 Nguyễn Bỉnh Khiêm, Bến Nghé, Q.1');
  await page.fill('input[data-b="wz:gps"]', '10.7875, 106.7053');
  await page.click('button[data-wact="next"]');
  await page.waitForTimeout(1000);
  await page.screenshot({ path: path.join(outDir, '03-wiz-step2-3d.png'), fullPage: true });
  console.log('Saved 03-wiz-step2-3d.png');

  // Wizard Step 2 (2D preview)
  await page.click('button[data-pv="2d"]');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '03-wiz-step2-2d.png'), fullPage: true });
  console.log('Saved 03-wiz-step2-2d.png');

  // Wizard Step 3 (Gửi duyệt)
  await page.click('button[data-wact="next"]');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '03-wiz-step3-review.png'), fullPage: true });
  console.log('Saved 03-wiz-step3-review.png');

  // Wizard confirm
  await page.click('button[data-wact="next"]');
  await page.waitForTimeout(300);
  await page.screenshot({ path: path.join(outDir, '03-wiz-step3-confirm.png'), fullPage: true });
  console.log('Saved 03-wiz-step3-confirm.png');

  // 4. Detail Lot 1 (Vincom Đồng Khởi - approved) + tabs
  await page.click('button[data-go="lots"]');
  await page.waitForTimeout(500);
  await page.click('button[data-go="detail"][data-id="1"]');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '04-lot-detail-info.png'), fullPage: true });

  const tabs = [
    { key: 'zones', name: '04-lot-detail-zones.png' },
    { key: 'types', name: '04-lot-detail-types.png' },
    { key: 'price', name: '04-lot-detail-price.png' },
    { key: 'policy', name: '04-lot-detail-policy.png' },
    { key: 'appr', name: '04-lot-detail-appr.png' },
  ];
  for (const t of tabs) {
    await page.click(`button[data-tb="${t.key}"]`);
    await page.waitForTimeout(300);
    await page.screenshot({ path: path.join(outDir, t.name), fullPage: true });
    console.log(`Saved ${t.name}`);
  }

  // Also check rejected lot (id 3 - Pasteur)
  await page.click('button[data-go="lots"]');
  await page.waitForTimeout(300);
  await page.click('button[data-go="detail"][data-id="3"]');
  await page.waitForTimeout(300);
  await page.click('button[data-tb="appr"]');
  await page.waitForTimeout(300);
  await page.screenshot({ path: path.join(outDir, '04-lot-detail-rejected-appr.png'), fullPage: true });
  console.log('Saved 04-lot-detail-rejected-appr.png');

  // 5. Layout (Sơ đồ 2D / 3D)
  await page.click('button[data-go="map"]');
  await page.waitForTimeout(1500);
  await page.screenshot({ path: path.join(outDir, '05-layout-both.png'), fullPage: true });
  console.log('Saved 05-layout-both.png');

  await page.click('button[data-mv="3d"]');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '05-layout-3d.png'), fullPage: true });
  console.log('Saved 05-layout-3d.png');

  await page.click('button[data-mv="2d"]');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '05-layout-2d.png'), fullPage: true });
  console.log('Saved 05-layout-2d.png');

  // 6. Reservations (Đặt chỗ)
  await page.click('button[data-go="rsv"]');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '06-reservations.png'), fullPage: true });
  console.log('Saved 06-reservations.png');

  // 7. Sessions (Phiên đỗ xe)
  await page.click('button[data-go="ses"]');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '07-sessions.png'), fullPage: true });
  console.log('Saved 07-sessions.png');

  // 8. Reports (Báo cáo doanh thu)
  await page.click('button[data-go="rep"]');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '08-reports-day.png'), fullPage: true });
  console.log('Saved 08-reports-day.png');

  await page.click('button[data-mode="month"]');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '08-reports-month.png'), fullPage: true });
  console.log('Saved 08-reports-month.png');

  await page.click('button[data-mode="year"]');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(outDir, '08-reports-year.png'), fullPage: true });
  console.log('Saved 08-reports-year.png');

  await browser.close();
  console.log('All prototype screenshots captured successfully!');
}

main().catch(err => {
  console.error('Error:', err);
  process.exit(1);
});
