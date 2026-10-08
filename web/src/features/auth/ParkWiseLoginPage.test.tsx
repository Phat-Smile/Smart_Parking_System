import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";
import { ParkWiseLoginPage } from "./ParkWiseLoginPage";
import { loginParkWiseAccount } from "./loginParkWiseAccount";

vi.mock("./loginParkWiseAccount", () => ({ loginParkWiseAccount: vi.fn() }));
const loginMock = vi.mocked(loginParkWiseAccount);

vi.mock("./ParkWiseAuthLayout", () => ({
    ParkWiseAuthLayout: ({ children }: { children: ReactNode }) => (
        <div>{children}</div>
    ),
}));

describe("ParkWise Login form", () => {
    it("requires email and password before calling Identity", async () => {
        loginMock.mockResolvedValue({
            id: "account-id",
            email: "driver@example.com",
            phoneNumber: null,
            status: "EmailVerified",
            role: "Customer",
        });
        render(<ParkWiseLoginPage fontClassName="test-font" />);
        fireEvent.click(screen.getByRole("button", { name: "Log in" }));
        expect(
            await screen.findByText("Email is required"),
        ).toBeInTheDocument();
        expect(screen.getByText("Password is required")).toBeInTheDocument();
        expect(loginMock).not.toHaveBeenCalled();

        fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
            target: { value: "driver@example.com" },
        });
        fireEvent.change(screen.getByLabelText("Password"), {
            target: { value: "ValidPassword123!" },
        });
        fireEvent.click(screen.getByRole("button", { name: "Log in" }));
        await waitFor(() =>
            expect(screen.getByRole("status")).toHaveTextContent(
                "Identity accepted driver@example.com",
            ),
        );
        expect(loginMock).toHaveBeenCalledWith(
            "driver@example.com",
            "ValidPassword123!",
        );
        expect(screen.getByLabelText("Password")).toHaveValue("");
    });
});
