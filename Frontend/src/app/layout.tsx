import type { Metadata } from "next";
import "./globals.css";
export const metadata: Metadata = { title: "Bếp Nhà • Restaurant Ordering", description: "Quản lý gọi món, bếp, phục vụ và thanh toán tại nhà hàng." };
export default function RootLayout({children}: Readonly<{children: React.ReactNode}>) { return <html lang="vi"><body>{children}</body></html>; }
