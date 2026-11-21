import type { Metadata } from "next";
import { Suspense } from "react";
import { FeeLookupView } from "@/features/publicFee/FeeLookupView";

export const metadata: Metadata = {
    title: "Tra cứu phí gửi xe | ParkWise",
    description:
        "Tra cứu công khai phí đỗ xe theo mã QR vé gửi xe tạm tính và thời lượng đỗ xe.",
};

export default function FeeLookupPage() {
    return (
        <Suspense
            fallback={
                <div className="min-h-screen bg-slate-900 text-slate-100 flex items-center justify-center p-4">
                    <div className="text-center">
                        <div className="inline-block animate-spin rounded-full h-8 w-8 border-3 border-slate-700 border-t-blue-500 mb-3" />
                        <p className="text-sm text-slate-400">
                            Đang tải trang tra cứu…
                        </p>
                    </div>
                </div>
            }
        >
            <FeeLookupView />
        </Suspense>
    );
}
