import { darkPatternURL, patternURL } from "@/lib/constant";
import Link from "next/link";

export default function Footer() {
  return (
    <div style={{
        backgroundImage: darkPatternURL
    }} className="footer sticky z-0 bottom-0 left-0 w-full h-80 flex justify-center items-center bg-[#161103]">
      <div className="relative overflow-hidden w-full h-full flex justify-end px-12 text-right items-start py-12 ">
        <div className="flex gap-x-16">
          <div className="grid grid-cols-1 gap-4 h-min">
            <Link
              href="/program"
              className="clickable hover:cursor-none hover:underline underline-offset-8 transition-all hover:text-amber-500 text-[#6B5B2A] text-2xl"
            >
              Event Program
            </Link>
            <Link
              href="/team"
              className="clickable hover:cursor-none hover:underline underline-offset-8 transition-all hover:text-amber-500 text-[#6B5B2A] text-2xl"
            >
              Our Team
            </Link>
            <Link
              href="/#letter"
              className="clickable hover:cursor-none hover:underline underline-offset-8 transition-all hover:text-amber-500 text-[#6B5B2A] text-2xl"
            >
              Letters
            </Link>
          </div>
          <div className="grid grid-cols-1 gap-4 h-min">
            <Link
              href="https://instagram.com/official_mots"
              target="_blank"
              className="clickable hover:cursor-none hover:underline underline-offset-8 transition-all hover:text-amber-500 text-[#6B5B2A] text-2xl"
            >
              Instagram
            </Link>
          </div>
        </div>
        <h2
          className="absolute bottom-0 left-0 p-12 sm:text-8xl 
          text-[80px]  font-calendas transition-all hover:translate-y-1/4 text-amber-500"
        >
          Model Fictional Universe
        </h2>
      </div>
    </div>
  );
}