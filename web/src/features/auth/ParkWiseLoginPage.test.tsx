import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";
import { ParkWiseLoginPage } from "./ParkWiseLoginPage";

vi.mock("./ParkWiseAuthLayout", () => ({
    ParkWiseAuthLayout: ({ children }: { children: ReactNode }) => (
        <div>{children}</div>
    ),
}));

describe("ParkWise Login form", () => {
    it("requires email and password before invoking the local submit handler", async () => {
        render(<ParkWiseLoginPage fontClassName="test-font" />);
        fireEvent.click(screen.getByRole("button", { name: "Log in" }));
        expect(
            await screen.findByText("Email is required"),
        ).toBeInTheDocument();
        expect(screen.getByText("Password is required")).toBeInTheDocument();

        fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
            target: { value: "driver@example.com" },
        });
        fireEvent.change(screen.getByLabelText("Password"), {
            target: { value: "ValidPassword123!" },
        });
        fireEvent.click(screen.getByRole("button", { name: "Log in" }));
        await waitFor(() =>
            expect(screen.getByRole("alert")).toHaveTextContent(
                "not connected",
            ),
        );
    });
});
