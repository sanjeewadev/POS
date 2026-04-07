// src/renderer/src/components/TopNavigationBar/TopNavigationBar.tsx
import styles from './TopNavigationBar.module.css'
import { useAuth } from '../../store/AuthContext'

interface Props {
  currentMode: string
  setMode: (mode: string) => void
}

export default function TopNavigationBar({ currentMode, setMode }: Props) {
  const { currentUser, logout } = useAuth()

  const isAdmin = currentUser?.Role === 0 || currentUser?.Role === 1
  const tabs = ['POS', 'Returns', 'Reports', 'Inventory']
  if (isAdmin) tabs.push('Settings')

  const initials = (currentUser?.FullName ?? 'U')
    .split(' ')
    .map((w: string) => w[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()

  const roleLabel =
    currentUser?.Role === 0 ? 'System Root' : currentUser?.Role === 1 ? 'Admin' : 'Staff'
  const roleClass =
    currentUser?.Role === 0 ? styles.root : currentUser?.Role === 1 ? styles.admin : ''

  return (
    <header className={styles.headerContainer}>
      {/* LEFT: Brand */}
      <div className={styles.brand}>
        <span className={styles.brandText}>
          Universal<span>POS</span>
        </span>
      </div>

      {/* CENTER: Massive Touch Tabs */}
      <nav className={`${styles.tabContainer} ${styles.noDrag}`}>
        {tabs.map((tab) => (
          <button
            key={tab}
            className={`${styles.tabButton} ${currentMode === tab ? styles.tabActive : ''}`}
            onClick={() => setMode(tab)}
          >
            {tab}
          </button>
        ))}
      </nav>

      {/* RIGHT: User info & logout */}
      <div className={`${styles.userInfo} ${styles.noDrag}`}>
        <div className={styles.userCard}>
          <div className={styles.avatar}>{initials}</div>
          <div className={styles.userText}>
            <span className={styles.userName}>{currentUser?.FullName || 'User'}</span>
            <span className={`${styles.userRole} ${roleClass}`}>{roleLabel}</span>
          </div>
        </div>

        <button className={styles.logoutBtn} onClick={() => logout()}>
          LOG OFF
        </button>
      </div>
    </header>
  )
}
