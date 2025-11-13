import { darkPatternURL, patternURL } from "@/lib/constant";
import Image from "next/image";
import Link from "next/link";

export default function Footer() {
  return (
    <div style={{
        backgroundImage: darkPatternURL
    }} className="footer sticky z-0 bottom-0 left-0 w-full h-80 flex justify-center items-center bg-[#0f0b01]">
      <div className="relative overflow-hidden w-full h-full flex justify-end px-12 text-right items-start py-12 ">
        <h2
          className="absolute bottom-0 left-0 p-12 sm:text-8xl 
          text-2xl  font-calendas transition-all text-amber-500"
        >
          Model Fictional Universe
        </h2>
      </div>
      <div className="absolute flex max-md:flex-col gap-2 *:text-amber-200 *:text-2xl top-12 left-12">
        <Link href="mailto:officialmodelfu@gmail.com">officialmodelfu@gmail.com</Link>
        <Link href="https://instagram.com/official_mfu" target="_blank">@official_mfu</Link>
                <p>mfu.org.tr</p>

      </div>
    </div>
  );
}