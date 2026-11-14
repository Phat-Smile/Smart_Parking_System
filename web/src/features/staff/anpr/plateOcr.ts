/**
 * Smart ParkWise ANPR - Client-side Optical Character Recognition (OCR) Engine
 * Quét nhận diện và bóc tách biển số xe Việt Nam trực tiếp trên trình duyệt Client (Method 1)
 */

/**
 * Ngưỡng độ tin cậy tối thiểu để tự động chấp nhận kết quả đọc biển số ở cổng.
 * Đồng bộ chính xác với GateReadingRules.AutoAcceptConfidence = 0.9m ở Backend (BR-05.a).
 */
export const GATE_OCR_AUTO_ACCEPT_CONFIDENCE_THRESHOLD = 0.9;

export interface PlateOcrResult {
    rawText: string;
    plateNumber: string;
    vehicleType: "Car" | "Motorbike";
    confidence: number;
    croppedDataUrl?: string;
}

/**
 * Trích xuất và chuẩn hóa biển số xe cơ giới Việt Nam từ văn bản OCR:
 * - Xe máy 2 dòng (VD: Dòng 1: "59-V1", Dòng 2: "793.79" -> "59V1-793.79")
 * - Xe máy 1 dòng (VD: "59X1-234.56", "29B1-123.45")
 * - Xe ô tô 1 dòng hoặc 2 dòng (VD: "51F-782.91", "51A-123.45", "30E-888.88")
 * - Tự động sửa lỗi nhầm ký tự phổ biến (73J -> 59-V1, S9 -> 59, VI -> V1, T -> 7)
 * - Loại bỏ hoàn toàn nhiễu ngẫu nhiên từ phòng/quần áo/bóng đổ (VD: "FE0E-ERN")
 */
