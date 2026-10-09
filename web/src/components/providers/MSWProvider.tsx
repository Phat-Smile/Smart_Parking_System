"use client";

import { useEffect, useState } from "react";

let mswStarted = false;

export function MSWProvider({ children }: { children: React.ReactNode }) {
  const [mswReady, setMswReady] = useState(false);

  useEffect(() => {
    if (process.env.NEXT_PUBLIC_USE_MOCKS === "true") {
      const init = async () => {
        if (mswStarted) {
          setMswReady(true);
          return;
        }
        mswStarted = true;
        
        try {
          const { worker } = await import("../../mocks/browser");
          await worker.start({
            onUnhandledRequest: "bypass",
          });
        } catch (error) {
          // Ignore if it's already running
        }
        setMswReady(true);
      };

      init();
    } else {
      setMswReady(true);
    }
  }, []);

  if (!mswReady) return null;

  return <>{children}</>;
}
