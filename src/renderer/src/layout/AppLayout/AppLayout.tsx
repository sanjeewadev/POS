// src/renderer/src/layout/AppLayout/AppLayout.tsx
import { ReactNode } from 'react'
import TopNavigationBar from '../../components/TopNavigationBar/TopNavigationBar'
import Sidebar from '../../components/Sidebar/Sidebar'
import styles from './AppLayout.module.css'

interface Props {
  currentMode: string
  setMode: (mode: string) => void
  children: ReactNode // This allows us to inject the POS or Inventory screen inside
}

export default function AppLayout({ currentMode, setMode, children }: Props) {
  return (
    <div className={styles.appContainer}>
      {/* 1. The Top Navigation Bar stays fixed at the top */}
      <TopNavigationBar currentMode={currentMode} setMode={setMode} />

      {/* 2. The lower section is split into two columns */}
      <div className={styles.bodyWrapper}>
        <Sidebar />

        {/* 3. The actual page content loads inside this main area */}
        <main className={styles.mainContent}>{children}</main>
      </div>
    </div>
  )
}
