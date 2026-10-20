import { redirect } from 'next/navigation';

/**
 * Route /owner/layout → chuyển hướng về /owner/layout/3d.
 * Toàn bộ code cũ đã được tách sang:
 *   - /owner/layout/3d/page.tsx (Sơ đồ 3D)
 *   - /owner/layout/2d/page.tsx (Sơ đồ 2D)
 */
export default function LayoutIndexPage() {
  redirect('/owner/layout/3d');
}
