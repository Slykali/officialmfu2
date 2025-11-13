"use client";

import { patternURL } from "@/lib/constant";
import { motion, useMotionValue, useTransform } from "framer-motion";
import Image from "next/image";
import { useEffect, useState } from "react";
import { TbMinus, TbPlus } from "react-icons/tb";

const faq = [
  {
    "question": "Who can participate?",
    "answer": "MFU is open to high school and university students passionate about storytelling, world-building, and creative dialogue."
  },
  {
    "question": "What is the official language of MFU?",
    "answer": "The official language of MFU is English. However, depending on the committee, bilingual or localized sessions may occur."
  },
  {
    "question": "What is the dress code?",
    "answer": "Participants are encouraged to follow a formal dress code. Light thematic accessories or subtle cosplay elements are allowed if appropriate for the committee."
  },
  {
    "question": "What are the committees about?",
    "answer": "Each committee represents a different fictional universe — from sci-fi to fantasy to modern storytelling."
  },
  {
    "question": "Do I need prior MUN experience?",
    "answer": "No! MFU focuses on creativity and storytelling — anyone passionate about fictional worlds is welcome."
  },
  {
    "question": "How are characters assigned?",
    "answer": "After registration, participants receive their character profiles and role documents including background and goals."
  },
  {
    "question": "What makes MFU unique?",
    "answer": "MFU blends storytelling and diplomacy — it’s not only about discussion, it’s about creation. Here, every word can shape an entire universe."
  },
  {
    "question": "Will delegates receive a certificate?",
    "answer": "Delegates must attend all sessions. A maximum of two absences is allowed — exceeding this limit means no certificate will be awarded."
  }
]


