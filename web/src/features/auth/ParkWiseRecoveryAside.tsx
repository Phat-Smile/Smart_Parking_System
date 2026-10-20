import Image from "next/image";

export function ParkWiseRecoveryAside() {
    return (
        <aside className="parkwise-login-aside parkwise-forgot-aside">
            <div className="parkwise-welcome">
                <h1>Forgot Your Password?</h1>
                <p className="parkwise-forgot-aside-description">
                    No worries. Enter your email and we&apos;ll help you get
                    back to your ParkWise account.
                </p>
            </div>
            <Image
                src="/images/parkwise/login-parking-illustration.png"
                alt="Car beside a parking availability map"
                width={1536}
                height={1024}
                sizes="(max-width: 767px) 90vw, 480px"
                loading="eager"
                className="parkwise-login-illustration"
            />
        </aside>
    );
}
