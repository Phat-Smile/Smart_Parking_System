import Image from "next/image";
import Link from "next/link";
import { ParkWiseLogo } from "./ParkWiseLogo";

export function ParkWiseFooter({
    language = "en",
}: {
    language?: "en" | "vi";
}) {
    const copy = (english: string, vietnamese: string) =>
        language === "vi" ? vietnamese : english;
    return (
        <footer className="parkwise-footer">
            <div className="parkwise-container">
                <div className="parkwise-footer-grid">
                    <div className="parkwise-footer-intro">
                        <ParkWiseLogo />
                        <p>
                            {copy(
                                "Find Convenient and Affordable Parking.",
                                "Tìm chỗ đỗ xe thuận tiện với giá hợp lý.",
                            )}
                        </p>
                    </div>
                    <div className="parkwise-footer-column">
                        <h3>{copy("Explore", "Khám phá")}</h3>
                        <div className="parkwise-footer-links">
                            <Link href="/#about">
                                {copy("About Us", "Về chúng tôi")}
                            </Link>
                            <Link href="/#how-it-works">
                                {copy("How It Works", "Cách hoạt động")}
                            </Link>
                        </div>
                    </div>
                    <div className="parkwise-footer-column">
                        <h3>{copy("Support", "Hỗ trợ")}</h3>
                        <div className="parkwise-footer-links">
                            <span>{copy("Contact Us", "Liên hệ")}</span>
                            <span>{copy("FAQs", "Câu hỏi thường gặp")}</span>
                        </div>
                    </div>
                    <div className="parkwise-footer-column">
                        <h3>{copy("Legal", "Pháp lý")}</h3>
                        <div className="parkwise-footer-links">
                            <Link href="/privacy-policy">
                                {copy("Privacy Policy", "Chính sách bảo mật")}
                            </Link>
                            <Link href="/terms-and-conditions">
                                {copy(
                                    "Terms & Conditions",
                                    "Điều khoản sử dụng",
                                )}
                            </Link>
                        </div>
                    </div>
                    <div className="parkwise-footer-column parkwise-footer-social-column">
                        <h3>{copy("Follow Us", "Theo dõi chúng tôi")}</h3>
                        <div className="parkwise-socials" aria-hidden="true">
                            {[1, 2, 3, 4, 5].map((number) => (
                                <Image
                                    key={number}
                                    src={`/images/parkwise/ss (${number}).svg`}
                                    alt=""
                                    width={40}
                                    height={40}
                                />
                            ))}
                        </div>
                    </div>
                </div>
                <div className="parkwise-footer-bottom">
                    <p>
                        © {new Date().getFullYear()} ParkWise | Developed for
                        educational purposes only.
                    </p>
                    <div>
                        <Link href="/privacy-policy">
                            {copy("Privacy Policy", "Chính sách bảo mật")}
                        </Link>
                        <Link href="/terms-and-conditions">
                            {copy("Terms & Conditions", "Điều khoản sử dụng")}
                        </Link>
                    </div>
                </div>
            </div>
        </footer>
    );
}
