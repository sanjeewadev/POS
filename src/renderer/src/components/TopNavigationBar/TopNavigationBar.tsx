// src/renderer/src/components/TopNavigationBar/TopNavigationBar.tsx
import styles from './TopNavigationBar.module.css'
import { useAuth } from '../../store/AuthContext'

interface Props {
  currentMode: string
  setMode: (mode: string) => void
}

export default function TopNavigationBar({ currentMode, setMode }: Props) {
  const { currentUser, logout } = useAuth()

  // 🚀 RBAC LOGIC: Admins (1) and Root (0) see everything. Staff cannot see Settings.
  const isAdmin = currentUser?.Role === 0 || currentUser?.Role === 1
  const tabs = ['POS', 'Returns', 'Inventory', 'Reports']
  if (isAdmin) tabs.push('Settings')

  // Derive initials from full name (up to 2 chars)
  const initials = (currentUser?.FullName ?? 'U')
    .split(' ')
    .map((w: string) => w[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()

  // Role display label + CSS modifier class
  const roleLabel =
    currentUser?.Role === 0 ? 'System Root' : currentUser?.Role === 1 ? 'Admin' : 'Staff'
  const roleClass =
    currentUser?.Role === 0 ? styles.root : currentUser?.Role === 1 ? styles.admin : ''

  const handleLogout = () => {
    // Completely removed window.confirm() to prevent Chromium from dropping focus!
    logout()
  }

  return (
    <header className={styles.headerContainer}>
      {/* LEFT: Brand */}
      <div className={styles.brand}>
        <span className={styles.brandText}>
          Universal<span>POS</span>
        </span>
      </div>

      {/* CENTER: Tabs */}
      <nav className={`${styles.tabContainer} ${styles.noDrag}`}>
        {tabs.map((tab) => (
          <button
            key={tab}
            className={`${styles.tabButton} ${currentMode === tab ? styles.tabActive : ''}`}
            onClick={() => setMode(tab)}
          >
            <span className={styles.tabDot} />
            {tab}
          </button>
        ))}
      </nav>

      {/* RIGHT: User info & logout */}
      <div className={`${styles.userInfo} ${styles.noDrag}`}>
        <div className={styles.divider} />

        <div className={styles.userCard}>
          <div className={styles.avatar}>{initials}</div>
          <div className={styles.userText}>
            <span className={styles.userName}>{currentUser?.FullName || 'Cashier'}</span>
            <span className={`${styles.userRole} ${roleClass}`}>{roleLabel}</span>
          </div>
        </div>

        <button className={styles.logoutBtn} onClick={handleLogout}>
          <svg
            width="13"
            height="13"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
            <polyline points="16 17 21 12 16 7" />
            <line x1="21" y1="12" x2="9" y2="12" />
          </svg>
          Log off
        </button>
      </div>
    </header>
  )
}
