"use client"

import { motion, useScroll, useTransform } from "framer-motion";
import Link from "next/link";
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
    { title: "Team", href: "/team" },
        { title: "Committees", href: "/team" },
    { title: "Schedule", href: "/team" },
    { title: "Contact Us", href: "/team" },

  ]

  if (!mounted) return null;

  return (
    <header
      className="flex justify-between border border-[#4A3811] items-center px-4 lg:px-[2%] py-3 fixed top-6 inset-x-0 mx-auto z-50 w-[90%] lg:w-[80%] h-20 rounded-2xl p-4 bg-[#FAF4E6]"
      style={{ opacity, translateY }}
      transition={{
        duration: 5,
        ease: "backInOut"
      }}
    >
        <Link href="/" className="text-lg: text-3xl">MFU</Link>

      <div className="flex gap-2 lg:gap-5 items-center">
        {
          menus.map((menu, index) => (
            <Link href={menu.href} key={index} className="text-sm lg:text-lg text-default-800 flex items-center gap-3">{menu.title}</Link>
          ))
        }
      </div>
    </header>
  );
}
