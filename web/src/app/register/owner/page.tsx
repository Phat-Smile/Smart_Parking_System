import { redirect } from "next/navigation";

export default function RegisterOwnerRedirect() {
    redirect("/owner/register");
}
