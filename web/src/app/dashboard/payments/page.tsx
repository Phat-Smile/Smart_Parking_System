import { redirect } from "next/navigation";

// The wallet lives in the customer area, next to the other signed-in customer pages.
export default function DashboardPaymentsRedirect() {
    redirect("/customer/wallet");
}
