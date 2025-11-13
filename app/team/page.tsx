import { patternURL } from "@/lib/constant";
import Image from "next/image";

export default function Page() {
  return (
    <main
      style={{
        backgroundImage: patternURL,
      }}
      className="bg-[#FAF4E6] pb-[15rem] overflow-x-hidden pt-32"
    >
      <Image
        alt=""
        width={735}
        height={435}
        className="absolute top-0 h-52 lg:h-[30rem] w-auto z-50"
        src="/assets/clock.png"
      />

      <div className="mt-12 w-screen h-full min-h-screen flex flex-col justify-start items-center pt-[%50]">
        <p className="text-2xl lg:text-4xl mb-3 text-amber-500">Team of</p>
        <h1 className="px-3 py-2 bg-amber-500 text-3xl lg:text-5xl">
          Model Fictional Universe
        </h1>

        <div className="grid grid-cols-1 lg:grid-cols-4 gap-3 px-[15%] mt-12 h-full">
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
              name: "Betül Yüksel",
              role: "Deputy Secretary General",
              image: "/assets/members/dsg.png",
            },
            {
              name: "Nehir Coşkun",
              role: "Deputy Director General",
              image: "/assets/members/ddg.png",
            },
          ].map((member) => (
            <div className="relative">
              <Image
                src={member.image}
                alt=""
                className=""
                width={1500}
                height={2000}
              />
              <div className="w-full absolute z-50 left-0 bottom-0 p-4 bg-linear-to-t from-black/60 to-transparent h-full flex flex-col justify-end items-start">
                <p className="font-bold text-amber-500 text-2xl relative z-50">
                  {member.name}
                </p>
                <p className="font-bold text-gray-300 relative z-50">
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
