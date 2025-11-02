"use client"

import { motion, useScroll, useTransform } from "framer-motion";
import { useEffect, useState } from "react";

export default function Header() {
  const { scrollY } = useScroll();
  const [mounted, setMounted] = useState(false);

  const opacity = useTransform(scrollY, [0, 600], [0, 1]);
  const translateY = useTransform(scrollY, [0, 800], [-100, 0]);

  useEffect(() => {
    setMounted(true);
  }, []);

  const menus = [
    { title: "Team" },
        { title: "Committees" },
    { title: "Schedule" },
    { title: "Contact Us" },

  ]

  if (!mounted) return null;

  return (
    <motion.header
      className="flex justify-between border border-[#4A3811] items-center px-[2%] py-3 fixed top-6 inset-x-0 mx-auto z-50 w-[80%] h-20 rounded-2xl p-4 bg-[#FAF4E6]"
      style={{ opacity, translateY }}
      transition={{
        duration: 5,
        ease: "backInOut"
      }}
    >
        <p className="text-3xl">Model Fictional Universe</p>

      <div className="flex gap-5 items-center">
        {
          menus.map((menu, index) => (
            <p key={index} className="text-lg text-default-800 flex items-center gap-3">{menu.title}</p>
          ))
        }
      </div>
    </motion.header>
  );
}
