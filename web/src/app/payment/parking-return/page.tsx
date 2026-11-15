import type { Metadata } from "next";
import { Suspense } from "react";
import { ParkingFeeReturnScreen } from "@/features/customer/parking-return/ParkingFeeReturnScreen";
import {
    CustomerFooter,
    CustomerHeader,
} from "@/features/customer/CustomerShell";
import "../../parkwise.css";
import "@/features/customer/customer-shell.css";

export const metadata: Metadata = {
    title: "Kết quả thanh toán phí đỗ xe | ParkWise",
    description:
        "Xác nhận và kiểm tra trạng thái thanh toán phí đỗ xe qua cổng VNPay.",
};

export default function ParkingReturnPage() {
    return (
        <div className="parkwise-page customer-shell-page min-h-screen flex flex-col justify-between">
            <CustomerHeader active="sessions" />
            <main className="flex-1 flex items-center justify-center py-12 px-4">
                <Suspense
                    fallback={
                        <div className="flex flex-col items-center justify-center p-8">
                            <div className="w-10 h-10 border-4 border-amber-500 border-t-transparent rounded-full animate-spin mb-4" />
                            <p className="text-slate-500 text-sm">
                                Đang tải kết quả giao dịch...
                            </p>
                        </div>
                    }
                >
                    <ParkingFeeReturnScreen />
                </Suspense>
            </main>
            <CustomerFooter />
        </div>
    );
}
