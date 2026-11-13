import "@testing-library/jest-dom/vitest";
import { cleanup, configure } from "@testing-library/react";
import { afterEach, vi } from "vitest";

// The shared agent runs the suites under load: the 1000 ms default of waitFor and findBy times out on slow renders.
configure({ asyncUtilTimeout: 5000 });

afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
    vi.restoreAllMocks();
});
