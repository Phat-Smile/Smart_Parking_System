import { ApiError } from "@/shared/errors/api-error";
import type { CustomerLanguage } from "./i18n/customerLanguage";

const messages = {
    plateAlreadyRegistered: [
        "This license plate is already registered in the system.",
        "Biển số này đã được đăng ký trong hệ thống.",
    ],
    invalidPlate: [
        "Enter a valid Vietnamese civilian plate for this vehicle type.",
        "Nhập biển số dân sự Việt Nam hợp lệ cho loại xe này.",
    ],
    photoRequired: [
        "Upload a clear vehicle photo, then try again.",
        "Vui lòng tải ảnh xe lên rồi thử lại.",
    ],
    bicyclePlate: [
        "A bicycle must not have a license plate.",
        "Xe đạp không sử dụng biển số.",
    ],
    vehicleType: [
        "Select a valid vehicle type.",
        "Vui lòng chọn loại xe hợp lệ.",
    ],
    account: [
        "Your account could not be found. Sign in again and retry.",
        "Không tìm thấy tài khoản. Vui lòng đăng nhập lại rồi thử lại.",
    ],
    invalidDetails: [
        "Some vehicle details are missing or invalid. Check the required fields and try again.",
        "Một số thông tin xe đang thiếu hoặc chưa hợp lệ. Hãy kiểm tra các mục bắt buộc rồi thử lại.",
    ],
    generic: [
        "Could not add this vehicle. Check its details and try again.",
        "Không thể thêm xe. Vui lòng kiểm tra thông tin và thử lại.",
    ],
} as const;

type MessageKey = keyof typeof messages;

export function vehicleAddErrorMessage(error: unknown, language: CustomerLanguage) {
    let key: MessageKey = "generic";
    if (error instanceof ApiError) {
        switch (error.problem?.errorCode) {
            case "identity.vehicle_plate_already_registered": key = "plateAlreadyRegistered"; break;
            case "identity.invalid_license_plate": key = "invalidPlate"; break;
            case "identity.bicycle_photo_required": key = "photoRequired"; break;
            case "identity.bicycle_plate_not_allowed": key = "bicyclePlate"; break;
            case "identity.invalid_vehicle_type": key = "vehicleType"; break;
            case "identity.account_not_found": key = "account"; break;
            case "request.validation_failed": {
                const fields = Object.keys(error.problem.validationErrors ?? {})
                    .map((field) => field.replace(/[^a-z]/gi, "").toLowerCase());
                if (fields.some((field) => field.endsWith("photourl"))) key = "photoRequired";
                else if (fields.some((field) => field.endsWith("licenseplate"))) key = "invalidPlate";
                else if (fields.some((field) => field.endsWith("vehicletype"))) key = "vehicleType";
                else key = "invalidDetails";
                break;
            }
        }
    }
    return messages[key][language === "vi" ? 1 : 0];
}
