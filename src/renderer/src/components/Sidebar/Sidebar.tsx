// src/renderer/src/components/Sidebar/Sidebar.tsx
import { useState, useEffect } from 'react'
import styles from './Sidebar.module.css'
import { RiMoneyDollarBoxLine } from 'react-icons/ri'
import { RiPrinterLine } from 'react-icons/ri'
import { RiCalculatorLine } from 'react-icons/ri'
import { RiLockLine } from 'react-icons/ri'

const QUICK_ACTIONS = [
  { icon: <RiMoneyDollarBoxLine size={20} />, label: 'Open Drawer' },
  { icon: <RiPrinterLine size={20} />, label: 'Last Receipt' },
  { icon: <RiCalculatorLine size={20} />, label: 'Calculator' },
  { icon: <RiLockLine size={20} />, label: 'Lock Register' }
]

export default function Sidebar() {
  // A live clock is crucial for cashiers
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
        {QUICK_ACTIONS.map(({ icon, label }) => (
          <button key={label} className={styles.actionBtn}>
            <span className={styles.actionIcon}>{icon}</span>
            <span className={styles.actionLabel}>{label}</span>
          </button>
        ))}
      </div>

      {/* 3. System Status */}
      <div className={styles.systemStatus}>
        <div className={styles.statusIndicator}>
          <div className={styles.pulseDot} />
          <span className={styles.statusText}>Online</span>
        </div>
        <div className={styles.versionText}>v2.0.0</div>
      </div>
    </aside>
  )
}
