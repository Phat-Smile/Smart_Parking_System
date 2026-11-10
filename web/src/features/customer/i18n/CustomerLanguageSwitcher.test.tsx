import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, it } from "vitest";
import { WalletPageIntro, VnpayReturnIntro } from "../wallet/WalletPageCopy";
import { CustomerLanguageSwitcher } from "./CustomerLanguageSwitcher";
import { setCustomerLanguage } from "./customerLanguage";

beforeEach(() => setCustomerLanguage("en"));
afterEach(() => setCustomerLanguage("en"));

it("switches Customer wallet copy and keeps the selected language after remount", async () => {
    const user = userEvent.setup();
    const { unmount } = render(
        <>
            <CustomerLanguageSwitcher />
            <WalletPageIntro />
            <VnpayReturnIntro />
        </>,
    );

    expect(screen.getAllByText("My Wallet")).toHaveLength(2);
    await user.click(screen.getByRole("button", { name: "Language: English" }));
    await user.click(screen.getByRole("option", { name: "Tiếng Việt" }));
    expect(
        screen.getByRole("heading", { name: "Ví của tôi" }),
    ).toBeInTheDocument();
    expect(
        screen.getByRole("heading", { name: "Kết quả nạp tiền" }),
    ).toBeInTheDocument();
    expect(localStorage.getItem("parkwise_customer_language")).toBe("vi");

    unmount();
    render(
        <>
            <CustomerLanguageSwitcher />
            <WalletPageIntro />
        </>,
    );
    await user.click(
        screen.getByRole("button", { name: "Ngôn ngữ: Tiếng Việt" }),
    );
    expect(screen.getByRole("option", { name: "Tiếng Việt" })).toHaveAttribute(
        "aria-selected",
        "true",
    );
    await user.click(screen.getByRole("option", { name: "English" }));
    expect(
        screen.getByRole("heading", { name: "My Wallet" }),
    ).toBeInTheDocument();
});
