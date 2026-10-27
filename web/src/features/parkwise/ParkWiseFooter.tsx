import Image from "next/image";
import Link from "next/link";

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
                    <div className="parkwise-footer-column">
                        <h3>Explore</h3>
                        <div className="parkwise-footer-links">
                            <Link href="/#about">About Us</Link>
                            <Link href="/#how-it-works">How It Works</Link>
                        </div>
                    </div>
                    <div className="parkwise-footer-column">
                        <h3>Support</h3>
                        <div className="parkwise-footer-links">
                            <span>Contact Us</span>
                            <span>FAQs</span>
                        </div>
                    </div>
                    <div className="parkwise-footer-column">
                        <h3>Legal</h3>
                        <div className="parkwise-footer-links">
                            <Link href="/privacy-policy">Privacy Policy</Link>
                            <Link href="/terms-and-conditions">
                                Terms &amp; Conditions
                            </Link>
                        </div>
                    </div>
                    <div className="parkwise-footer-column parkwise-footer-social-column">
                        <h3>Follow Us</h3>
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
                        <Link href="/privacy-policy">Privacy Policy</Link>
                        <Link href="/terms-and-conditions">
                            Terms &amp; Conditions
                        </Link>
                    </div>
                </div>
            </div>
        </footer>
    );
}
