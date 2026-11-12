import { afterEach, describe, expect, it, vi } from "vitest";
import { renderToString } from "react-dom/server";
import { ToastNotificationHub } from "./ToastNotificationHub";

afterEach(() => vi.unstubAllGlobals());

describe("ToastNotificationHub hydration", () => {
    it("renders the same initial HTML on the server and a browser with Push support", () => {
        vi.stubGlobal("window", undefined);
        const serverHtml = renderToString(<ToastNotificationHub />);
        vi.unstubAllGlobals();

        vi.stubGlobal("Notification", { permission: "default" });
        const browserHtml = renderToString(<ToastNotificationHub />);

        expect(browserHtml).toBe(serverHtml);
    });
});
