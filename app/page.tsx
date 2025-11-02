"use client";

import { patternURL } from "@/lib/constant";
import { motion, useMotionValue, useTransform } from "framer-motion";
import Image from "next/image";
import { useEffect } from "react";

export default function DepthScene2Layer() {
  const x = useMotionValue(0);
  const y = useMotionValue(0);

  // mouse hareketini yakala
  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      const { innerWidth, innerHeight } = window;
      const offsetX = (e.clientX - innerWidth / 2) / innerWidth;
      const offsetY = (e.clientY - innerHeight / 2) / innerHeight;
      x.set(offsetX);
      y.set(offsetY);
    };
    window.addEventListener("mousemove", handleMouseMove);
    return () => window.removeEventListener("mousemove", handleMouseMove);
  }, [x, y]);

  // Arka plan biraz yavaş hareket eder
  const bgX = useTransform(x, [-0.5, 0.5], [30, -30]);
  const bgY = useTransform(y, [-0.5, 0.5], [30, -30]);

  // Ön plan daha hızlı hareket eder
  const fgX = useTransform(x, [-0.5, 0.5], [80, -80]);
  const fgY = useTransform(y, [-0.5, 0.5], [80, -80]);

  // Kamerayı hafif döndür (perspektif efekti)
  const rotateX = useTransform(y, [-0.5, 0.5], [8, -8]);
  const rotateY = useTransform(x, [-0.5, 0.5], [-8, 8]);

  return (
    <main
      style={{
        backgroundImage: patternURL,
      }}
      className="bg-[#FAF4E6] pb-[15rem]"
    >
      <motion.div
        className="relative w-screen h-screen overflow-hidden bg-gray-950 perspective-[1000px]"
        style={{ rotateX, rotateY }}
      >
        {/* BACKGROUND */}
        <motion.img
          src="/assets/background.png"
          alt="Background"
          className="absolute inset-0 w-full h-full object-cover"
          style={{
            x: bgX,
            y: bgY,
            scale: 1.1,
            zIndex: 1,
          }}
        />

        {/* FOREGROUND */}
        <motion.img
          src="/assets/foreground.png"
          alt="Foreground"
          className="absolute inset-0 w-full h-full object-cover pointer-events-none"
          style={{
            x: fgX,
            y: fgY,
            scale: 1.2,
            zIndex: 3,
          }}
        />

        {/* Yazı örneği */}
        {/*
          <motion.div
          className="absolute right-0 top-0 p-[4%] flex items-center justify-left text-[#E9C46A] text-[12rem]/50 font-bold drop-shadow-2xl"
          style={{ x: fgX, y: fgY, zIndex: 2 }}
        >
          Model <br /> Fictional <br /> Universe
        </motion.div>
          */}

        <div className="bg-linear-to-t from-[#FAF4E6]/20 to-transparent absolute left-0 bottom-0 z-50 w-full h-[30%]"></div>
      </motion.div>

      <div className="mt-12 w-screen h-screen flex flex-col gap-3 justify-center items-center">
        <Image
          className="w-32 h-32"
          src="/assets/dragon.svg"
          alt=""
          width={500}
          height={500}
        />
        <p className="text-6xl text-[#264653]">
          When facts are gone, the fiction goes on.
        </p>
      </div>

      <div className="mt-12 w-screen h-screen flex flex-col justify-start items-center pt-[%50]">
        <p className="text-4xl mb-3 text-amber-500">Introduce</p>
        <h1 className="px-3 py-2 bg-amber-500 text-5xl">
          Model Fictional Universe
        </h1>
        <p className="w-[65%] mx-auto text-center text-3xl mt-5">
          Model Fictional Universe (MFU) is a simulation conference where
          participants take on different characters within a multi-universe,
          story-driven environment. Each committee has its own universe, and
          participants experience events, crises, and storylines from their
          characters’ perspectives in an interactive setting. MFU is a unique
          platform that combines imagination with strategic thinking.
        </p>
      </div>

      <div className="mt-12 w-screen h-screen flex flex-col justify-start items-center pt-[%50]">
        <p className="text-4xl mb-3 text-amber-500">Vision of</p>
        <h1 className="px-3 py-2 bg-amber-500 text-5xl">
          Model Fictional Universe
        </h1>
        <p className="w-[65%] mx-auto text-center text-3xl mt-5">
          Model Fictional Universe is an innovative platform that unites
          imagination with academic thought to inspire the next generation of
          leaders, thinkers, and storytellers. We believe that ideas have the
          power to build universes. MFU offers participants not just a
          conference, but a multidimensional journey of thinking, creating, and
          imagining.
          <br />
          <br />
          Our vision is to empower young individuals to enhance their
          creativity, problem-solving, and leadership skills — and to inspire
          them to make a difference in the real world through fictional
          universes. Every universe begins with an idea — and we exist to help
          those ideas come to life.
          <br />
          <br />
          MFU aims to be a meeting point for young people who think beyond
          boundaries and shape the future with an interdisciplinary vision.
        </p>
      </div>

      <div className="mt-12 w-screen h-screen flex flex-col justify-start items-center pt-[%50]">
        <p className="text-4xl mb-3 text-amber-500">Mision of</p>
        <h1 className="px-3 py-2 bg-amber-500 text-5xl">
          Model Fictional Universe
        </h1>
        <p className="w-[65%] mx-auto text-center text-3xl mt-5">
          Model Fictional Universe provides a multidimensional learning
          environment where young people can develop their creativity, critical
          thinking, and communication skills. Our mission is to bring together
          different disciplines and support youth in developing their
          analytical, leadership, and innovative abilities through fictional
          worlds.
          <br />
          <br />
          MFU offers not just a conference experience, but an opportunity to
          transform imagination into tangible projects. We aim to create a
          community where every young individual can build their own universe,
          where ideas turn into action, and where boundaries are redefined
          through creativity.
          <br />
          <br />
          For us, MFU is more than an event — it is a movement where young
          people redefine the future.
        </p>
      </div>

      <div className="mt-12 w-screen h-screen flex flex-col justify-start items-center pt-[%50]">
        <p className="text-4xl mb-3 text-amber-500">Team of</p>
        <h1 className="px-3 py-2 bg-amber-500 text-5xl">
          Model Fictional Universe
        </h1>

        <div className="grid grid-cols-4 gap-3 px-[15%] mt-12">
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
                <p className="font-bold text-amber-500 text-2xl relative z-50">{member.name}</p>
                                <p className="font-bold text-gray-300 relative z-50">{member.role}</p>

              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="w-screen h-full min-h-screen flex justify-center items-center">
        <Image
          src="/assets/parchment.png"
          alt="Parchment"
          width={1000}
          height={1000}
        />
        <div className="flex flex-col gap-3 absolute text-center items-center">
          <p className="text-4xl">Letter from Secretary General</p>
          <p className="text-2xl text-[#755F37] w-[30%]">
            Lorem ipsum dolor sit amet consectetur adipisicing elit. Vero labore
            vel quas consequuntur, eos minus provident adipisci quod maxime
            dolor eaque aspernatur sed, exercitationem facere enim delectus?
            Voluptatum, veritatis quasi.
            <br />
            <br />
            Lorem ipsum dolor sit amet consectetur adipisicing elit. Vero labore
            vel quas consequuntur, eos minus provident adipisci quod maxime
            dolor eaque aspernatur sed, exercitationem facere enim delectus?
            Voluptatum, veritatis quasi.
            <br />
            <br />
            Lorem ipsum dolor sit amet consectetur adipisicing elit. Vero labore
            vel quas consequuntur, eos minus provident adipisci quod maxime
            dolor eaque aspernatur sed, exercitationem facere enim delectus?
            Voluptatum, veritatis quasi.
            <br />
            <br />
          </p>
        </div>
      </div>
    </main>
  );
}
