import { render, screen } from "@testing-library/react";
import { useQueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";
import Home from "@/app/page";
import NotFound from "@/app/not-found";
import { Providers } from "@/app/providers";

function QueryProbe() {
    const queryClient = useQueryClient();
    return <span>{queryClient ? "Query client ready" : "Unavailable"}</span>;
}

describe("application shell", () => {
    it("shows only a technical home shell", () => {
        render(<Home />);
        expect(
            screen.getByRole("heading", { name: "Web foundation" }),
        ).toBeInTheDocument();
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
