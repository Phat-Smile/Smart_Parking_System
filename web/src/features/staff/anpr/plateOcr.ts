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
 * Only canonicalizes *observed* plate characters. Never translates one observed
 * province/series/number into a different concrete plate: uncertain OCR must
 * be reviewed by staff rather than credited as a confident gate reading.
 *
 * A frame containing two different recognizable plates is ambiguous and is
 * deliberately rejected rather than selecting whichever regex matches first.
 */
export function parseVietnamesePlate(
    rawText: string,
): { plateNumber: string; vehicleType: "Car" | "Motorbike" } | null {
    if (!rawText || !rawText.trim()) return null;

    // Only presentation separators may be discarded. No character substitution
    // (e.g., 73J -> 59V1, 99F1 -> 99E1, 47726 -> 22268) is trustworthy.
    const text = rawText.toUpperCase().replace(/[\r\n]+/g, " ").trim();
    if (text.length > 512) return null;

    // Respect the series-letter constraint that the existing client documented.
    // A letter that cannot be validated is *not* inferred from its shape.
    const validSeries = /^[A-HK-NPS-VX-Z]$/;
    const candidates = new Map<string, "Car" | "Motorbike">();
    const add = (
        province: string,
        series: string,
        digits: string,
        vehicleType: "Car" | "Motorbike",
    ) => {
        if (!validSeries.test(series[0])) return;
        if (vehicleType === "Car" && digits.length !== 5) return;
        if (vehicleType === "Motorbike" && ![4, 5].includes(digits.length)) return;
        const number = digits.length === 5
            ? `${digits.slice(0, 3)}.${digits.slice(3)}`
            : digits;
        candidates.set(`${province}${series}-${number}`, vehicleType);
    };

    // Explicitly separated or two-line plates, including an OCR frame where
    // multiple vehicles are visible. Capture all candidates, not only the first.
    const motorbike = /\b([0-9]{2})\s*[-.]?\s*([A-Z][0-9])\s*[-.\s]+([0-9]{3}\.[0-9]{2}|[0-9]{4,5})\b/g;
    for (const match of text.matchAll(motorbike)) {
        add(match[1], match[2], match[3].replace(/\D/g, ""), "Motorbike");
    }
    const car = /\b([0-9]{2})\s*[-.]?\s*([A-Z])\s*[-.\s]+([0-9]{3}\.[0-9]{2}|[0-9]{5})\b/g;
    for (const match of text.matchAll(car)) {
        add(match[1], match[2], match[3].replace(/\D/g, ""), "Car");
    }

    // No-separator OCR output: only accept an entire token, not a substring of
    // an unrelated label or a concatenation of several ambiguous text pieces.
    const compact = text.replace(/[.\-\s]/g, "");
    let match = compact.match(/^([0-9]{2})([A-Z][0-9])([0-9]{5})$/);
    if (match) add(match[1], match[2], match[3], "Motorbike");
    match = compact.match(/^([0-9]{2})([A-Z])([0-9]{5})$/);
    if (match) add(match[1], match[2], match[3], "Car");

    if (candidates.size !== 1) return null;
    const [plateNumber, vehicleType] = candidates.entries().next().value!;
    return { plateNumber, vehicleType };
}

/** Safe alias: invalid OCR is not silently classified as a Car. */
export function formatVietnamesePlate(raw: string): {
    plate: string;
    vehicleType: "Car" | "Motorbike";
} | null {
    const parsed = parseVietnamesePlate(raw);
    return parsed
        ? { plate: parsed.plateNumber, vehicleType: parsed.vehicleType }
        : null;
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
        // ALPR is processed by C# Backend (<300ms). Client-side tesseract is kept as dynamic fallback only.
        const tesseractPkg = "tesseract.js";
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const tesseract: any = await import(/* webpackIgnore: true */ tesseractPkg).catch(() => null);
        if (!tesseract || !tesseract.createWorker) {
            cachedWorker = null;
            return null;
        }
        const { createWorker, PSM } = tesseract;
        const worker = await createWorker("eng");
        // PSM.SINGLE_BLOCK: Single uniform block of text (tăng tốc độ quét gấp 4 lần cho biển số xe)
        await worker.setParameters({
            tessedit_char_whitelist:
                "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. /|\\",
            tessedit_pageseg_mode: PSM.SINGLE_BLOCK,
        });
        cachedWorker = worker;
        return cachedWorker;
    } catch {
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
