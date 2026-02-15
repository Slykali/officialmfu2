import { patternURL } from "@/lib/constant";
import Image from "next/image";
import Link from "next/link";

const APPLICATION_FORMS = [
  { url: "https://forms.gle/iNB6nyCfY59LSzvr7", label: "Delegate (individual)" },
  { url: "https://forms.gle/fTVcZwc7ur3p8Dfe7", label: "Chair" },
  {
    url: "https://docs.google.com/forms/d/e/1FAIpQLSfodYw5w63wLgbGZ-kI9kbwIA-IlDMB5hByGrjr9VZtQ5WC9g/viewform?usp=publish-editor",
    label: "Press",
  },
] as const;

const PRICING = [
  { amount: "2.200 TL", label: "Delegate" },
  { amount: "3.000 TL", label: "Delegates using accommodation" },
  { amount: "1.500 TL", label: "Organization members" },
  { amount: "1.500 TL", label: "Chair Board" },
] as const;

export default function Page() {
  return (
    <main
      style={{
        backgroundImage: patternURL,
      }}
      className="bg-[#FAF4E6] pb-[15rem] overflow-x-hidden pt-24 sm:pt-28 md:pt-32 px-4 sm:px-6"
    >
      <Image
        alt=""
        width={735}
        height={435}
        className="absolute top-0 right-0 h-40 sm:h-52 md:h-64 lg:h-[30rem] w-auto z-50 object-contain max-w-[50%]"
        src="/assets/clock.png"
      />

      <div className="mt-8 sm:mt-12 w-full max-w-[100vw] min-h-screen flex flex-col justify-start items-center">
        <p className="text-xl sm:text-2xl md:text-3xl lg:text-4xl mb-2 sm:mb-3 text-amber-500 text-center">
          Applications for
        </p>
        <h1 className="px-2 py-1.5 sm:px-3 sm:py-2 bg-amber-500 text-2xl sm:text-3xl md:text-4xl lg:text-5xl text-center">
          Model Fictional Universe
        </h1>

        {/* Apply as a delegate - responsive butonlar */}
        <section className="w-full max-w-3xl mt-8 sm:mt-12 flex flex-col items-center">
          <h2 className="text-xl sm:text-2xl md:text-3xl lg:text-4xl font-bold text-[#264653] mb-4 sm:mb-6 text-center px-2">
            Apply as a delegate
          </h2>
          <div className="flex flex-col sm:flex-row gap-3 sm:gap-4 flex-wrap justify-center w-full sm:w-auto">
            {APPLICATION_FORMS.map((form, index) => (
              <Link
                key={index}
                href={form.url}
                target="_blank"
                rel="noopener noreferrer"
                className="px-4 py-3 sm:px-6 sm:py-4 bg-amber-500 hover:bg-amber-600 active:bg-amber-700 text-[#264653] font-bold text-base sm:text-lg rounded-xl transition-colors shadow-md w-full sm:min-w-[200px] sm:w-auto text-center touch-manipulation"
              >
                {form.label}
              </Link>
            ))}
          </div>
        </section>

        {/* Fiyat listesi - phone / tablet / PC */}
        <section className="w-full max-w-3xl mt-10 sm:mt-16 px-0 sm:px-4">
          <h2 className="text-lg sm:text-xl md:text-2xl lg:text-3xl font-bold text-[#264653] mb-4 sm:mb-6 text-center">
            Participation fees
          </h2>
          <ul className="space-y-3 sm:space-y-4 w-full">
            {PRICING.map((item, index) => (
              <li
                key={index}
                className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-1 sm:gap-2 p-3 sm:p-4 rounded-xl border-2 border-amber-500/30 bg-[#FAF4E6]/80"
              >
                <span className="font-bold text-amber-700 text-base sm:text-lg order-2 sm:order-1">
                  {item.amount}
                </span>
                <span className="text-[#755F37] text-sm sm:text-base md:text-lg order-1 sm:order-2">
                  {item.label}
                </span>
              </li>
            ))}
          </ul>
        </section>
      </div>
    </main>
  );
}
