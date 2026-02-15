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
    { title: "Applications", href: "/applications" },
    { title: "Team", href: "/team" },
    { title: "Committees", href: "/team" },
    { title: "Schedule", href: "/team" },
    { title: "Contact Us", href: "/team" },
  ]

  if (!mounted) return null;

  return (
    <header
      className="flex justify-between border border-[#4A3811] items-center px-2 sm:px-4 md:px-6 lg:px-[2%] py-2 sm:py-3 fixed top-2 sm:top-4 md:top-6 inset-x-0 mx-auto z-50 w-[95%] sm:w-[92%] md:w-[90%] lg:w-[80%] min-h-14 sm:h-16 md:h-20 rounded-xl sm:rounded-2xl bg-[#FAF4E6]"
      style={{ opacity, translateY }}
      transition={{
        duration: 5,
        ease: "backInOut"
      }}
    >
      <Link href="/" className="text-xl sm:text-2xl md:text-3xl font-bold text-[#264653] min-w-[3rem]">MFU</Link>

      <nav className="flex gap-1 sm:gap-2 md:gap-3 lg:gap-5 items-center flex-wrap justify-end">
        {menus.map((menu, index) => (
          <Link
            href={menu.href}
            key={index}
            className="text-xs sm:text-sm md:text-base lg:text-lg text-default-800 py-2 px-1 sm:px-2 rounded touch-manipulation min-h-[44px] min-w-[44px] flex items-center justify-center"
          >
            {menu.title}
          </Link>
        ))}
      </nav>
    </header>
  );
}