export default function Page() {
  const x = useMotionValue(0);
  const y = useMotionValue(0);

    const [openIndex, setOpenIndex] = useState<number | null>(null);

  const toggleFAQ = (index: number) => {
    setOpenIndex(openIndex === index ? null : index);
  };
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
      className="bg-[#FAF4E6] pb-[15rem] overflow-x-hidden"
    >
      <motion.div
        className="relative w-screen h-screen flex items-center justify-center overflow-hidden bg-gray-950 lg:perspective-[1000px]"
        style={{ rotateX, rotateY }}
      >
                  <Image
            className="w-full h-full z-30 object-cover lg:hidden absolute"
            src="/assets/hero2.png"
            alt="Parchment"
            width={736}
            height={1308}
          />
          <h1 className="lg:text-9xl text-5xl z-40 relative text-white text-center">The castle whispers <br/> your name…</h1>
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

        <div className="bg-linear-to-t from-[#FAF4E6] to-transparent absolute left-0 bottom-0 z-50 w-full h-[50%]"></div>
      </motion.div>

      <div className="mt-12 w-screen h-screen flex flex-col gap-3 justify-center items-center max-md:px-12">
        <Image
          className="w-32 h-32"
          src="/assets/dragon.svg"
          alt=""
          width={500}
          height={500}
        />
        <p className="text-3xl text-center lg:text-6xl text-[#264653]">
          When facts are gone, the fiction goes on.
        </p>
      </div>

      <div className="mt-12 w-screen h-full min-h-screen flex flex-col justify-start items-center pt-[%50] relative">
        <p className="text-2xl lg:text-4xl mb-3 text-amber-500">What Is</p>
        <h1 className="px-3 py-2 bg-amber-500 text-3xl lg:text-5xl">
          Model Fictional Universe
        </h1>
        <p className="w-[90%] lg:w-[65%] mx-auto text-center text-lg lg:text-3xl mt-5">
          Model Fictional Universe (MFU) is a simulation conference where
          participants take on different characters within a multi-universe,
          story-driven environment. Each committee has its own universe, and
          participants experience events, crises, and storylines from their
          characters’ perspectives in an interactive setting. MFU is a unique
          platform that combines imagination with strategic thinking.
        </p>

          <Image
          alt=""
          width={735}
          height={435}
          className="absolute -right-[25%] lg:right-0 max-md:-top-[22%] lg:bottom-0"
          src="/assets/dragon_green.png"
          />
      </div>

      <div className="mt-12 w-screen h-full min-h-screen flex flex-col justify-start items-center pt-[%50] relative">
        <p className="text-2xl lg:text-4xl mb-3 text-amber-500">Vision of</p>
        <h1 className="px-3 py-2 bg-amber-500 text-3xl lg:text-5xl">
          Model Fictional Universe
        </h1>
        <p className="w-[90%] lg:w-[65%] mx-auto text-center text-lg lg:text-3xl mt-5">
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
                  <Image
          alt=""
          width={735}
          height={435}
          className="absolute left-[-22%] top-[-50%] lg:top-0"
          src="/assets/ship.png"
          />
      </div>

      <div className="mt-12 w-screen h-full min-h-screen flex flex-col justify-start items-center pt-[%50] relative">
        <p className="text-2xl lg:text-4xl mb-3 text-amber-500">Mision of</p>
        <h1 className="px-3 py-2 bg-amber-500 text-3xl lg:text-5xl">
          Model Fictional Universe
        </h1>
        <p className="w-[90%] lg:w-[65%] mx-auto text-center text-lg lg:text-3xl mt-5">
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
        <Image
          alt=""
          width={735}
          height={435}
          className="absolute right-[-19%] -top-[15%] lg:-top-[44%] max-md:w-40 max-md:h-auto "
          src="/assets/owl.png"
          />
      </div>

      <div className="w-screen h-full min-h-screen flex max-md:flex-col justify-center items-center">
        <div className="relative h-90 lg:h-160">
          <Image
          alt=""
          width={735}
          height={435}
          className="top-[-5%] left-[-6%] absolute h-100 lg:h-180 z-50 min-w-80 lg:min-w-140"
          src="/assets/frame.png"/>
          <Image
            src="/assets/letter_sg.png"
            alt=""
            className="h-full w-full z-30"
            width={1500}
            height={2000}
          />
        </div>

        <div className="relative w-[90%] lg:w-[50%] max-md:mt-32 flex justify-center items-center">
          <Image
            className="max-md:hidden w-full h-auto"
            src="/assets/parchment.png"
            alt="Parchment"
            width={1000}
            height={1000}
          />
          <div className="flex flex-col w-full gap-3 relative lg:absolute text-center items-center justify-start">
            <p className="text-2xl">Letter from Founder & Secretary General</p>
            <p className="text-xl text-[#755F37] w-[90%] lg:w-[60%]">
              Dear participants, The creation of a universe always begins with
              an idea. MFU is not merely a platform where fictional worlds
              collide — it is a journey built on imagination, courage, and
              belief. Every character, every decision, and every crisis
              represents the power of young minds to reshape worlds.
              <br />
              <br />
              This project was a long journey — from the pages of a dream to the
              edge of reality. It was built on sleepless nights, dedication, and
              faith. When I founded MFU, my goal was not only to create a
              conference, but a center where different universes and stories
              could intersect. Here, ideas merge, characters rewrite their
              destinies, and every universe finds its voice.
              <br />
              <br />
              MFU is not the echo of one world — it is the reflection of
              countless ones. The line between reality and fiction fades here,
              for we seek the meaning behind every story. We build universes —
              sometimes with a word, sometimes with a spark of hope.
              <br />
              <br />
              Everyone reading these words is now part of this story. We built
              this universe with our hearts, nurtured it with faith, and protect
              it with hope.
              <br />
              <br />
              And remember — whether you rise from the shadows or walk within
              the light... The good always win in every universe.
              <br />
            </p>
          </div>
        </div>
      </div>

      <div id="faq" className="w-[90%] max-md:mx-auto lg:w-screen h-full min-h-screen mt-32 flex flex-col items-center gap-12">
        <Image
        src="/assets/logo.png"
        width={1000}
        height={1000}
        className="size-120"
        />
    {faq.map((item, index) => (
        <div
          key={index}
          className="flex flex-col items-center lg:w-full cursor-pointer transition-all duration-300"
        >
          <h1
            onClick={() => toggleFAQ(index)}
            className="text-xl lg:text-3xl text-[#BC9764] inline-flex gap-2 items-center font-bold text-center lg:mx-auto lg:w-min whitespace-nowrap select-none"
          >
            {openIndex === index ? <TbMinus /> : <TbPlus />} {item.question}
          </h1>

          {/* Cevap kısmı sadece açık olan indexte görünür */}
          <p
            className={`w-[70%] lg:w-[50%] text-md lg:text-xl mt-2 text-center mx-auto overflow-hidden transition-all duration-500 ${
              openIndex === index
                ? "max-h-[500px] opacity-100"
                : "max-h-0 opacity-0"
            }`}
          >
            {item.answer}
          </p>
        </div>
      ))}
      </div>
    </main>
  );
}
