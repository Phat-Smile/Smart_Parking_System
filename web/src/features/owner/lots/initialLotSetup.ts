import {
    configureDepositPolicyApi,
    configureOpeningHoursApi,
    configurePricingApi,
    type OpeningHoursDayItem,
} from "@/features/owner/api/ownerApi";
import { saveOwnerParkingLayout } from "@/features/owner/layout/ownerLayoutApi";
import { buildInitialLayout } from "./initialLayout";

/*
 * Parking's register endpoint only takes the name, address, position, type and features. What the
 * owner also typed in the wizard (hours, prices, deposit, layout) is saved right after, through the same
 * calls the config screens use, so a new lot does not arrive empty.
 */

export type InitialLotSetup = {
    openAt: string;
    closeAt: string;
    acceptsCars: boolean;
    acceptsMotorbikes: boolean;
    carPricePerHour: number;
    motorbikePricePerHour: number;
    depositVnd: number;
    capacity: number;
    floors: number;
    rowsPerFloor: number;
};

const TIME = /^([01]\d|2[0-3]):[0-5]\d$/;

/** Same hours for all seven days; equal or missing times mean open all day. */
export function weeklyHours(openAt: string, closeAt: string) {
    const allDay =
        !TIME.test(openAt) || !TIME.test(closeAt) || openAt === closeAt;
    const days: OpeningHoursDayItem[] = Array.from({ length: 7 }, (_, day) =>
        allDay
            ? { dayOfWeek: day, isClosed: false, is24Hours: true }
            : {
                  dayOfWeek: day,
                  isClosed: false,
                  is24Hours: false,
                  opensAt: openAt,
                  closesAt: closeAt,
              },
    );
    return { days };
}

/** Saves what the wizard collected. Returns the names of the parts that failed; empty means all saved. */
export async function saveInitialLotSetup(
    lotId: string,
    setup: InitialLotSetup,
): Promise<string[]> {
    const steps: { label: string; run: () => Promise<unknown> }[] = [
        {
            label: "sơ đồ ô đỗ",
            run: () =>
                saveOwnerParkingLayout(lotId, {
                    expectedVersion: 0,
                    floors: buildInitialLayout({
                        capacity: setup.capacity,
                        floors: setup.floors,
                        rowsPerFloor: setup.rowsPerFloor,
                        acceptsCars: setup.acceptsCars,
                        acceptsMotorbikes: setup.acceptsMotorbikes,
                    }),
                }),
        },
        {
            label: "giờ hoạt động",
            run: () =>
                configureOpeningHoursApi(
                    lotId,
                    weeklyHours(setup.openAt, setup.closeAt),
                ),
        },
    ];
    if (setup.acceptsCars && setup.carPricePerHour > 0) {
        steps.push({
            label: "giá ô tô",
            run: () =>
                configurePricingApi(lotId, "CAR", {
                    billingMode: "Block",
                    blockMinutes: 60,
                    price: setup.carPricePerHour,
                }),
        });
    }
    if (setup.acceptsMotorbikes && setup.motorbikePricePerHour > 0) {
        steps.push({
            label: "giá xe máy",
            run: () =>
                configurePricingApi(lotId, "MOTORBIKE", {
                    billingMode: "Block",
                    blockMinutes: 60,
                    price: setup.motorbikePricePerHour,
                }),
        });
    }
    if (setup.depositVnd > 0) {
        steps.push({
            label: "tiền cọc giữ chỗ",
            run: () =>
                configureDepositPolicyApi(lotId, {
                    holdDepositVnd: Math.round(setup.depositVnd),
                }),
        });
    }
    const failed: string[] = [];
    for (const step of steps) {
        try {
            await step.run();
        } catch {
            failed.push(step.label);
        }
    }
    return failed;
}
