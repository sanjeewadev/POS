// src/renderer/src/components/Sidebar/Sidebar.tsx
import { useState, useEffect } from 'react'
import styles from './Sidebar.module.css'

export default function Sidebar() {
  // 🚀 A live clock is crucial for cashiers
  const [time, setTime] = useState(new Date())

  useEffect(() => {
    const timer = setInterval(() => setTime(new Date()), 1000)
    return () => clearInterval(timer)
  }, [])

  return (
    <aside className={styles.sidebarContainer}>
      {/* 1. Live Time & Date Widget */}
      <div className={styles.timeWidget}>
        <div className={styles.timeText}>
          {time.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
        </div>
        <div className={styles.dateText}>
          {time.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' })}
        </div>
      </div>

      {/* 2. Quick Touch Actions */}
      <div className={styles.quickActions}>
        <button className={styles.actionBtn}>
          <span className={styles.icon}>💰</span>
          OPEN DRAWER
        </button>
        <button className={styles.actionBtn}>
          <span className={styles.icon}>🖨️</span>
          LAST RECEIPT
        </button>
        <button className={styles.actionBtn}>
          <span className={styles.icon}>🖩</span>
          CALCULATOR
        </button>
        <button className={styles.actionBtn}>
          <span className={styles.icon}>🔒</span>
          LOCK REGISTER
        </button>
      </div>

      {/* 3. System Status Indicator */}
      <div className={styles.systemStatus}>
        <div className={styles.statusIndicator}>
          <div className={styles.pulseDot}></div>
          <span style={{ fontWeight: 800 }}>SYSTEM ONLINE</span>
        </div>
        <div className={styles.versionText}>Version 2.0.0</div>
      </div>
    </aside>
  )
}
