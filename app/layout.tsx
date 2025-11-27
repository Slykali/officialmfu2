import type { Metadata } from "next";
import { Quintessential } from "next/font/google";
import "./globals.css";
import Footer from "@/components/footer";
import Header from "@/components/header";
import Image from "next/image";
import Link from "next/link";
import { GoogleAnalytics } from "@next/third-parties/google";

const font = Quintessential({
  subsets: ["latin"],
  weight: "400"
});

export const metadata: Metadata = {
  title: "Model Fictional Universe",
  description: "The castle whispers your name…",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body
        className={`${font.className} antialiased`}
      >
        <Header/>
        <div className="relative z-30">{children}</div>
        <Link href="/#faq" className="fixed bottom-5 right-5 z-50">
                  <Image src="/assets/figure.png" className="size-30 drop-shadow-2xl drop-shadow-indigo-500/80" alt="logo" width={100} height={100}/>

        </Link>
        <Footer/>
              <GoogleAnalytics gaId="G-7JT7T8CWSF" />
      </body>
    </html>
  );
}
