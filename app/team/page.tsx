import { patternURL } from "@/lib/constant";
import Image from "next/image";

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
        <p className="text-xl sm:text-2xl md:text-3xl lg:text-4xl mb-2 sm:mb-3 text-amber-500 text-center">Team of</p>
        <h1 className="px-2 py-1.5 sm:px-3 sm:py-2 bg-amber-500 text-2xl sm:text-3xl md:text-4xl lg:text-5xl text-center">
          Model Fictional Universe
        </h1>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 sm:gap-5 md:gap-6 px-2 sm:px-4 md:px-[8%] lg:px-[15%] mt-8 sm:mt-12 w-full max-w-6xl">
          {[
            {
              name: "Ebru Özçelik",
              role: "Founder and Secretary General",
              image: "/assets/members/sg.png",
            },
            {
              name: "Elif Enda Çelik",
              role: "Director General",
              image: "/assets/members/dg.png",
            },
            {
              name: "Arda Zorbozan",
              role: "Deputy Secretary General",
              image: "/assets/members/dsg.jpeg",
            },
            {
              name: "Nehir Coşkun",
              role: "Deputy Director General",
              image: "/assets/members/ddg.png",
            },
          ].map((member) => (
            <div key={member.name} className="relative aspect-[3/4] min-h-[280px] sm:min-h-[320px] overflow-hidden rounded-lg shadow-lg">
              <Image
                src={member.image}
                alt={`${member.name}, ${member.role}`}
                className="object-cover w-full h-full"
                width={1500}
                height={2000}
                sizes="(max-width: 640px) 100vw, (max-width: 1024px) 50vw, 25vw"
              />
              <div className="w-full absolute z-50 left-0 bottom-0 p-3 sm:p-4 bg-linear-to-t from-black/70 to-transparent min-h-[40%] flex flex-col justify-end items-start">
                <p className="font-bold text-amber-500 text-base sm:text-lg md:text-xl lg:text-2xl relative z-50 leading-tight">
                  {member.name}
                </p>
                <p className="font-bold text-gray-300 text-xs sm:text-sm md:text-base relative z-50 leading-tight">
                  {member.role}
                </p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </main>
  );
}
