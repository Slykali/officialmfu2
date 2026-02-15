import { darkPatternURL, patternURL } from "@/lib/constant";
import Image from "next/image";
import Link from "next/link";

export default function Footer() {
  return (
    <div
      style={{ backgroundImage: darkPatternURL }}
      className="footer sticky z-0 bottom-0 left-0 w-full min-h-64 sm:min-h-72 md:h-80 flex justify-center items-center bg-[#0f0b01]"
    >
      <div className="relative overflow-hidden w-full h-full flex justify-end px-4 sm:px-8 md:px-12 text-right items-start py-8 sm:py-12">
        <h2 className="absolute bottom-0 left-0 p-4 sm:p-8 md:p-12 text-xl sm:text-3xl md:text-4xl lg:text-5xl xl:text-6xl font-calendas transition-all text-amber-500 max-w-[90%]">
          Model Fictional Universe
        </h2>
      </div>
      <div className="absolute flex flex-col sm:flex-row gap-2 sm:gap-4 text-amber-200 text-sm sm:text-base md:text-lg lg:text-xl xl:text-2xl top-4 left-4 sm:top-8 sm:left-8 md:top-12 md:left-12">
        <Link href="mailto:officialmodelfu@gmail.com" className="break-all">officialmodelfu@gmail.com</Link>
        <Link href="https://instagram.com/official_mfu" target="_blank" rel="noopener noreferrer">@official_mfu</Link>
        <p>mfu.org.tr</p>
      </div>
    </div>
  );
}