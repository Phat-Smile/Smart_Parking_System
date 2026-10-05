import { render, screen } from "@testing-library/react";
import { useQueryClient } from "@tanstack/react-query";
import { describe, expect, it, vi } from "vitest";
import Home from "@/app/page";
import NotFound from "@/app/not-found";
import { Providers } from "@/app/providers";
import { redirect } from "next/navigation";

vi.mock("next/navigation", () => ({ redirect: vi.fn() }));

function QueryProbe() {
    const queryClient = useQueryClient();
    return <span>{queryClient ? "Query client ready" : "Unavailable"}</span>;
}

describe("application shell", () => {
    it("routes the home page to the existing Guest landing page", () => {
        Home();
        expect(redirect).toHaveBeenCalledWith("/guest");
    });

    it("shows a safe not-found page", () => {
        render(<NotFound />);
        expect(
            screen.getByRole("heading", { name: "Page not found" }),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("link", { name: "Return home" }),
        ).toHaveAttribute("href", "/");
    });

    it("provides a TanStack Query client", () => {
        render(
            <Providers>
                <QueryProbe />
            </Providers>,
        );
        expect(screen.getByText("Query client ready")).toBeInTheDocument();
    });
});
