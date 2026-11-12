import { describe, expect, it } from "vitest";
import { ApiError } from "@/shared/errors/api-error";
import { vehicleAddErrorMessage } from "./vehicleAddError";

describe("vehicle add error localization", () => {
    it("explains a photo validation error in English and Vietnamese", () => {
        const error = new ApiError(400, {
            errorCode: "request.validation_failed",
            validationErrors: { "Details.PhotoUrl": ["invalid"] },
        });
        expect(vehicleAddErrorMessage(error, "en")).toBe("Upload a clear vehicle photo, then try again.");
        expect(vehicleAddErrorMessage(error, "vi")).toBe("Vui lòng tải ảnh xe lên rồi thử lại.");
    });

    it("explains an invalid plate according to the selected language", () => {
        const error = new ApiError(400, { errorCode: "identity.invalid_license_plate" });
        expect(vehicleAddErrorMessage(error, "en")).toContain("valid Vietnamese civilian plate");
        expect(vehicleAddErrorMessage(error, "vi")).toContain("biển số dân sự Việt Nam");
    });

    it("localizes unknown validation fields and unknown failures", () => {
        const validationError = new ApiError(400, {
            errorCode: "request.validation_failed",
            validationErrors: { "Details.Brand": ["invalid"] },
        });
        expect(vehicleAddErrorMessage(validationError, "en")).toContain("Some vehicle details are missing or invalid");
        expect(vehicleAddErrorMessage(validationError, "vi")).toContain("Một số thông tin xe đang thiếu hoặc chưa hợp lệ");
        expect(vehicleAddErrorMessage(new Error("offline"), "en")).toBe("Could not add this vehicle. Check its details and try again.");
        expect(vehicleAddErrorMessage(new Error("offline"), "vi")).toBe("Không thể thêm xe. Vui lòng kiểm tra thông tin và thử lại.");
    });
});
