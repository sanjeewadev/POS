// src/renderer/src/layout/AppLayout/AppLayout.tsx
import React, { useState } from 'react'
import Swal from 'sweetalert2'
import { useAuth } from '../../store/AuthContext'
import TopNavigationBar from '../../components/TopNavigationBar/TopNavigationBar'
import Sidebar from '../../components/Sidebar/Sidebar'
import CalculatorModal from '../../components/Calculator/CalculatorModal' // 🚀 The new component
import styles from './AppLayout.module.css'
import { RiLockLine } from 'react-icons/ri'

interface AppLayoutProps {
  children: React.ReactNode
  currentMode: string
  setMode: (mode: string) => void
  onOpenTodaysSales: () => void
}

export default function AppLayout({
  children,
  currentMode,
  setMode,
  onOpenTodaysSales
}: AppLayoutProps) {
  const { currentUser, login } = useAuth()

  // State Management
  const [isLocked, setIsLocked] = useState(false)
  const [unlockPassword, setUnlockPassword] = useState('')
  const [isUnlocking, setIsUnlocking] = useState(false)

  // 🚀 New state strictly for the Calculator Component
  const [isCalculatorOpen, setIsCalculatorOpen] = useState(false)

  const handleOpenDrawer = async () => {
    try {
      // @ts-ignore
      if (window.api.openCashDrawer) await window.api.openCashDrawer()
      Swal.fire({
        toast: true,
        position: 'top-end',
        icon: 'success',
        title: 'Cash Drawer Opened',
        showConfirmButton: false,
        timer: 1500
      })
    } catch (err) {
      Swal.fire('Hardware Error', 'Could not open cash drawer. Is it connected?', 'error')
    }
  }

  const handleLastReceipt = async () => {
    try {
      // @ts-ignore
      if (window.api.printLastReceipt) await window.api.printLastReceipt()
      Swal.fire({
        toast: true,
        position: 'top-end',
        icon: 'info',
        title: 'Printing Last Receipt...',
        showConfirmButton: false,
        timer: 2000
      })
    } catch (err) {
      Swal.fire('Printer Error', 'Could not print receipt. Check printer connection.', 'error')
    }
  }

  const handleUnlock = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!currentUser || !unlockPassword) return
    setIsUnlocking(true)
    const result = await login(currentUser.Username, unlockPassword)
    if (result.success) {
      setIsLocked(false)
      setUnlockPassword('')
    } else {
      Swal.fire('Access Denied', 'Incorrect password. Register remains locked.', 'error')
      setUnlockPassword('')
    }
    setIsUnlocking(false)
  }

  return (
    <div className={styles.appWrapper}>
      {/* 🚀 The New Calculator Component */}
      {isCalculatorOpen && <CalculatorModal onClose={() => setIsCalculatorOpen(false)} />}

      {/* Lock Screen Overlay */}
      {isLocked && (
        <div className={styles.lockScreen}>
          <div className={styles.lockBox}>
            <div className={styles.lockIcon}>
              <RiLockLine size={40} />
            </div>
            <h2 className={styles.lockTitle}>Register Locked</h2>
            <p className={styles.lockSubtitle}>
              Locked by: <span className={styles.lockUser}>{currentUser?.FullName}</span>
            </p>
            <form onSubmit={handleUnlock} className={styles.lockForm}>
              <input
                type="password"
                className="pos-input"
                style={{ textAlign: 'center', fontSize: '24px', letterSpacing: '5px' }}
                placeholder="Enter Password"
                value={unlockPassword}
                onChange={(e) => setUnlockPassword(e.target.value)}
                autoFocus
                required
              />
              <button
                type="submit"
                className="pos-btn success"
                style={{ width: '100%', marginTop: '15px' }}
                disabled={isUnlocking}
              >
                {isUnlocking ? 'VERIFYING...' : 'UNLOCK REGISTER'}
              </button>
            </form>
          </div>
        </div>
      )}

      <TopNavigationBar currentMode={currentMode} setMode={setMode} />

      <div className={styles.mainLayout}>
        <Sidebar
          onOpenDrawer={handleOpenDrawer}
          onLastReceipt={handleLastReceipt}
          onCalculator={() => setIsCalculatorOpen(true)} // 🚀 Triggers the new state
          onTodaysSales={onOpenTodaysSales}
          onLockRegister={() => setIsLocked(true)}
        />
        <main className={styles.contentArea}>{children}</main>
      </div>
    </div>
  )
}
