// src/renderer/src/components/Sidebar/Sidebar.tsx
import { useState, useEffect } from 'react'
import styles from './Sidebar.module.css'
import { RiMoneyDollarBoxLine, RiPrinterLine, RiCalculatorLine, RiLockLine } from 'react-icons/ri'

interface SidebarProps {
  onOpenDrawer: () => void
  onLastReceipt: () => void
  onCalculator: () => void
  onLockRegister: () => void
}

export default function Sidebar({
  onOpenDrawer,
  onLastReceipt,
  onCalculator,
  onLockRegister
}: SidebarProps) {
  const [time, setTime] = useState(new Date())

  useEffect(() => {
    const timer = setInterval(() => setTime(new Date()), 1000)
    return () => clearInterval(timer)
  }, [])

  const actions = [
    { icon: <RiMoneyDollarBoxLine size={22} />, label: 'Open Drawer', onClick: onOpenDrawer },
    { icon: <RiPrinterLine size={22} />, label: 'Last Receipt', onClick: onLastReceipt },
    { icon: <RiCalculatorLine size={22} />, label: 'Calculator', onClick: onCalculator }
  ]

  return (
    <aside className={styles.sidebarContainer}>
      {/* Time & Date */}
      <div className={styles.timeWidget}>
        <div className={styles.timeText}>
          {time.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
        </div>
        <div className={styles.dateText}>
          {time.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' })}
        </div>
      </div>

      {/* Quick Actions */}
      <div className={styles.navActions}>
        {actions.map(({ icon, label, onClick }) => (
          <button key={label} className={styles.actionBtn} onClick={onClick}>
            <span className={styles.actionIcon}>{icon}</span>
            <span className={styles.actionText}>{label}</span>
          </button>
        ))}

        {/* Lock separated at the bottom */}
        <button className={`${styles.actionBtn} ${styles.lockBtn}`} onClick={onLockRegister}>
          <span className={styles.actionIcon}>
            <RiLockLine size={22} />
          </span>
          <span className={styles.actionText}>Lock Register</span>
        </button>
      </div>

      {/* System Status */}
      <div className={styles.systemStatus}>
        <div className={styles.statusIndicator}>
          <span className={styles.statusDot} />
          <span className={styles.statusOnline}>Online</span>
        </div>
        <span className={styles.versionText}>v2.0.0</span>
      </div>
    </aside>
  )
}
