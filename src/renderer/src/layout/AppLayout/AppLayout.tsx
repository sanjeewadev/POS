// src/renderer/src/layout/AppLayout/AppLayout.tsx
import React, { useState } from 'react'
import Swal from 'sweetalert2'
import { useAuth } from '../../store/AuthContext'
import TopNavigationBar from '../../components/TopNavigationBar/TopNavigationBar'
import Sidebar from '../../components/Sidebar/Sidebar'
import styles from './AppLayout.module.css'
import { RiLockLine } from 'react-icons/ri'

interface AppLayoutProps {
  children: React.ReactNode
  currentMode: string
  setMode: (mode: string) => void
  onOpenTodaysSales: () => void // Register the prop
}

export default function AppLayout({
  children,
  currentMode,
  setMode,
  onOpenTodaysSales
}: AppLayoutProps) {
  const { currentUser, login } = useAuth()

  const [isLocked, setIsLocked] = useState(false)
  const [unlockPassword, setUnlockPassword] = useState('')
  const [isUnlocking, setIsUnlocking] = useState(false)
  const [showCalculator, setShowCalculator] = useState(false)
  const [calcDisplay, setCalcDisplay] = useState('0')

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

  const handleCalcInput = (val: string) => {
    if (calcDisplay === '0' && val !== '.') setCalcDisplay(val)
    else setCalcDisplay((prev) => prev + val)
  }

  const handleCalcEval = () => {
    try {
      const result = new Function('return ' + calcDisplay)()
      setCalcDisplay(String(result))
    } catch {
      setCalcDisplay('Error')
      setTimeout(() => setCalcDisplay('0'), 1000)
    }
  }

  return (
    <div className={styles.appWrapper}>
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

      {/* Calculator Modal */}
      {showCalculator && (
        <div className={styles.calcOverlay} onClick={() => setShowCalculator(false)}>
          <div className={styles.calcBox} onClick={(e) => e.stopPropagation()}>
            <div className={styles.calcHeader}>
              <span>Quick Calc</span>
              <button
                className="pos-btn danger"
                style={{ minHeight: '30px', padding: '0 10px' }}
                onClick={() => setShowCalculator(false)}
              >
                Close
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
                Clear
              </button>
            </div>
          </div>
        </div>
      )}

      <TopNavigationBar currentMode={currentMode} setMode={setMode} />

      <div className={styles.mainLayout}>
        <Sidebar
          onOpenDrawer={handleOpenDrawer}
          onLastReceipt={handleLastReceipt}
          onCalculator={() => setShowCalculator(true)}
          onTodaysSales={onOpenTodaysSales}
          onLockRegister={() => setIsLocked(true)}
        />
        <main className={styles.contentArea}>{children}</main>
      </div>
    </div>
  )
}
