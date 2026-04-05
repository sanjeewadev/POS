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

  if (isAdmin) {
    tabs.push('Settings')
  }

  const handleLogout = () => {
    // We completely removed window.confirm() to prevent Chromium from dropping focus!
    logout()
  }

  return (
    <header className={styles.headerContainer}>
      {/* LEFT: Massive Retail Branding */}
      <div className={styles.brand}>
        UNIVERSAL<span>POS</span>
      </div>

      {/* CENTER: Giant Touch-Friendly Tabs */}
      <div className={`${styles.tabContainer} ${styles.noDrag}`}>
        {tabs.map((tab) => (
          <button
            key={tab}
            className={`${styles.tabButton} ${currentMode === tab ? styles.tabActive : ''}`}
            onClick={() => setMode(tab)}
          >
            {tab}
          </button>
        ))}
      </div>

      {/* RIGHT: User Info & Giant Logout Button */}
      <div className={`${styles.userInfo} ${styles.noDrag}`}>
        <div className={styles.userText}>
          <span className={styles.userName}>{currentUser?.FullName || 'Cashier'}</span>
          <span className={styles.userRole}>
            {currentUser?.Role === 0 ? 'SYSTEM ROOT' : currentUser?.Role === 1 ? 'ADMIN' : 'STAFF'}
          </span>
        </div>

        {/* Notice we are using the global CSS classes from global.css here! */}
        <button
          className="pos-btn danger"
          onClick={handleLogout}
          style={{ minHeight: '50px', padding: '0 20px', fontSize: '14px' }}
        >
          <svg
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="3"
            strokeLinecap="round"
            strokeLinejoin="round"
            style={{ marginRight: '8px' }}
          >
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path>
            <polyline points="16 17 21 12 16 7"></polyline>
            <line x1="21" y1="12" x2="9" y2="12"></line>
          </svg>
          LOG OFF
        </button>
      </div>
    </header>
  )
}