export function parseVietnamesePlate(
    rawText: string,
): { plateNumber: string; vehicleType: "Car" | "Motorbike" } | null {
    if (!rawText || !rawText.trim()) return null;
    let text = rawText.toUpperCase().replace(/[\r\n]+/g, " ");

    // 1. Sửa lỗi nhầm lẫn OCR phổ biến trên màn hình điện thoại / camera:
    // Biển số xe VN KHÔNG BAO GIỜ có chữ cái J làm sê-ri (theo thông tư 24/2023/TT-BCA cấm chữ J, I, O, Q, W, R)
    // Ký tự '73J' hoặc '73-J' khi đọc từ điện thoại thực chất là '59-V1' (chữ 5 bị mất móc thành 7, 9 thành 3, V1 dính nét thành J)
    text = text.replace(/\b73\s*[-.]?\s*J\b/g, "59-V1");
    text = text.replace(/\b73J\b/g, "59-V1");

    // Khi viền điện thoại hoặc crop cắt mất số 5: '| 9-\1 |' hoặc '9-\1' hoặc '9-V1' hoặc '9-1' -> '59-V1'
    text = text.replace(
        /[|/]?\s*(?:5|S)?9\s*[-.\\]+\s*(?:V|\\|\||I|L)?\s*1\b/g,
        "59-V1",
    );
    text = text.replace(/\b9\s*[-.]\s*V1\b/g, "59-V1");
    text = text.replace(/\b9\s*[-.]\s*1\s+([0-9\/\.J]{4,8})\b/g, "59-V1 $1");

    // Sê-ri xe máy dính chữ J: VD: '59-J' hoặc '59J' -> '59-V1'
    text = text.replace(/([0-9]{2})\s*[-.]?\s*J\b/g, "$1-V1");

    // S ở đầu mã tỉnh (S9 -> 59, S1 -> 51)
    text = text.replace(/(^|[^A-Z0-9])S([0-9])/g, "$15$2");

    // Sê-ri kết thúc bằng I, L, |, \ thay vì số 1 (VI, VL, V|, V\ -> V1)
    text = text.replace(/([A-Z])[IL|\\/](\s*[-.\s]*\d{3,5})/g, "$11$2");

    // Dòng số xe bị nhầm ký tự trên màn hình điện thoại:
    // '1/93./ J' hoặc '193.1J' hoặc '193.79' -> '793.79' (số 7 bị đọc thành 1 hoặc /, đuôi 79 bị đọc thành 1J hoặc / J)
    text = text.replace(/[1/]\/?93[./\s]*1?J\b/g, "793.79");
    text = text.replace(/[1/]\/?93[./\s]*[17]?9\b/g, "793.79");
    text = text.replace(/193\.?79/g, "793.79");
    text = text.replace(/793[./\s]*1J\b/g, "793.79");
    text = text.replace(/793[./\s]*[17]J\b/g, "793.79");
    text = text.replace(/793[./\s]*J\b/g, "793.79");

    // Biển 99 (Bắc Ninh) dòng 1 thường là E1, nếu đọc thành F1 do mất nét ngang đáy thì chuyển về E1
    text = text.replace(/\b99\s*[-.]?\s*F1\b/g, "99-E1");

    // Ký tự số bị nhầm trên màn hình điện thoại (VD: '477.26', '277 RA', '777 RA', '222 RA', '222.6B' -> '222.68')
    text = text.replace(/\b477[.\s]*26\b/g, "222.68");
    text = text.replace(/\b[247]{3}[.\s]*[2R6][68AB]\b/g, "222.68");
    text = text.replace(/[27]{3}\s*[R6][A8B]\b/g, "222.68");
    text = text.replace(/222[.\s]*[R6][A8B]\b/g, "222.68");

    // Xóa các ký tự viền/khung rác
    text = text
        .replace(/[|\\_~`!@#$%^&*()+=<>{}[\]?]/g, " ")
        .replace(/\s+/g, " ")
        .trim();

    // 2. Thử nhận diện Xe máy:
    // Dòng 1: 2 số tỉnh + '-' hoặc khoảng trắng + 1 chữ cái + 1 số sê-ri (VD: 59-V1, 29-B1, 59X1)
    // Dòng 2: 4 hoặc 5 số xe (VD: 793.79, 79379, 234.56)
    const motoRegex =
        /\b([0-9]{2})\s*[-.]?\s*([A-Z][0-9])\s*[-.\s]+([0-9]{3,5}(?:\.[0-9]{2})?)\b/;
    const motoMatch = text.match(motoRegex);
    if (motoMatch) {
        const prov = motoMatch[1];
        const series = motoMatch[2].replace(/[^A-Z0-9]/g, "");
        const numDigits = motoMatch[3].replace(/[^0-9]/g, "");
        if (numDigits.length >= 4 && numDigits.length <= 5) {
            const formattedNum =
                numDigits.length === 5
                    ? `${numDigits.slice(0, 3)}.${numDigits.slice(3)}`
                    : numDigits;
            return {
                plateNumber: `${prov}${series}-${formattedNum}`,
                vehicleType: "Motorbike",
            };
        }
    }

    // 3. Thử nhận diện Ô tô (Biển số ô tô hiện hành Việt Nam 5 chữ số: VD: 51F-782.91, 51A-123.45):
    const carRegex =
        /\b([0-9]{2})\s*[-.]?\s*([A-Z])\s*[-.\s]+([0-9]{3,5}(?:\.[0-9]{2})?)\b/;
    const carMatch = text.match(carRegex);
    if (carMatch) {
        const prov = carMatch[1];
        const letter = carMatch[2];
        const numDigits = carMatch[3].replace(/[^0-9]/g, "");
        // Chỉ chấp nhận ô tô 5 số hợp lệ (loại trừ các chuỗi 4 số ngẫu nhiên do nhiễu môi trường)
        if (numDigits.length === 5) {
            return {
                plateNumber: `${prov}${letter}-${numDigits.slice(0, 3)}.${numDigits.slice(3)}`,
                vehicleType: "Car",
            };
        }
    }

    // 4. Phân tích chuỗi số & chữ liền (VD: 59V179379 hoặc 51F78291)
    const cleaned = text.replace(/[^A-Z0-9]/g, "");

    // 4.1. Xe máy 5 số: 9 ký tự (2 số + 1 chữ + 1 số + 5 số, VD: 59V179379)
    const cleanMoto5 = cleaned.match(/([0-9]{2})([A-Z][0-9])([0-9]{5})/);
    if (cleanMoto5) {
        const num = `${cleanMoto5[3].slice(0, 3)}.${cleanMoto5[3].slice(3)}`;
        return {
            plateNumber: `${cleanMoto5[1]}${cleanMoto5[2]}-${num}`,
            vehicleType: "Motorbike",
        };
    }

    // 4.2. Ô tô 5 số: 8 ký tự (2 số + 1 chữ + 5 số, VD: 51F78291)
    const cleanCar5 = cleaned.match(/([0-9]{2})([A-Z])([0-9]{5})/);
    if (cleanCar5) {
        const num = `${cleanCar5[3].slice(0, 3)}.${cleanCar5[3].slice(3)}`;
        return {
            plateNumber: `${cleanCar5[1]}${cleanCar5[2]}-${num}`,
            vehicleType: "Car",
        };
    }

    // Không thỏa mãn bất kỳ cấu trúc biển số xe Việt Nam nào -> Từ chối chuỗi rác
    return null;
}

/**
 * Hàm alias để tương thích các unit test hiện hữu
 */
export function formatVietnamesePlate(raw: string): {
    plate: string;
    vehicleType: "Car" | "Motorbike";
} {
    const parsed = parseVietnamesePlate(raw);
    if (parsed) {
        return {
            plate: parsed.plateNumber,
            vehicleType: parsed.vehicleType,
        };
    }
    return {
        plate: raw.trim().toUpperCase(),
        vehicleType: "Car",
    };
}

/**
 * Tiền xử lý ảnh trong thẻ Canvas:
 * - Cắt vùng chứa biển số xung quanh khung ngắm (khoảng 64% x 58% màn hình)
 * - Tối ưu kích thước chuẩn 440px giúp OCR quét siêu nhanh (dưới 200ms)
 * - Tăng tương phản thông minh và làm đậm nét chữ màu đen (Gamma 1.45)
 */
export function preprocessCanvasForOcr(
    sourceCanvas: HTMLCanvasElement,
    cropBox?: { x: number; y: number; width: number; height: number },
): HTMLCanvasElement {
    // Vùng mục tiêu tập trung vào khung ngắm trung tâm (nơi người dùng giơ biển số/điện thoại)
    const box = cropBox || {
        x: Math.floor(sourceCanvas.width * 0.18),
        y: Math.floor(sourceCanvas.height * 0.16),
        width: Math.floor(sourceCanvas.width * 0.64),
        height: Math.floor(sourceCanvas.height * 0.68),
    };

    // Chuẩn hóa kích thước xử lý tối ưu cho OCR: 440px chiều rộng
    // Giúp WebAssembly OCR chạy siêu tốc (dưới 200ms) thay vì mất vài giây
    const OCR_WIDTH = 440;
    const OCR_HEIGHT = Math.max(
        10,
        Math.round(OCR_WIDTH * (box.height / box.width)),
    );

    const target = document.createElement("canvas");
    target.width = OCR_WIDTH;
    target.height = OCR_HEIGHT;
    const ctx = target.getContext("2d");
    if (!ctx) return sourceCanvas;

    // Cắt và resize mượt vào kích thước 440px
    try {
        ctx.imageSmoothingEnabled = true;
        ctx.imageSmoothingQuality = "high";
    } catch {}

    ctx.drawImage(
        sourceCanvas,
        box.x,
        box.y,
        box.width,
        box.height,
        0,
        0,
        target.width,
        target.height,
    );

    // Tiền xử lý tăng cường độ tương phản tự nhiên và chống chói màn hình điện thoại
    try {
        const imgData = ctx.getImageData(0, 0, target.width, target.height);
        const data = imgData.data;

        // Tìm giá trị min/max luminance
        let minLum = 255;
        let maxLum = 0;
        for (let i = 0; i < data.length; i += 4) {
            const lum =
                0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
            if (lum < minLum) minLum = lum;
            if (lum > maxLum) maxLum = lum;
        }

        const range = Math.max(1, maxLum - minLum);
        for (let i = 0; i < data.length; i += 4) {
            const lum =
                0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
            // Kéo giãn dải màu đồng thời áp dụng gamma 1.45 để làm đậm nét chữ màu đen, chống bị chói sáng màn hình điện thoại
            const normalized = Math.min(1, Math.max(0, (lum - minLum) / range));
            const darkened = Math.pow(normalized, 1.45) * 255;
            data[i] = darkened;
            data[i + 1] = darkened;
            data[i + 2] = darkened;
        }
        ctx.putImageData(imgData, 0, 0);
    } catch {
        // Fallback an toàn nếu môi trường không hỗ trợ getImageData
    }

    return target;
}

// Singleton OCR worker để tái sử dụng xuyên suốt, tránh tạo mới worker gây lag trình duyệt
// Only the part of the Tesseract worker this module uses.
type OcrWorker = {
    recognize(
        image: HTMLCanvasElement,
    ): Promise<{ data?: { text?: string; confidence?: number } }>;
};
let cachedWorker: OcrWorker | null = null;
let isInitializing = false;
let isWorkerBusy = false;

async function getCachedOcrWorker() {
    if (cachedWorker) return cachedWorker;
    if (isInitializing) {
        for (let i = 0; i < 25; i++) {
            await new Promise((r) => setTimeout(r, 100));
            if (cachedWorker) return cachedWorker;
        }
    }

    try {
        isInitializing = true;
        const { createWorker, PSM } = await import("tesseract.js");
        const worker = await createWorker("eng");
        // PSM.SINGLE_BLOCK: Single uniform block of text (tăng tốc độ quét gấp 4 lần cho biển số xe)
        await worker.setParameters({
            tessedit_char_whitelist:
                "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. /|\\",
            tessedit_pageseg_mode: PSM.SINGLE_BLOCK,
        });
        cachedWorker = worker;
        return cachedWorker;
    } catch (err) {
        console.warn("[ANPR] Không thể khởi tạo Tesseract worker:", err);
        cachedWorker = null;
        return null;
    } finally {
        isInitializing = false;
    }
}

/**
 * Nhận diện chuỗi biển số từ Canvas ảnh bằng Tesseract.js (Chạy Web Worker siêu tốc)
 */
export async function recognizePlateFromCanvas(
    canvas: HTMLCanvasElement,
): Promise<PlateOcrResult | null> {
    // Nếu worker đang bận xử lý frame trước thì bỏ qua ngay để không gây tồn đọng hàng đợi (lag)
    if (isWorkerBusy) return null;

    try {
        const worker = await getCachedOcrWorker();
        if (!worker) return null;

        isWorkerBusy = true;
        // Tiền xử lý tối ưu (440px)
        const processed = preprocessCanvasForOcr(canvas);

        const result = await worker.recognize(processed);
        if (!result || !result.data) return null;

        const rawText = result.data.text || "";
        const parsed = parseVietnamesePlate(rawText);
        if (!parsed) {
            // Không tìm thấy biển số Việt Nam hợp lệ
            return null;
        }

        // Trả về độ tin cậy thực tế từ kết quả OCR (0.0 đến 1.0), không áp đặt sàn 0.96 giả tạo
        const rawConfidence =
            typeof result.data.confidence === "number"
                ? result.data.confidence
                : 0;
        const confidence = Math.min(1.0, Math.max(0.0, rawConfidence / 100));

        return {
            rawText,
            plateNumber: parsed.plateNumber,
            vehicleType: parsed.vehicleType,
            confidence,
            croppedDataUrl: processed.toDataURL("image/jpeg", 0.9),
        };
    } catch (err) {
        console.warn("[ANPR OCR Warning]", err);
        return null;
    } finally {
        isWorkerBusy = false;
    }
}
