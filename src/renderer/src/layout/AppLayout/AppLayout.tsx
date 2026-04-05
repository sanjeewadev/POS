// src/renderer/src/layout/AppLayout/AppLayout.tsx
import React, { useState } from 'react'
import Swal from 'sweetalert2'
import { useAuth } from '../../store/AuthContext'
import TopNavigationBar from '../../components/TopNavigationBar/TopNavigationBar'
import styles from './AppLayout.module.css'

interface AppLayoutProps {
  children: React.ReactNode
  currentMode: string
  setMode: (mode: string) => void
}

export default function AppLayout({ children, currentMode, setMode }: AppLayoutProps) {
  const { currentUser, login } = useAuth()

  // --- HARDWARE & UTILITY STATES ---
  const [isLocked, setIsLocked] = useState(false)
  const [unlockPassword, setUnlockPassword] = useState('')
  const [isUnlocking, setIsUnlocking] = useState(false)

  const [showCalculator, setShowCalculator] = useState(false)
  const [calcDisplay, setCalcDisplay] = useState('0')

  // --- HARDWARE ACTIONS ---
  const handleOpenDrawer = async () => {
    try {
      // @ts-ignore - Assuming you add this to your preload script later!
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

  // --- LOCK SCREEN ACTIONS ---
  const handleUnlock = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!currentUser || !unlockPassword) return

    setIsUnlocking(true)
    // Re-verify the password using our secure AuthContext
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

  // --- CALCULATOR ACTIONS ---
  const handleCalcInput = (val: string) => {
    if (calcDisplay === '0' && val !== '.') {
      setCalcDisplay(val)
    } else {
      setCalcDisplay((prev) => prev + val)
    }
  }

  const handleCalcEval = () => {
    try {
      // Safely evaluate the math string
      const result = new Function('return ' + calcDisplay)()
      setCalcDisplay(String(result))
    } catch (e) {
      setCalcDisplay('Error')
      setTimeout(() => setCalcDisplay('0'), 1000)
    }
  }

  return (
    <div className={styles.appWrapper}>
      {/* 🚀 THE LOCK SCREEN OVERLAY */}
      {isLocked && (
        <div className={styles.lockScreen}>
          <div className={styles.lockBox}>
            <div style={{ fontSize: '48px', marginBottom: '10px' }}>🔒</div>
            <h2
              style={{
                margin: 0,
                color: 'var(--text-dark)',
                fontSize: '24px',
                textTransform: 'uppercase'
              }}
            >
              Register Locked
            </h2>
            <p style={{ color: 'var(--text-muted)', fontWeight: 600, marginTop: '5px' }}>
              Locked by:{' '}
              <span style={{ color: 'var(--brand-primary)' }}>{currentUser?.FullName}</span>
            </p>

            <form onSubmit={handleUnlock} style={{ width: '100%', marginTop: '20px' }}>
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

      {/* 🚀 THE CALCULATOR MODAL */}
      {showCalculator && (
        <div className={styles.calcOverlay} onClick={() => setShowCalculator(false)}>
          <div className={styles.calcBox} onClick={(e) => e.stopPropagation()}>
            <div className={styles.calcHeader}>
              <span style={{ fontWeight: 900 }}>Quick Calc</span>
              <button
                className="pos-btn danger"
                style={{ minHeight: '30px', padding: '0 10px' }}
                onClick={() => setShowCalculator(false)}
              >
                ✖
              </button>
            </div>
            <div className={styles.calcDisplay}>{calcDisplay}</div>
            <div className={styles.calcGrid}>
              {['7', '8', '9', '/', '4', '5', '6', '*', '1', '2', '3', '-', '0', '.', '=', '+'].map(
                (btn) => (
                  <button
                    key={btn}
                    className={styles.calcBtn}
                    onClick={() => {
                      if (btn === '=') handleCalcEval()
                      else handleCalcInput(btn)
                    }}
                  >
                    {btn}
                  </button>
                )
              )}
              <button className={styles.calcClearBtn} onClick={() => setCalcDisplay('0')}>
                CLEAR
              </button>
            </div>
          </div>
        </div>
      )}

      <TopNavigationBar currentMode={currentMode} setMode={setMode} />

      <div className={styles.mainLayout}>
        <nav className={styles.sideNav}>
          <div className={styles.clockWidget}>
            <div className={styles.timeText}>
              {new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
            </div>
            <div className={styles.dateText}>
              {new Date().toLocaleDateString('en-US', {
                weekday: 'short',
                month: 'short',
                day: 'numeric'
              })}
            </div>
          </div>

          <div className={styles.navActions}>
            <button className={styles.actionBtn} onClick={handleOpenDrawer}>
              <span className={styles.actionIcon}>💰</span>
              <span className={styles.actionText}>OPEN DRAWER</span>
            </button>

            <button className={styles.actionBtn} onClick={handleLastReceipt}>
              <span className={styles.actionIcon}>🖨️</span>
              <span className={styles.actionText}>LAST RECEIPT</span>
            </button>

            <button className={styles.actionBtn} onClick={() => setShowCalculator(true)}>
              <span className={styles.actionIcon}>📱</span>
              <span className={styles.actionText}>CALCULATOR</span>
            </button>

            <button
              className={`${styles.actionBtn} ${styles.lockBtn}`}
              onClick={() => setIsLocked(true)}
            >
              <span className={styles.actionIcon}>🔒</span>
              <span className={styles.actionText}>LOCK REGISTER</span>
            </button>
          </div>

          <div className={styles.systemStatus}>
            <span className={styles.statusDot}></span>
            <div className={styles.statusText}>
              <strong>SYSTEM ONLINE</strong>
              <span>Version 2.0.0</span>
            </div>
          </div>
        </nav>

        <main className={styles.contentArea}>{children}</main>
      </div>
    </div>
  )
}
