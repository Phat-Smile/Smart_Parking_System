import { describe, expect, it } from "vitest";
import {
    isVietnameseVoice,
    pickVoice,
    scoreVietnameseVoice,
    hasVietnameseVoice,
    getBestVietnameseVoice,
    type VoiceLike,
} from "./voiceSelection";

const v = (name: string, lang: string, localService = true): VoiceLike => ({
    name,
    lang,
    voiceURI: name,
    localService,
});

const enUS = v("Google US English", "en-US", false);
const viLocal = v("Vietnamese", "vi-VN");
const viNatural = v(
    "Microsoft HoaiMy Online (Natural) - Vietnamese",
    "vi-VN",
    false,
);
const viGoogle = v("Google Tiếng Việt", "vi-VN", false);

describe("isVietnameseVoice", () => {
    it("nhận vi-VN, vi_VN và vi", () => {
        expect(isVietnameseVoice(v("a", "vi-VN"))).toBe(true);
        expect(isVietnameseVoice(v("a", "vi_VN"))).toBe(true);
        expect(isVietnameseVoice(v("a", "vi"))).toBe(true);
    });

    it("nhận giọng có tên Vietnamese dù lang sai", () => {
        expect(isVietnameseVoice(v("Vietnamese Female", "en-US"))).toBe(true);
    });

    it("không nhận giọng Anh, Pháp hoặc lang bắt đầu bằng vi nhưng không phải Việt", () => {
        expect(isVietnameseVoice(enUS)).toBe(false);
        expect(isVietnameseVoice(v("Amelie", "fr-CA"))).toBe(false);
        expect(isVietnameseVoice(v("Viking", "vik-XX"))).toBe(false);
    });

    it("an toàn khi truyền null, undefined hoặc thiếu thuộc tính", () => {
        expect(isVietnameseVoice(null)).toBe(false);
        expect(isVietnameseVoice(undefined)).toBe(false);
        expect(isVietnameseVoice({ name: "", lang: "", voiceURI: "" })).toBe(
            false,
        );
    });
});

describe("pickVoice", () => {
    it("ưu tiên giọng thần kinh HoaiMy Online hơn giọng Google và giọng cài sẵn", () => {
        const pick = pickVoice([enUS, viLocal, viGoogle, viNatural], null);
        expect(pick.voice?.name).toBe(viNatural.name);
        expect(pick.hasVietnamese).toBe(true);
    });

    it("chỉ liệt kê giọng tiếng Việt khi máy có giọng Việt", () => {
        const pick = pickVoice([enUS, viLocal], null);
        expect(pick.options).toEqual([viLocal]);
    });

    it("giữ giọng Việt người dùng đã chọn", () => {
        const pick = pickVoice([viLocal, viNatural], viLocal.voiceURI);
        expect(pick.voice?.voiceURI).toBe(viLocal.voiceURI);
    });

    it("bỏ giọng đã lưu nếu đó là giọng tiếng Anh trong khi máy đã có giọng Việt", () => {
        const pick = pickVoice([enUS, viLocal], enUS.voiceURI);
        expect(pick.voice?.voiceURI).toBe(viLocal.voiceURI);
    });

    it("khi máy không có giọng Việt, không tự chọn giọng Anh làm mặc định", () => {
        const pick = pickVoice([enUS, v("Amelie", "fr-CA")], null);
        expect(pick.voice).toBeNull();
        expect(pick.hasVietnamese).toBe(false);
        expect(pick.options).toHaveLength(2);
    });

    it("khi máy không có giọng Việt, vẫn tôn trọng lựa chọn tay đã lưu", () => {
        const pick = pickVoice([enUS], enUS.voiceURI);
        expect(pick.voice?.voiceURI).toBe(enUS.voiceURI);
    });

    it("danh sách rỗng trả về không có giọng", () => {
        const pick = pickVoice([], null);
        expect(pick.voice).toBeNull();
        expect(pick.options).toEqual([]);
    });
});

describe("scoreVietnameseVoice", () => {
    it("giọng trực tuyến tự nhiên có điểm cao hơn giọng cài sẵn", () => {
        expect(scoreVietnameseVoice(viNatural)).toBeGreaterThan(
            scoreVietnameseVoice(viLocal),
        );
    });

    it("trả về 0 an toàn khi giọng là null hoặc undefined", () => {
        expect(scoreVietnameseVoice(null)).toBe(0);
        expect(scoreVietnameseVoice(undefined)).toBe(0);
    });
});

describe("hasVietnameseVoice & getBestVietnameseVoice", () => {
    it("hasVietnameseVoice phát hiện chuẩn xác giọng tiếng Việt trong danh sách", () => {
        expect(hasVietnameseVoice([enUS, viLocal])).toBe(true);
        expect(hasVietnameseVoice([enUS])).toBe(false);
        expect(hasVietnameseVoice([])).toBe(false);
        expect(hasVietnameseVoice(null)).toBe(false);
    });

    it("getBestVietnameseVoice chọn đúng giọng tiếng Việt tối ưu nhất", () => {
        const best = getBestVietnameseVoice([enUS, viLocal, viNatural]);
        expect(best?.name).toBe(viNatural.name);
        expect(getBestVietnameseVoice([enUS])).toBeNull();
        expect(getBestVietnameseVoice([])).toBeNull();
        expect(getBestVietnameseVoice(null)).toBeNull();
    });
});
