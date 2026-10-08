import Image from "next/image";
import Link from "next/link";

/** src/Components/Nav/Footer.jsx, retaining the original columns and visual assets. */
export function ParkWiseFooter() {
    return (
        <footer className="parkwise-footer">
            <div className="parkwise-container">
                <div className="parkwise-footer-grid">
                    <div className="parkwise-footer-intro">
                        <Image
                            src="/images/parkwise/logo.png"
                            alt="ParkWise"
                            width={707}
                            height={353}
                        />
                        <p>Find Convenient and Affordable Parking.</p>
                    </div>
                    <div>
                        <h3>Explore</h3>
                        <div className="parkwise-footer-links">
                            <span>Blog</span>
                            <Link href="/#about">About Us</Link>
                            <span>Careers</span>
                            <span>Contact Us</span>
                        </div>
                    </div>
                    <div>
                        <h3>Link</h3>
                        <div className="parkwise-footer-links">
                            <span>Privacy Policy</span>
                            <span>Term &amp; Conditioner</span>
                            <Link href="/#how-it-works">FAQs</Link>
                        </div>
                    </div>
                    <div>
                        <h3>Link</h3>
                        <div
                            className="parkwise-socials"
                            aria-label="ParkWise social icons"
                        >
                            {[1, 2, 3, 4, 5].map((number) => (
                                <Image
                                    key={number}
                                    src={`/images/parkwise/ss (${number}).svg`}
                                    alt=""
                                    width={32}
                                    height={32}
                                />
                            ))}
                        </div>
                    </div>
                </div>
                <div className="parkwise-footer-bottom">
                    <p>© {new Date().getFullYear()} ParkWise</p>
                    <div>
                        <span>Privacy Policy</span>
                        <span>Term &amp; Conditioner</span>
                    </div>
                </div>
            </div>
        </footer>
    );
}
